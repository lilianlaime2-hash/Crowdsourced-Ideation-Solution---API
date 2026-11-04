const assert = require('node:assert/strict');
const { test } = require('node:test');
const { parseArguments } = require('../../scripts/phase2/run-workflow.cjs');

test('required-check workflow accepts migration/functional modes and rejects excluded performance options', () => {
    assert.equal(parseArguments([]).mode, 'full');
    assert.equal(parseArguments(['--mode', 'migration']).mode, 'migration');
    assert.equal(parseArguments(['--mode', 'functional']).mode, 'functional');
    assert.throws(() => parseArguments(['--mode', 'performance']));
    assert.throws(() => parseArguments(['--profile', 'burst']));
    for (const args of [['--mode', 'unknown'], ['--profile', 'unknown'], ['--duration', '0s'],
        ['--duration', 'abc'], ['--vus', '0'], ['--vus', '1.5'], ['--unknown', 'x'], ['--mode']]) {
        assert.throws(() => parseArguments(args));
    }
});
