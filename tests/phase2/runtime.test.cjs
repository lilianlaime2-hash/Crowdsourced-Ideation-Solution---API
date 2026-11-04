const assert = require('node:assert/strict');
const { test } = require('node:test');
const path = require('node:path');
const { resolveOutput, dockerAddress, run, cleanupResources, stopperFor } = require('../../scripts/phase2/runtime.cjs');
const { spawn } = require('node:child_process');
const { once, EventEmitter } = require('node:events');

test('reports must stay inside TestResults without replacing its root', () => {
    const root = path.resolve(__dirname, '../..');
    assert.equal(resolveOutput(root, 'TestResults/Phase2/example'), path.join(root, 'TestResults/Phase2/example'));
    for (const value of ['../escape', '.', 'TestResults', 'TestResults/../scripts']) {
        assert.throws(() => resolveOutput(root, value), /TestResults/);
    }
});

test('Docker published ports use the daemon host in CI and loopback locally', () => {
    assert.equal(dockerAddress({}), '127.0.0.1');
    assert.equal(dockerAddress({ DOCKER_HOST: 'tcp://docker:2375' }), 'docker');
    assert.equal(dockerAddress({ TESTCONTAINERS_HOST_OVERRIDE: 'custom-host' }), 'custom-host');
    assert.equal(dockerAddress({ DOCKER_HOST: 'npipe:////./pipe/docker_engine' }), '127.0.0.1');
});

test('child command failure rejects with its code; successful stdout is retained', async () => {
    const result = await run(process.execPath, ['-e', 'process.stdout.write("verified")']);
    assert.equal(result.stdout, 'verified');
    await assert.rejects(run(process.execPath, ['-e', 'process.exit(7)']), /exit 7/);
    await assert.rejects(run(process.execPath, ['-e', 'setTimeout(()=>{},10000)'], { timeout: 30 }), /timeout/);
});

test('cleanup attempts every resource in order and retains failures without stopping teardown', async () => {
    const visited = [];
    const failures = await cleanupResources([
        { name: 'api', stop: async () => { visited.push('api'); throw new Error('API stop failed'); } },
        { name: 'users', stop: async () => { visited.push('users'); } },
        { name: 'databases', stop: async () => { visited.push('databases'); throw new Error('Docker unavailable'); } }
    ]);
    assert.deepEqual(visited, ['api', 'users', 'databases']);
    assert.deepEqual(failures, [{ name: 'api', error: 'API stop failed' }, { name: 'databases', error: 'Docker unavailable' }]);
});

test('API process cleanup resolves after a previously delivered signal/close and is idempotent', async t => {
    const child = spawn(process.execPath, ['-e', 'setInterval(()=>{},1000)'], { windowsHide: true });
    t.after(() => child.kill());
    const stop = stopperFor(child, 1000);
    await once(child, 'spawn');
    const closed = once(child, 'close');
    child.kill();
    await closed;
    await stop();
    await stop();
    assert.notEqual(child.signalCode, null);
});

test('API process cleanup terminates a running process and bounds an undelivered close event', async t => {
    const child = spawn(process.execPath, ['-e', 'setInterval(()=>{},1000)'], { windowsHide: true });
    t.after(() => child.kill());
    const stop = stopperFor(child, 1000);
    await once(child, 'spawn');
    await stop();
    assert.ok(child.exitCode !== null || child.signalCode !== null);
    const unresponsive = Object.assign(new EventEmitter(), { pid: 123, exitCode: null, signalCode: null });
    const signals = [];
    unresponsive.kill = signal => { signals.push(signal || 'SIGTERM'); return true; };
    await assert.rejects(stopperFor(unresponsive, 10)(), /close/);
    assert.deepEqual(signals, ['SIGTERM', 'SIGKILL']);
});

