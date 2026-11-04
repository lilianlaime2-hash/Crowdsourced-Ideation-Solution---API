'use strict';

const { spawn } = require('node:child_process');
const path = require('node:path');

function resolveOutput(root, value) {
    const result = path.resolve(root, value);
    const relative = path.relative(path.join(root, 'TestResults'), result);
    if (!relative || relative.startsWith('..') || path.isAbsolute(relative)) {
        throw new Error('Output must be a subdirectory of TestResults.');
    }
    return result;
}

function dockerAddress(env = process.env) {
    if (env.TESTCONTAINERS_HOST_OVERRIDE) return env.TESTCONTAINERS_HOST_OVERRIDE;
    if (/^tcp:\/\//.test(env.DOCKER_HOST || '')) return new URL(env.DOCKER_HOST).hostname;
    return '127.0.0.1';
}

function run(command, args, { cwd, env, timeout = 180000, allowFailure = false } = {}) {
    return new Promise((resolve, reject) => {
        const child = spawn(command, args, { cwd, env, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
        let stdout = '', stderr = '', expired = false;
        const timer = setTimeout(() => { expired = true; child.kill(); }, timeout);
        child.stdout.on('data', chunk => { stdout = (stdout + chunk).slice(-2000000); });
        child.stderr.on('data', chunk => { stderr = (stderr + chunk).slice(-2000000); });
        child.once('error', error => { clearTimeout(timer); reject(error); });
        child.once('close', code => {
            clearTimeout(timer);
            if (expired) return reject(new Error(`${path.basename(command)} timeout after ${timeout}ms`));
            const result = { code, stdout, stderr };
            if (code === 0 || allowFailure) resolve(result);
            else reject(new Error(`${path.basename(command)} exit ${code}:\n${stdout.slice(-12000)}\n${stderr.slice(-12000)}`));
        });
    });
}

async function cleanupResources(resources) {
    const failures = [];
    for (const resource of resources) {
        try { await resource.stop(); }
        catch (error) { failures.push({ name: resource.name, error: error.message }); }
    }
    return failures;
}

function stopperFor(child, timeout = 5000) {
    // Capture close immediately: exitCode stays null after signal termination.
    const closed = new Promise(resolve => child.once('close', resolve));
    const wait = () => new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('API process close event timed out.')), timeout);
        closed.then(() => { clearTimeout(timer); resolve(); });
    });
    return async () => {
        if (!child.pid) return;
        if (child.exitCode === null && child.signalCode === null) child.kill();
        try { await wait(); }
        catch (_) { child.kill('SIGKILL'); await wait(); }
    };
}

module.exports = { resolveOutput, dockerAddress, run, cleanupResources, stopperFor };
