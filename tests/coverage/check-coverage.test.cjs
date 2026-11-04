const { test } = require('node:test');
const assert = require('node:assert/strict');
const { mkdtempSync, writeFileSync, rmSync } = require('node:fs');
const { tmpdir } = require('node:os');
const { join, resolve } = require('node:path');
const { spawnSync } = require('node:child_process');

const gate = resolve(__dirname, '../../scripts/coverage/check-coverage.cjs');
const report = (covered = '80', valid = '100', extra = '') =>
  `<?xml version="1.0"?><coverage lines-covered="${covered}" lines-valid="${valid}" branches-covered="3" branches-valid="5" ${extra}><packages/></coverage>`;

function run(xml, threshold) {
  const directory = mkdtempSync(join(tmpdir(), 'cis-coverage-'));
  try {
    const path = join(directory, 'coverage.xml');
    if (xml !== null) writeFileSync(path, xml);
    return spawnSync(process.execPath, [gate, path, ...(threshold === undefined ? [] : [threshold])], { encoding: 'utf8' });
  } finally { rmSync(directory, { recursive: true, force: true }); }
}

test('passes at exact default threshold and reports both line counts and branches', () => {
  const result = run(report());
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /80\/100.*80\.00%/);
  assert.match(result.stdout, /^Line coverage: 80\.00%$/m);
  assert.match(result.stdout, /3\/5.*60\.00%/);
});
test('uses counts instead of a misleading root line-rate', () => {
  assert.equal(run(report('79', '100', 'line-rate="1"')).status, 1);
});
test('compares full precision without rounding a failing result up', () => {
  assert.equal(run(report('79999', '100000')).status, 1);
});
test('supports a configured percentage threshold', () => {
  assert.equal(run(report('81'), '81').status, 0);
  assert.equal(run(report('81'), '81.01').status, 1);
});
for (const threshold of ['', '-1', '101', 'NaN', 'Infinity', '0x50', '80percent']) {
  test(`rejects invalid threshold ${JSON.stringify(threshold)}`, () => {
    const result = run(report(), threshold);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /threshold/i);
  });
}
for (const [name, xml] of [
  ['missing file', null], ['empty report', ''], ['zero valid lines', report('0', '0')],
  ['NaN count', report('NaN')], ['negative count', report('-1')],
  ['fractional count', report('80.5')], ['covered exceeds valid', report('101')],
  ['missing count', '<coverage lines-valid="100"/>'],
  ['wrong root', '<packages lines-covered="80" lines-valid="100"/>'],
  ['unclosed root', report().replace('</coverage>', '')],
  ['mismatched nesting', report().replace('<packages/>', '<packages></classes>')],
  ['trailing second root', report() + '<extra/>'],
  ['unquoted attribute', report().replace('"80"', '80')],
  ['invalid branch count', report().replace('branches-valid="5"', 'branches-valid="2"')],
  ['invalid attribute entity', report('80', '100', 'version="&bogus;"')],
  ['declaration inside root', report().replace('<packages/>', '<?xml version="1.0"?><packages/>')],
  ['invalid comment', report().replace('<packages/>', '<!-- illegal -- comment --><packages/>')],
]) {
  test(`fails closed for ${name}`, () => {
    const result = run(xml);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Coverage gate failed:/);
  });
}
test('reports no branch opportunities without applying a branch gate', () => {
  const result = run(report().replace('branches-covered="3" branches-valid="5"', 'branches-covered="0" branches-valid="0"'));
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /0\/0.*n\/a/);
});
