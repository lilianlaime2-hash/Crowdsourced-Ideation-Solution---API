'use strict';

const assert = require('node:assert/strict');
const { mkdtemp, mkdir, writeFile, readFile, readdir, rm } = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const { runPostman } = require('../../scripts/phase2/postman.cjs');

// A separate real Node process stands in for the executable, not for the API.
// These tests exercise process startup, arguments, report files and cleanup.
const stub = String.raw`
const fs = require('node:fs');
const path = require('node:path');
const [mode, ...args] = process.argv.slice(2);
const flag = name => args[args.indexOf(name) + 1];
const envPath = flag('-e');
const env = JSON.parse(fs.readFileSync(envPath, 'utf8'));
const inputs = Object.fromEntries(env.values.map(v => [v.key, v.value]));
const directory = path.dirname(envPath);
fs.writeFileSync(path.join(path.dirname(directory), 'checks.json'), JSON.stringify({
  inputNames: Object.keys(inputs).sort(),
  secretInArgs: args.some(arg => arg.includes(inputs.login) || arg.includes(inputs.password)),
  collectionExists: fs.existsSync(args[2]),
  eventsDisabled: args.includes('--no-report-events'),
  jsonBodyOmission: args.includes('--reporter-json-omitAllHeadersAndBody')
}));
const failed = mode === 'json-failure' ? 1 : 0;
const total = mode === 'partial' ? 31 : 32;
const assertions = total * 2;
// Real Postman 1.74 counts requests at the root and assertions in each suite.
let xml = '<?xml version="1.0" encoding="UTF-8"?><testsuites tests="' + total + '">';
if (mode === 'root-failure') xml = xml.replace('<testsuites ', '<testsuites failures="1" ');
if (mode === 'wrong-root-count') xml = xml.replace('<testsuites tests="' + total + '">', '<testsuites tests="' + assertions + '">');
for (let n = 0; n < total; n++) {
  xml += '<testsuite name="Request ' + n + '" tests="2" failures="' + (mode === 'junit-failure' && n === 0 ? 1 : 0) + '" errors="0">';
  xml += '<testcase name="HTTP status"/><testcase name="Response data"/>';
  xml += '</testsuite>';
}
xml += '</testsuites>';
if (mode === 'empty') xml = '';
if (mode === 'malformed') xml = '<testsuites tests="1"><testsuite tests="1" failures="0" errors="0"><testcase name="unclosed"/></testsuites>';
if (mode === 'cdata-secret') xml = xml.replace('<testcase name="HTTP status"/>', '<testcase name="HTTP status"><system-out><![CDATA[' + inputs.password + ' eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0In0.dGVzdHNpZ25hdHVyZQ]]></system-out></testcase>');
if (mode !== 'missing') fs.writeFileSync(flag('--reporter-junit-export'), xml);
const report = { environment: env, run: {
  stats: { requests: { total, failed: 0, pending: mode === 'request-pending' ? 1 : 0 }, assertions: { total: assertions, failed, pending: mode === 'pending' ? 1 : 0 }, scripts: { failed: 0 } },
  executions: Array.from({length: total}, () => ({})), failures: failed ? [{}] : [], error: null
}};
if (mode !== 'no-json') fs.writeFileSync(flag('--reporter-json-export'), mode === 'bad-json' ? '{bad' : JSON.stringify(report));
process.exitCode = mode === 'nonzero' ? 7 : 0;
`;

async function fixture(t, mode = 'success') {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'cis-postman-runner-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const outputDirectory = path.join(directory, 'reports with spaces');
  await mkdir(outputDirectory);
  const executable = path.join(directory, 'stub postman.cjs');
  await writeFile(executable, stub);
  const options = { baseUrl: 'http://127.0.0.1:5039', usersBaseUrl: 'http://127.0.0.1:8080',
    login: 'fixture-user', password: 'fixture-password', outputDirectory,
    command: [process.execPath, executable, mode] };
  return { directory, outputDirectory, options };
}

async function cleanAndSanitized(outputDirectory) {
  const names = await readdir(outputDirectory);
  assert.ok(names.every(name => !name.startsWith('.postman-')), 'Sensitive temporary file remains');
  for (const name of names) {
    const contents = await readFile(path.join(outputDirectory, name), 'utf8');
    assert.ok(!contents.includes('fixture-password'));
    assert.ok(!contents.includes('fixture-user'));
  }
}

test('runs all 32 requests asynchronously, preserves variable precedence, exports only safe results', async t => {
  const { directory, outputDirectory, options } = await fixture(t);
  const result = await runPostman(options);
  assert.deepEqual(result, { requests: 32, assertions: 64, passed: 64, failed: 0,
    reportPath: path.join(outputDirectory, 'postman.junit.xml') });
  assert.equal(JSON.parse(await readFile(path.join(outputDirectory, 'summary.json'))).status, 'passed');
  assert.deepEqual(JSON.parse(await readFile(path.join(directory, 'checks.json'))), {
    inputNames: ['baseUrl', 'login', 'password', 'usersBaseUrl'], secretInArgs: false,
    collectionExists: true, eventsDisabled: true, jsonBodyOmission: true
  });
  assert.deepEqual((await readdir(outputDirectory)).sort(), ['postman.junit.xml', 'summary.json']);
  await cleanAndSanitized(outputDirectory);
});

for (const [mode, pattern] of [
  ['missing', /required JUnit/], ['empty', /empty or inconsistent/], ['malformed', /Mismatched/],
  ['junit-failure', /failed, skipped or pending/], ['json-failure', /failed, skipped or pending/],
  ['root-failure', /failed, skipped or pending/], ['request-pending', /failed, skipped or pending/],
  ['wrong-root-count', /empty or inconsistent/],
  ['pending', /failed, skipped or pending/], ['partial', /Incomplete Postman run/],
  ['no-json', /valid JSON/], ['bad-json', /valid JSON/], ['nonzero', /exited unsuccessfully/]
]) {
  test(`rejects ${mode} reports even if a CLI claims success, and removes credentials`, async t => {
    const { options, outputDirectory } = await fixture(t, mode);
    await assert.rejects(runPostman(options), pattern);
    assert.equal(JSON.parse(await readFile(path.join(outputDirectory, 'summary.json'))).status, 'failed');
    await cleanAndSanitized(outputDirectory);
  });
}

test('a stale successful JUnit file cannot mask a missing current report', async t => {
  const { options, outputDirectory } = await fixture(t, 'missing');
  await writeFile(path.join(outputDirectory, 'postman.junit.xml'), '<testsuites tests="1"><testsuite tests="1" failures="0" errors="0"><testcase/></testsuite></testsuites>');
  await assert.rejects(runPostman(options), /required JUnit/);
  assert.ok(!(await readdir(outputDirectory)).includes('postman.junit.xml'));
  await cleanAndSanitized(outputDirectory);
});

test('spawn failure still removes the temporary credential environment', async t => {
  const { options, outputDirectory, directory } = await fixture(t);
  options.command = path.join(directory, 'does-not-exist');
  await assert.rejects(runPostman(options), /Unable to start Postman CLI/);
  await cleanAndSanitized(outputDirectory);
});

test('redacts password and JWT values from the retained JUnit report', async t => {
  const { options, outputDirectory } = await fixture(t, 'cdata-secret');
  await runPostman(options);
  const xml = await readFile(path.join(outputDirectory, 'postman.junit.xml'), 'utf8');
  assert.ok(xml.includes('[REDACTED]'));
  assert.ok(xml.includes('[REDACTED_JWT]'));
  assert.ok(!xml.includes('eyJhbGci'));
  await cleanAndSanitized(outputDirectory);
});

test('short credentials cannot corrupt XML element names or test counters', async t => {
  const { options, outputDirectory } = await fixture(t, 'cdata-secret');
  options.login = 'a';
  options.password = '1';
  const result = await runPostman(options);
  assert.equal(result.assertions, 64);
  const xml = await readFile(path.join(outputDirectory, 'postman.junit.xml'), 'utf8');
  assert.ok(xml.includes('<testsuites tests="32">'));
  assert.ok(xml.includes('<testcase'));
  assert.ok(xml.includes('[REDACTED]'));
  assert.ok(!xml.includes('eyJhbGci'));
});

test('validates required inputs before starting the executable', async t => {
  const { options } = await fixture(t);
  await assert.rejects(runPostman({ ...options, password: '' }), /requires password/);
  await assert.rejects(runPostman({ ...options, baseUrl: 'http://user:pass@localhost' }), /Invalid Postman base URL/);
});
