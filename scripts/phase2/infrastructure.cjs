'use strict';

const { spawn } = require('node:child_process');
const { writeFileSync } = require('node:fs');
const { randomUUID } = require('node:crypto');
const path = require('node:path');
const { run, dockerAddress, cleanupResources, stopperFor } = require('./runtime.cjs');

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

async function startInfrastructure() {
    const id = randomUUID();
    const names = { mysql: `cis-phase2-${id}-mysql`, mongo: `cis-phase2-${id}-mongo` };
    const host = dockerAddress();
    const password = randomUUID();
    const publish = port => host === '127.0.0.1' ? `127.0.0.1::${port}` : String(port);
    const stop = async () => {
        const failures = await cleanupResources(Object.values(names).map(name => ({ name, stop: async () => {
            const result = await run('docker', ['inspect', '--format', '{{.Name}}', name], { allowFailure: true });
            if (result.code === 0 && result.stdout.trim() === '/' + name) {
                await run('docker', ['rm', '--force', name]);
            } else if (result.code !== 0 && !/No such (object|container)/i.test(result.stderr)) {
                throw new Error(`Could not inspect temporary container ${name}.`);
            }
        } })));
        if (failures.length) throw new Error(failures.map(item => item.error).join(' '));
    };
    try {
        await run('docker', ['run', '-d', '--rm', '--name', names.mysql, '--publish', publish(3306),
            '-e', `MYSQL_ROOT_PASSWORD=${password}`, '-e', 'MYSQL_ROOT_HOST=%',
            '-e', 'MYSQL_DATABASE=cis_phase2_source', 'mysql:8.4']);
        await run('docker', ['run', '-d', '--rm', '--name', names.mongo, '--publish', publish(27017), 'mongo:8.0']);
        const deadline = Date.now() + 90000;
        for (const [name, command] of [
            [names.mysql, ['-e', `MYSQL_PWD=${password}`, names.mysql, 'mysql', '-h127.0.0.1', '-uroot', '-N', '-e', 'SELECT 1', 'cis_phase2_source']],
            [names.mongo, [names.mongo, 'mongosh', '--quiet', '--eval', 'db.runCommand({ping:1})']]
        ]) {
            while (true) {
                const result = await run('docker', ['exec', ...command], { allowFailure: true, timeout: 10000 });
                if (result.code === 0) break;
                if (Date.now() > deadline) throw new Error(`Temporary database did not become ready: ${name}`);
                await delay(300);
            }
        }
        const port = async (name, containerPort) => {
            const result = await run('docker', ['inspect', '--format', '{{json .NetworkSettings.Ports}}', name]);
            return JSON.parse(result.stdout)[containerPort + '/tcp'][0].HostPort;
        };
        return { names, stop,
            mysqlConnection: `Server=${host};Port=${await port(names.mysql, 3306)};Database=cis_phase2_source;User ID=root;Password=${password};SslMode=Disabled;AllowPublicKeyRetrieval=True;`,
            mongoConnection: `mongodb://${host}:${await port(names.mongo, 27017)}/sd3db_mongo_perf` };
    } catch (error) {
        try { await stop(); } catch (cleanupError) { error.message += ` Cleanup: ${cleanupError.message}`; }
        throw error;
    }
}

async function startApi({ root, provider, infrastructure, usersBaseUrl, signingKey, logPath }) {
    const child = spawn('dotnet', [path.join(root, 'CIS.API/bin/Release/net10.0/CIS.API.dll')], {
        cwd: path.join(root, 'CIS.API'), windowsHide: true,
        env: { ...process.env, DOTNET_ENVIRONMENT: 'Testing', ASPNETCORE_ENVIRONMENT: 'Testing',
            ASPNETCORE_URLS: 'http://127.0.0.1:0', Persistence__Provider: provider,
            ConnectionStrings__LegacyDatabase: infrastructure.mysqlConnection,
            ConnectionStrings__MongoDatabase: infrastructure.mongoConnection,
            UsersApi__BaseUrl: usersBaseUrl, Jwt__SigningKey: signingKey,
            Logging__LogLevel__Default: 'Warning', 'Logging__LogLevel__Microsoft.Hosting.Lifetime': 'Information' }
    });
    const stopProcess = stopperFor(child);
    let logs = '', url, spawnError;
    const append = chunk => {
        logs = (logs + chunk).slice(-2000000);
        const match = logs.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
        if (match) url = match[1];
    };
    child.stdout.on('data', append);
    child.stderr.on('data', append);
    child.once('error', error => { spawnError = error; });
    const stop = async () => {
        try { await stopProcess(); }
        finally { writeFileSync(logPath, logs); }
    };
    try {
        const deadline = Date.now() + 45000;
        while (!url) {
            if (spawnError) throw spawnError;
            if (child.exitCode !== null || Date.now() > deadline) throw new Error(`${provider} API did not start:\n${logs}`);
            await delay(100);
        }
        const probe = await fetch(url + '/api/topics', { signal: AbortSignal.timeout(10000) });
        if (probe.status !== 401) throw new Error(`${provider} API authentication probe returned ${probe.status}`);
        return { url, stop };
    } catch (error) {
        try { await stop(); } catch (cleanupError) { error.message += ` Cleanup: ${cleanupError.message}`; }
        throw error;
    }
}

module.exports = { startInfrastructure, startApi };
