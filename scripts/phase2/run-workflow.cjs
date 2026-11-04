#!/usr/bin/env node
'use strict';

const { mkdirSync, writeFileSync } = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { resolveOutput, run, cleanupResources } = require('./runtime.cjs');
const { startInfrastructure, startApi } = require('./infrastructure.cjs');
const { startUsersFixture, fixtureUsers, fixturePassword, fixtureSigningKey } = require('./users-fixture.cjs');

function parseArguments(args) {
    const result = { mode: 'full',
        output: `TestResults/Phase2/Workflow-${new Date().toISOString().replace(/[:.]/g, '-')}` };
    for (let index = 0; index < args.length; index += 2) {
        const name = args[index]?.replace(/^--/, '');
        if (!['mode', 'output'].includes(name) || !args[index + 1]) throw new Error('Unknown or incomplete workflow argument.');
        result[name] = args[index + 1];
    }
    if (!['full', 'migration', 'functional'].includes(result.mode)) throw new Error('Invalid workflow mode.');
    return result;
}

async function main(args = process.argv.slice(2)) {
    const options = parseArguments(args);
    const root = path.resolve(__dirname, '../..');
    const output = resolveOutput(root, options.output);
    mkdirSync(output, { recursive: true });
    const evidence = { startedAt: new Date().toISOString(), options, environment: {
        platform: process.platform, arch: process.arch, node: process.version,
        cpu: os.cpus()[0]?.model, logicalProcessors: os.cpus().length, memoryBytes: os.totalmem(),
        mysqlImage: 'mysql:8.4', mongoImage: 'mongo:8.0', usersApi: 'isolated fixture (not Java Users API)'
    }, migration: false, functional: [], passed: false };
    let infrastructure, users, failure;
    const apis = [];
    const save = () => writeFileSync(path.join(output, 'workflow.json'), JSON.stringify(evidence, null, 2) + '\n');
    const progress = message => console.log(`[required-checks] ${message}`);
    try {
        progress('Building isolated workflow dependencies');
        for (const project of ['CIS.API/CIS.API.csproj', 'tools/CIS.Migration/CIS.Migration.csproj']) {
            await run('dotnet', ['build', project, '-c', 'Release', '-m:1', '--verbosity', 'quiet'], { cwd: root });
        }
        evidence.environment.dotnet = (await run('dotnet', ['--version'])).stdout.trim();
        evidence.environment.docker = (await run('docker', ['version', '--format', '{{.Server.Version}}'])).stdout.trim();
        evidence.environment.dockerResources = JSON.parse((await run('docker', ['info', '--format', '{"logicalProcessors":{{.NCPU}},"memoryBytes":{{.MemTotal}}}'])).stdout);
        progress('Starting fresh MySQL and MongoDB containers');
        infrastructure = await startInfrastructure();
        const migrationEnv = { ...process.env, CIS_MYSQL_CONNECTION: infrastructure.mysqlConnection,
            CIS_MONGO_CONNECTION: infrastructure.mongoConnection };
        const migrationDll = path.join(root, 'tools/CIS.Migration/bin/Release/net10.0/CIS.Migration.dll');
        const migration = async args => run('dotnet', [migrationDll, ...args], { cwd: root, env: migrationEnv });
        for (const command of ['seed', 'migrate']) {
            progress(command);
            const result = await migration([command]);
            writeFileSync(path.join(output, command + '.log'), result.stdout + result.stderr);
        }
        progress('Comparing canonical MySQL/MongoDB SHA-256 projections');
        await migration(['verify', '--report', path.join(output, 'migration-before.json')]);
        await run('docker', ['cp', path.join(root, 'scripts/migration/post-migration-sd3-perf.js'),
            infrastructure.names.mongo + ':/tmp/cis-post-migration.js']);
        const postMigration = await run('docker', ['exec', infrastructure.names.mongo, 'mongosh',
            'sd3db_mongo_perf', '--quiet', '--file', '/tmp/cis-post-migration.js']);
        writeFileSync(path.join(output, 'post-migration.log'), postMigration.stdout + postMigration.stderr);
        await migration(['verify', '--report', path.join(output, 'migration-after.json')]);
        evidence.migration = true;
        save();
        if (options.mode !== 'migration') {
            users = await startUsersFixture();
            for (const provider of ['MySQL', 'MongoDB']) {
                const api = await startApi({ root, provider, infrastructure, usersBaseUrl: users.url,
                    signingKey: fixtureSigningKey, logPath: path.join(output, `api-${provider}.log`) });
                apis.push({ ...api, provider });
            }
            if (['full', 'functional'].includes(options.mode)) {
                const { runPostman } = require('./postman.cjs');
                for (const api of apis) {
                    progress(`Postman CLI ${api.provider}`);
                    const result = await runPostman({ baseUrl: api.url, usersBaseUrl: users.url,
                        login: fixtureUsers[0].login, password: fixturePassword,
                        outputDirectory: path.join(output, `postman-${api.provider}`) });
                    evidence.functional.push({ provider: api.provider, ...result });
                    save();
                }
            }
        }
        evidence.passed = true;
    } catch (error) {
        evidence.error = error.message;
        failure = error;
    } finally {
        const resources = apis.reverse().map(api => ({ name: `api-${api.provider}`, stop: api.stop }));
        if (users) resources.push({ name: 'users-fixture', stop: users.stop });
        if (infrastructure) resources.push({ name: 'databases', stop: infrastructure.stop });
        evidence.cleanupErrors = await cleanupResources(resources);
        if (evidence.cleanupErrors.length) {
            evidence.passed = false;
            failure ??= new Error('Resource cleanup failed; see workflow.json.');
            evidence.error ??= failure.message;
        }
        evidence.finishedAt = new Date().toISOString();
        save();
    }
    if (failure) throw failure;
    progress(`PASS; reports: ${output}`);
    return evidence;
}

module.exports = { main, parseArguments };
if (require.main === module) main().catch(error => { console.error(error.message); process.exitCode = 1; });
