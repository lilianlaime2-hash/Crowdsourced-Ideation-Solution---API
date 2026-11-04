'use strict';

const { spawn } = require('node:child_process');
const { randomUUID } = require('node:crypto');
const { mkdir, readFile, writeFile, rm } = require('node:fs/promises');
const path = require('node:path');

const collectionPath = path.resolve(__dirname, '../../tests/postman/CIS-API-funcional.postman_collection.json');
const xmlTokens = /<\?xml\s[^?]*\?>|<!--[\s\S]*?-->|<!\[CDATA\[[\s\S]*?\]\]>|<\/[A-Za-z_][\w.:-]*\s*>|<[A-Za-z_][\w.:-]*(?:\s+[A-Za-z_][\w.:-]*\s*=\s*(?:"[^"<]*"|'[^'<]*'))*\s*\/?>|[^<]+/gy;

function integer(value, name) {
  if (!Number.isSafeInteger(value) || value < 0) throw new Error(`Invalid Postman report counter: ${name}.`);
  return value;
}

// Parse only the XML subset produced by Postman's JUnit reporter. Reject DTDs,
// malformed structure and unknown entities; never resolve external resources.
function junitCounters(source) {
  const xml = source.replace(/^\uFEFF/, '');
  const tokens = new RegExp(xmlTokens.source, 'gy');
  const stack = [];
  let position = 0, root, cases = 0, declared = 0, failures = 0, skipped = 0, suites = 0;
  while (position < xml.length) {
    tokens.lastIndex = position;
    const match = tokens.exec(xml);
    if (!match) throw new Error('Malformed Postman JUnit report.');
    const token = match[0];
    position = tokens.lastIndex;
    if (token.startsWith('<?')) {
      if (match.index !== 0) throw new Error('Malformed Postman JUnit declaration.');
      continue;
    }
    if (token.startsWith('<!--')) continue;
    if (token.startsWith('<![CDATA[')) {
      if (!stack.length) throw new Error('JUnit CDATA outside root.');
      continue;
    }
    if (!token.startsWith('<')) {
      if ((!stack.length && token.trim()) || /&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;)/.test(token))
        throw new Error('Malformed Postman JUnit text.');
      continue;
    }
    if (token.startsWith('</')) {
      if (stack.pop() !== token.slice(2, -1).trim()) throw new Error('Mismatched Postman JUnit elements.');
      continue;
    }
    const name = /^<([\w.:-]+)/.exec(token)[1];
    const attributes = Object.create(null);
    for (const attribute of token.matchAll(/([\w.:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)) {
      if (Object.hasOwn(attributes, attribute[1])) throw new Error('Duplicate JUnit attribute.');
      attributes[attribute[1]] = attribute[2] ?? attribute[3];
      if (/&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;)/.test(attributes[attribute[1]]))
        throw new Error('Malformed Postman JUnit attribute.');
    }
    const count = key => {
      if (!/^\d+$/.test(attributes[key] ?? '')) throw new Error(`Missing JUnit ${key} counter.`);
      return integer(Number(attributes[key]), key);
    };
    if (!stack.length) {
      if (root || name !== 'testsuites') throw new Error('Expected one Postman JUnit testsuites root.');
      root = attributes;
      count('tests');
      for (const key of ['failures', 'errors']) if (attributes[key] !== undefined) failures += count(key);
      if (attributes.skipped !== undefined) skipped += count('skipped');
    }
    if (name === 'testsuite') {
      if (stack.at(-1) !== 'testsuites') throw new Error('Unexpected nested JUnit suite.');
      suites++;
      declared += count('tests');
      failures += count('failures') + count('errors');
      if (attributes.skipped !== undefined) skipped += count('skipped');
    }
    if (name === 'testcase') {
      if (stack.at(-1) !== 'testsuite') throw new Error('JUnit testcase outside suite.');
      cases++;
    }
    if (name === 'failure' || name === 'error') failures++;
    if (name === 'skipped') skipped++;
    if (!token.endsWith('/>')) stack.push(name);
  }
  // Postman CLI 1.74 reports request count at the root; child suites declare
  // assertion counts. JSON cross-checks these separately after parsing.
  if (!root || stack.length || cases === 0 || declared !== cases || Number(root.tests) !== suites)
    throw new Error('Missing, empty or inconsistent Postman JUnit report.');
  return { cases, suites, failures, skipped };
}

function redactJUnit(xml, sanitize) {
  const tokens = new RegExp(xmlTokens.source, 'g');
  const decode = value => value.replace(/&(amp|lt|gt|quot|apos);/g,
    (_, entity) => ({ amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" })[entity]);
  const encode = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;')
    .replaceAll("'", '&apos;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
  return xml.replace(tokens, token => {
    if (token.startsWith('<?')) return token;
    if (token.startsWith('<!--')) return `<!--${sanitize(token.slice(4, -3))}-->`;
    if (token.startsWith('<![CDATA[')) return `<![CDATA[${sanitize(token.slice(9, -3))}]]>`;
    if (!token.startsWith('<')) return encode(sanitize(decode(token)));
    // Preserve structural names and numeric counters even for short credentials
    // such as "a" or "1". Credentials can only occur in descriptive values.
    return token.replace(/([\w.:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g, (attribute, name, double, single) =>
      /^(tests|failures|errors|skipped|time|timestamp|id)$/.test(name)
        ? attribute : `${name}="${encode(sanitize(decode(double ?? single)))}"`);
  });
}

function execute(command, args) {
  const parts = Array.isArray(command) ? command : [command];
  if (!parts.length || parts.some(part => typeof part !== 'string' || !part)) throw new Error('Invalid Postman command.');
  return new Promise((resolve, reject) => {
    // Async spawn keeps the event loop available for the local Users fixture.
    // Do not forward CLI output: a failed assertion can contain credentials/JWTs.
    const child = spawn(parts[0], [...parts.slice(1), ...args], {
      stdio: ['ignore', 'ignore', 'ignore'], windowsHide: true, shell: false
    });
    child.once('error', error => reject(new Error(`Unable to start Postman CLI (${error.code || 'spawn error'}).`)));
    child.once('close', (code, signal) => resolve({ code, signal }));
  });
}

async function runPostman({ baseUrl, usersBaseUrl, login, password, outputDirectory, command = process.env.POSTMAN_CLI || 'postman' }) {
  for (const [name, value] of Object.entries({ baseUrl, usersBaseUrl, login, password, outputDirectory }))
    if (typeof value !== 'string' || !value) throw new Error(`Postman requires ${name}.`);
  for (const value of [baseUrl, usersBaseUrl]) {
    const url = new URL(value);
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password) throw new Error('Invalid Postman base URL.');
  }
  const directory = path.resolve(outputDirectory);
  await mkdir(directory, { recursive: true });
  const reportPath = path.join(directory, 'postman.junit.xml');
  const summaryPath = path.join(directory, 'summary.json');
  const suffix = randomUUID();
  const environmentPath = path.join(directory, `.postman-environment-${suffix}.json`);
  const rawReportPath = path.join(directory, `.postman-report-${suffix}.json`);
  const result = { requests: 0, assertions: 0, passed: 0, failed: 0, reportPath };
  const sanitize = text => {
    text = text.replace(/\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b/g, '[REDACTED_JWT]');
    for (const secret of [password, login].sort((a, b) => b.length - a.length)) {
      // Preserve XML validity even when credentials include XML metacharacters.
      const escaped = secret.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll("'", '&apos;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
      text = text.split(escaped).join('[REDACTED]').split(secret).join('[REDACTED]');
    }
    return text;
  };
  try {
    await rm(reportPath, { force: true });
    await rm(summaryPath, { force: true });
    // Only inputs belong in this environment. Token and ID outputs are set by
    // pm.collectionVariables and must not be shadowed by environment entries.
    await writeFile(environmentPath, JSON.stringify({
      id: suffix, name: 'CIS Phase 2 temporary local inputs', _postman_variable_scope: 'environment',
      values: Object.entries({ baseUrl, usersBaseUrl, login, password }).map(([key, value]) => ({ key, value, enabled: true, type: 'secret' }))
    }), { mode: 0o600, flag: 'wx' });
    const execution = await execute(command, [
      'collection', 'run', collectionPath, '-e', environmentPath, '--no-report-events',
      '-r', 'junit,json', '--reporter-junit-export', reportPath,
      '--reporter-json-export', rawReportPath, '--reporter-json-structure', 'newman',
      '--reporter-json-omitAllHeadersAndBody', '--timeout', '120000', '--timeout-request', '15000'
    ]);
    let junit;
    try {
      const xml = await readFile(reportPath, 'utf8');
      const sanitizedXml = redactJUnit(xml, sanitize);
      await writeFile(reportPath, sanitizedXml);
      junit = junitCounters(xml);
      junitCounters(sanitizedXml);
    } catch (error) {
      if (error.code === 'ENOENT') throw new Error('Postman did not produce the required JUnit report.');
      throw error;
    }
    let run;
    try { ({ run } = JSON.parse(await readFile(rawReportPath, 'utf8'))); }
    catch { throw new Error('Postman did not produce a valid JSON counters report.'); }
    const stats = run?.stats;
    result.requests = integer(stats?.requests?.total, 'requests.total');
    result.assertions = integer(stats?.assertions?.total, 'assertions.total');
    const assertionFailures = integer(stats?.assertions?.failed, 'assertions.failed');
    const requestFailures = integer(stats?.requests?.failed, 'requests.failed');
    const scriptFailures = integer(stats?.scripts?.failed, 'scripts.failed');
    const pending = integer(stats?.assertions?.pending, 'assertions.pending')
      + integer(stats?.requests?.pending, 'requests.pending');
    if (assertionFailures > result.assertions) throw new Error('Inconsistent Postman assertion counters.');
    result.passed = result.assertions - assertionFailures;
    result.failed = Math.max(assertionFailures, requestFailures, scriptFailures, junit.failures, run.failures?.length || 0);
    const collection = JSON.parse(await readFile(collectionPath, 'utf8'));
    const requestCount = items => items.reduce((sum, item) => sum + (item.request ? 1 : requestCount(item.item || [])), 0);
    const expected = requestCount(collection.item);
    if (result.requests !== expected || run.executions?.length !== expected || junit.suites !== expected || result.assertions < expected || junit.cases !== result.assertions)
      throw new Error(`Incomplete Postman run: expected all ${expected} requests and their assertions.`);
    if (pending || junit.skipped || run.error || result.failed)
      throw new Error('Postman reported failed, skipped or pending checks.');
    if (execution.code !== 0 || execution.signal) throw new Error('Postman CLI exited unsuccessfully.');
    await writeFile(summaryPath, JSON.stringify({ status: 'passed', ...result }, null, 2) + '\n');
    return result;
  } catch (error) {
    await writeFile(summaryPath, JSON.stringify({ status: 'failed', ...result, error: sanitize(error.message) }, null, 2) + '\n');
    throw new Error(`Postman validation failed: ${sanitize(error.message)}`);
  } finally {
    await rm(environmentPath, { force: true });
    await rm(rawReportPath, { force: true });
  }
}

module.exports = { runPostman };
