#!/usr/bin/env node
'use strict';

const { readFileSync } = require('node:fs');

// Validate the XML structure before trusting its root counters. This deliberately
// accepts the XML subset emitted by Coverlet/ReportGenerator; no entities or DTDs
// are resolved, and unsupported/malformed input fails closed.
function coverageAttributes(xml) {
  const stack = [];
  let root;
  let position = 0;
  const tokens = /<\?xml\s[^?]*\?>|<!--[\s\S]*?-->|<!DOCTYPE\s+coverage\s+SYSTEM\s+(?:"[^"]*"|'[^']*')\s*>|<\/[A-Za-z_][\w.:-]*\s*>|<[A-Za-z_][\w.:-]*(?:\s+[A-Za-z_][\w.:-]*\s*=\s*(?:"[^"<]*"|'[^'<]*'))*\s*\/?>|[^<]+/gy;
  while (position < xml.length) {
    tokens.lastIndex = position;
    const match = tokens.exec(xml);
    if (!match) throw new Error('Malformed or unsupported coverage XML.');
    const token = match[0];
    position = tokens.lastIndex;
    if (token.startsWith('<?')) {
      if (match.index !== 0) throw new Error('XML declaration must appear at the beginning.');
      continue;
    }
    if (token.startsWith('<!--')) {
      if (token.slice(4, -3).includes('--')) throw new Error('Malformed XML comment.');
      continue;
    }
    if (token.startsWith('<!')) {
      if (root) throw new Error('DOCTYPE must precede the XML root.');
      continue;
    }
    if (!token.startsWith('<')) {
      if (!stack.length && token.trim()) throw new Error('Text outside XML root.');
      if (/&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;)/.test(token)) throw new Error('Invalid XML entity.');
      continue;
    }
    if (token.startsWith('</')) {
      const name = token.slice(2, -1).trim();
      if (stack.pop() !== name) throw new Error('Mismatched coverage XML elements.');
      continue;
    }
    const name = /^<([\w.:-]+)/.exec(token)[1];
    const attributes = Object.create(null);
    for (const attribute of token.matchAll(/([\w.:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)) {
      if (Object.hasOwn(attributes, attribute[1])) throw new Error('Duplicate XML attribute.');
      attributes[attribute[1]] = attribute[2] ?? attribute[3];
      if (/&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;)/.test(attributes[attribute[1]])) throw new Error('Invalid XML attribute entity.');
    }
    if (!stack.length) {
      if (root || name !== 'coverage') throw new Error('Expected a single coverage XML root.');
      root = attributes;
    }
    if (!token.endsWith('/>')) stack.push(name);
  }
  if (!root || stack.length) throw new Error('Missing or unclosed coverage XML root.');
  return root;
}

function counter(attributes, name) {
  const value = attributes[name];
  if (typeof value !== 'string' || !/^\d+$/.test(value) || !Number.isSafeInteger(Number(value))) {
    throw new Error(`Invalid or missing coverage counter: ${name}.`);
  }
  return Number(value);
}

try {
  const [path, suppliedThreshold = '80', ...extra] = process.argv.slice(2);
  if (!path || extra.length) throw new Error('Usage: node scripts/coverage/check-coverage.cjs <coverage.cobertura.xml> [threshold]');
  if (!/^(?:\d+(?:\.\d+)?|\.\d+)$/.test(suppliedThreshold)) throw new Error('Coverage threshold must be a finite percentage between 0 and 100.');
  const threshold = Number(suppliedThreshold);
  if (!Number.isFinite(threshold) || threshold < 0 || threshold > 100) throw new Error('Coverage threshold must be between 0 and 100.');
  const attributes = coverageAttributes(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
  const covered = counter(attributes, 'lines-covered');
  const valid = counter(attributes, 'lines-valid');
  const branchesCovered = counter(attributes, 'branches-covered');
  const branchesValid = counter(attributes, 'branches-valid');
  if (valid === 0 || covered > valid || branchesCovered > branchesValid) throw new Error('Coverage counters must describe a nonempty report and covered cannot exceed valid.');
  const percentage = covered / valid * 100;
  console.log(`Lines: ${covered}/${valid} (${percentage.toFixed(2)}%); required: ${threshold}%.`);
  console.log(`Line coverage: ${percentage.toFixed(2)}%`);
  console.log(`Branches: ${branchesCovered}/${branchesValid} (${branchesValid ? (branchesCovered / branchesValid * 100).toFixed(2) + '%' : 'n/a'}); informational.`);
  if (percentage < threshold) throw new Error('Line coverage is below the required threshold.');
} catch (error) {
  console.error(`Coverage gate failed: ${error.message}`);
  process.exitCode = 1;
}
