// Runs real k6 against CIS API and isolated MongoDB; only the external Users API is simulated.
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const { randomUUID, createHmac } = require('node:crypto');
const { createServer, request: proxyRequest } = require('node:http');
const { mkdtempSync, readFileSync, rmSync } = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { before, after, test } = require('node:test');
const { dockerAddress } = require('../../scripts/phase2/runtime.cjs');

const root = path.resolve(__dirname, '../..');
const container = `cis-k6-verify-${randomUUID()}`;
const outputDirectory = mkdtempSync(path.join(os.tmpdir(), 'cis-k6-verify-'));
const database = 'cis_k6_verification';
const userId = '11111111-1111-4111-8111-111111111111';
const signingKey = 'cis-k6-verification-key-only-not-a-production-secret';
const mongoHost = dockerAddress();
let usersServer, proxyServer, apiProcess, apiUrl, usersUrl, proxyUrl;
let logs = '';

function processResult(command, args, options = {}) {
    return new Promise((resolve, reject) => {
        const child = spawn(command, args, { cwd: root, windowsHide: true, ...options });
        let stdout = '', stderr = '';
        child.stdout.on('data', data => { stdout += data; });
        child.stderr.on('data', data => { stderr += data; });
        child.once('error', reject);
        child.once('close', code => resolve({ code, stdout, stderr }));
    });
}

function mongo(code) {
    return execFileSync('docker', ['exec', container, 'mongosh', database, '--quiet', '--eval', code],
        { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }).trim();
}

async function listen(server) {
    await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(0, '127.0.0.1', resolve);
    });
    return `http://127.0.0.1:${server.address().port}`;
}

function token() {
    const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
    const content = encode({ alg: 'HS256', typ: 'JWT' }) + '.' +
        encode({ sub: userId, exp: Math.floor(Date.now() / 1000) + 3600 });
    return content + '.' + createHmac('sha256', signingKey).update(content).digest('base64url');
}

function respond(response, status, body) {
    response.writeHead(status, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify(body));
}

before(async () => {
    const build = await processResult('dotnet', ['build', 'CIS.API/CIS.API.csproj', '--no-restore', '--verbosity', 'quiet']);
    assert.equal(build.code, 0, build.stdout + build.stderr);
    execFileSync('docker', ['run', '-d', '--rm', '--name', container,
        '--publish', mongoHost === '127.0.0.1' ? '127.0.0.1::27017' : '27017', 'mongo:8.0'], { stdio: 'pipe' });
    const deadline = Date.now() + 30000;
    while (true) {
        try { mongo('db.runCommand({ping:1})'); break; }
        catch (error) {
            if (Date.now() > deadline) throw error;
            await new Promise(resolve => setTimeout(resolve, 250));
        }
    }
    const ports = JSON.parse(execFileSync('docker', ['inspect', container], { encoding: 'utf8' }));
    const mongoPort = ports[0].NetworkSettings.Ports['27017/tcp'][0].HostPort;
    mongo(`
        const user='${userId}'; const date=ISODate('2026-10-01T12:00:00Z');
        db.topics.insertMany([1,2].map(n=>({_id:NumberLong(String(n)),userId:user,
            title:'Fixture topic '+n,description:'Fixture description',status:'Active',
            createdAt:date,updatedAt:date,tagIds:[]})));
        db.ideas.insertMany([1,2,3].map(n=>({_id:NumberLong(String(n)),topicId:NumberLong('2'),userId:user,
            title:'Fixture idea '+n,description:'Fixture description',status:'Active',createdAt:date,updatedAt:date})));
        db.votes.insertOne({_id:NumberLong('1'),ideaId:NumberLong('1'),userId:user,createdAt:date});
        db.votes.createIndex({ideaId:1,userId:1},{unique:true,name:'ux_votes_ideaId_userId'});
        db.counters.insertMany([['topics',2],['ideas',3],['votes',1],['tags',0]].map(([name,n])=>({_id:name,seq:NumberLong(String(n))})));
    `);
    usersServer = createServer(async (request, response) => {
        if (request.url === '/auth/login' && request.method === 'POST') {
            const chunks = [];
            for await (const chunk of request) chunks.push(chunk);
            const body = JSON.parse(Buffer.concat(chunks).toString());
            return body.login === 'fixture' && body.password === 'fixture-password'
                ? respond(response, 200, { id: userId, name: 'Fixture author', login: 'fixture', token: token() })
                : respond(response, 401, { message: 'Invalid credentials' });
        }
        if (request.url === `/users/${userId}`) {
            return respond(response, 200, { id: userId, name: 'Fixture author', login: 'fixture' });
        }
        respond(response, 404, { message: 'Not found' });
    });
    usersUrl = await listen(usersServer);
    apiProcess = spawn('dotnet', ['bin/Debug/net10.0/CIS.API.dll'], {
        cwd: path.join(root, 'CIS.API'), windowsHide: true,
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Testing', DOTNET_ENVIRONMENT: 'Testing',
            ASPNETCORE_URLS: 'http://127.0.0.1:0', Persistence__Provider: 'MongoDB',
            ConnectionStrings__MongoDatabase: `mongodb://${mongoHost}:${mongoPort}/${database}`,
            UsersApi__BaseUrl: usersUrl, Jwt__SigningKey: signingKey,
            Logging__LogLevel__Default: 'Warning', 'Logging__LogLevel__Microsoft.Hosting.Lifetime': 'Information' }
    });
    // Use a direct health request once Kestrel reports the automatically assigned port.
    apiProcess.stdout.on('data', chunk => {
        logs += chunk;
        const match = logs.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
        if (match) apiUrl = match[1];
    });
    apiProcess.stderr.on('data', chunk => { logs += chunk; });
    const startupDeadline = Date.now() + 30000;
    while (!apiUrl) {
        if (apiProcess.exitCode !== null || Date.now() > startupDeadline) throw new Error('API did not start: ' + logs);
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    const probe = await fetch(apiUrl + '/api/topics');
    assert.equal(probe.status, 401);
    proxyServer = createServer((request, response) => {
        const failUpdate = request.url.startsWith('/fail-update/');
        const invalidPayload = request.url.startsWith('/invalid-payload/');
        const invalidVote = request.url.startsWith('/invalid-vote/');
        const requestPath = request.url.replace(/^\/(fail-update|invalid-payload|invalid-vote)/, '');
        if (failUpdate && request.method === 'PUT' && requestPath.startsWith('/api/ideas/')) {
            request.resume();
            return respond(response, 500, { message: 'Intentional test failure' });
        }
        if (invalidPayload && requestPath.startsWith('/api/topics?')) {
            request.resume();
            return respond(response, 200, { items: [null], page: 1, pageSize: 10, totalItems: 1, totalPages: 1 });
        }
        const upstream = proxyRequest(apiUrl + requestPath, { method: request.method, headers: request.headers }, incoming => {
            if (invalidVote && request.method === 'POST' && requestPath.endsWith('/votes')) {
                incoming.resume();
                incoming.on('end', () => respond(response, incoming.statusCode, { id: 'malformed' }));
                return;
            }
            response.writeHead(incoming.statusCode, incoming.headers);
            incoming.pipe(response);
        });
        upstream.on('error', error => respond(response, 502, { message: error.message }));
        request.pipe(upstream);
    });
    proxyUrl = await listen(proxyServer);
});

after(async () => {
    if (apiProcess && apiProcess.exitCode === null) {
        const closed = new Promise(resolve => apiProcess.once('close', resolve));
        apiProcess.kill();
        await closed;
    }
    if (proxyServer) await new Promise(resolve => proxyServer.close(resolve));
    if (usersServer) await new Promise(resolve => usersServer.close(resolve));
    try { execFileSync('docker', ['rm', '-f', container], { stdio: 'pipe' }); } catch (_) { /* setup may have failed */ }
    const resolvedOutput = path.resolve(outputDirectory);
    if (path.dirname(resolvedOutput) !== path.resolve(os.tmpdir()) ||
        !path.basename(resolvedOutput).startsWith('cis-k6-verify-')) {
        throw new Error('Unexpected temporary output directory; cleanup stopped.');
    }
    rmSync(resolvedOutput, { recursive: true, force: true });
});

async function run(file, override = {}) {
    const summaryPath = path.join(outputDirectory, randomUUID() + '.json');
    const inherited = { ...process.env };
    // Native K6_DURATION/K6_VUS options override scenarios; keep fixture runs deterministic.
    for (const key of Object.keys(inherited)) if (key.startsWith('K6_')) delete inherited[key];
    const result = await processResult('k6', ['run', '--quiet', '--no-usage-report', '--summary-export', summaryPath,
        path.join(__dirname, file)], {
        env: { ...inherited, CIS_BASE_URL: apiUrl, USERS_BASE_URL: usersUrl,
            K6_LOGIN: 'fixture', K6_PASSWORD: 'fixture-password', K6_TOKEN: '',
            PERF_PROFILE: 'smoke', PERF_VUS: '50', PERF_SLEEP: '0', PERF_TOPIC_ID: '',
            PERF_PAGE_SIZE: '10', PERF_P95_MS: '10000', PERF_DURATION: '1s', ...override }
    });
    let summary;
    try { summary = JSON.parse(readFileSync(summaryPath, 'utf8')); } catch (_) { /* setup failures may not export */ }
    return { ...result, summary };
}

function metric(summary, name) {
    assert.ok(summary && summary.metrics && summary.metrics[name], `Missing metric ${name}`);
    return summary.metrics[name].values || summary.metrics[name];
}

for (const file of ['get-topics-k6.js','get-topic-k6.js','get-ideas-k6.js',
    'most-voted-ideas-k6.js','most-active-topics-k6.js']) {
    test(`real HTTP smoke: ${file}`, async () => {
        const result = await run(file);
        assert.equal(result.code, 0, result.stdout + result.stderr);
        assert.equal(metric(result.summary, 'http_reqs{phase:measure}').count, 1);
    });
}

test('burst sends 50 measured reads and excludes login/discovery from that count', async () => {
    const result = await run('get-topic-k6.js', { PERF_PROFILE: 'burst' });
    assert.equal(result.code, 0, result.stdout + result.stderr);
    assert.equal(metric(result.summary, 'http_reqs{phase:measure}').count, 50);
    assert.equal(metric(result.summary, 'http_reqs').count, 52);
});

test('load profile repeats measured requests during the configured duration', async () => {
    const result = await run('get-topics-k6.js', { PERF_PROFILE: 'load', PERF_VUS: '2', PERF_DURATION: '1s' });
    assert.equal(result.code, 0, result.stdout + result.stderr);
    assert.ok(metric(result.summary, 'http_reqs{phase:measure}').count > 2);
});

test('CRUD burst completes 50 independent workflows against real API/MongoDB', async () => {
    const result = await run('crud-k6.js', { PERF_PROFILE: 'burst' });
    assert.equal(result.code, 0, result.stdout + result.stderr);
    assert.equal(metric(result.summary, 'http_reqs{phase:measure}').count, 400);
    assert.equal(mongo(`db.topics.countDocuments({status:'Active'})`), '2');
    assert.equal(mongo(`db.ideas.countDocuments({status:'Active'})`), '3');
    assert.equal(mongo(`db.votes.countDocuments({})`), '1');
    assert.equal(mongo(`db.topics.countDocuments({status:'Deleted'})`), '50');
    assert.equal(mongo(`db.ideas.countDocuments({status:'Deleted'})`), '50');
});

test('wrong credentials abort in setup without sending measured requests', async () => {
    const result = await run('get-topics-k6.js', { K6_PASSWORD: 'incorrect' });
    assert.notEqual(result.code, 0);
    assert.match(result.stdout + result.stderr, /Login fallido/);
});

test('HTTP 200 with malformed items still fails performance checks', async () => {
    const result = await run('get-topics-k6.js', { CIS_BASE_URL: proxyUrl + '/invalid-payload' });
    assert.notEqual(result.code, 0, result.stdout + result.stderr);
    assert.ok(metric(result.summary, 'checks{phase:measure}').value < 1);
});

test('failed CRUD step produces failing thresholds and cleans up its created resources', async () => {
    const result = await run('crud-k6.js', { CIS_BASE_URL: proxyUrl + '/fail-update' });
    assert.notEqual(result.code, 0, result.stdout + result.stderr);
    assert.equal(mongo(`db.topics.countDocuments({status:'Active'})`), '2');
    assert.equal(mongo(`db.ideas.countDocuments({status:'Active'})`), '3');
    assert.equal(mongo(`db.votes.countDocuments({})`), '1');
});

test('malformed vote response fails checks but still removes the committed vote before soft-deleting its idea', async () => {
    const result = await run('crud-k6.js', { CIS_BASE_URL: proxyUrl + '/invalid-vote' });
    assert.notEqual(result.code, 0, result.stdout + result.stderr);
    assert.equal(mongo(`db.votes.countDocuments({})`), '1', 'Committed test vote was left behind');
    assert.equal(mongo(`db.topics.countDocuments({status:'Active'})`), '2');
    assert.equal(mongo(`db.ideas.countDocuments({status:'Active'})`), '3');
});
