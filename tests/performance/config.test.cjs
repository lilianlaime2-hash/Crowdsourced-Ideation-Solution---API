const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const path = require('node:path');
const { test, before } = require('node:test');

let configuration, performanceOptions;
before(async () => {
    const source = readFileSync(path.join(__dirname, 'helpers/config.js'), 'utf8');
    ({ configuration, performanceOptions } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`));
});

test('load defaults to 50 VUs for 30s, with endpoint-specific measurement thresholds', () => {
    const options = performanceOptions({}, ['get_topics']);
    assert.equal(options.scenarios.workload.executor, 'constant-vus');
    assert.equal(options.scenarios.workload.vus, 50);
    assert.equal(options.scenarios.workload.duration, '30s');
    assert.deepEqual(options.thresholds['http_req_duration{phase:measure,endpoint:get_topics}'], ['p(95)<2000']);
    assert.deepEqual(options.thresholds['http_reqs{phase:measure}'], ['count>0']);
    assert.deepEqual(options.thresholds['http_req_failed{phase:measure}'], ['rate<0.01']);
});

test('burst runs exactly one iteration for each of 50 VUs with no default pause', () => {
    const env = { PERF_PROFILE: 'burst' };
    const scenario = performanceOptions(env, ['get_topic']).scenarios.workload;
    assert.equal(scenario.executor, 'per-vu-iterations');
    assert.equal(scenario.vus, 50);
    assert.equal(scenario.iterations, 1);
    assert.equal(configuration(env).sleep, 0);
});

test('smoke runs one VU and one iteration', () => {
    const scenario = performanceOptions({ PERF_PROFILE: 'smoke' }, ['get_topic']).scenarios.workload;
    assert.equal(scenario.vus, 1);
    assert.equal(scenario.iterations, 1);
});

test('allows configured load, pagination and latency thresholds', () => {
    const env = { PERF_VUS: '12', PERF_DURATION: '15s', PERF_PAGE_SIZE: '50', PERF_P95_MS: '750', PERF_SLEEP: '0.5' };
    const config = configuration(env);
    assert.equal(config.pageSize, 50);
    assert.equal(config.sleep, 0.5);
    const options = performanceOptions(env, ['get_ideas']);
    assert.equal(options.scenarios.workload.vus, 12);
    assert.equal(options.scenarios.workload.duration, '15s');
    assert.deepEqual(options.thresholds['http_req_duration{phase:measure,endpoint:get_ideas}'], ['p(95)<750']);
});

test('rejects invalid profiles, VUs, pagination, IDs and pauses', () => {
    for (const env of [
        { PERF_PROFILE: 'typo' }, { PERF_VUS: '0' }, { PERF_VUS: '3.5' }, { PERF_VUS: 'abc' },
        { PERF_PAGE_SIZE: '101' }, { PERF_PAGE_SIZE: '-1' }, { PERF_TOPIC_ID: '0' },
        { PERF_P95_MS: '0' }, { PERF_SLEEP: '-1' }, { PERF_SLEEP: 'abc' }
    ]) assert.throws(() => configuration(env));
});

test('normalizes base URL trailing slashes and retains an explicit topic ID', () => {
    const config = configuration({ CIS_BASE_URL: 'http://localhost:5039///',
        USERS_BASE_URL: 'http://localhost:8080/', PERF_TOPIC_ID: '2' });
    assert.equal(config.cisUrl, 'http://localhost:5039');
    assert.equal(config.usersUrl, 'http://localhost:8080');
    assert.equal(config.topicId, 2);
});
