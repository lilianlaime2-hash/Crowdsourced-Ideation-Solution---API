export function positiveInteger(value, fallback, label, maximum = Number.MAX_SAFE_INTEGER) {
    const number = value === undefined || value === '' ? fallback : Number(value);
    if (!Number.isSafeInteger(number) || number < 1 || number > maximum) {
        throw new Error(`${label} debe ser un entero entre 1 y ${maximum}.`);
    }
    return number;
}

export function configuration(env) {
    const profile = env.PERF_PROFILE || 'load';
    if (!['smoke', 'load', 'burst'].includes(profile)) {
        throw new Error('PERF_PROFILE debe ser smoke, load o burst.');
    }
    const vus = positiveInteger(env.PERF_VUS, 50, 'PERF_VUS');
    const sleep = env.PERF_SLEEP === undefined || env.PERF_SLEEP === ''
        ? (profile === 'load' ? 1 : 0) : Number(env.PERF_SLEEP);
    if (!Number.isFinite(sleep) || sleep < 0) throw new Error('PERF_SLEEP debe ser mayor o igual a 0.');
    const result = {
        cisUrl: (env.CIS_BASE_URL || 'http://localhost:5039').replace(/\/+$/, ''),
        usersUrl: (env.USERS_BASE_URL || 'http://localhost:8080').replace(/\/+$/, ''),
        profile, vus, sleep,
        pageSize: positiveInteger(env.PERF_PAGE_SIZE, 10, 'PERF_PAGE_SIZE', 100),
        p95: positiveInteger(env.PERF_P95_MS, 2000, 'PERF_P95_MS'),
        duration: env.PERF_DURATION || '30s'
    };
    if (env.PERF_TOPIC_ID) result.topicId = positiveInteger(env.PERF_TOPIC_ID, undefined, 'PERF_TOPIC_ID');
    return result;
}

export function performanceOptions(env, operations) {
    const config = configuration(env);
    const scenario = config.profile === 'load'
        ? { executor: 'constant-vus', vus: config.vus, duration: config.duration, gracefulStop: '2m' }
        : { executor: 'per-vu-iterations', vus: config.profile === 'smoke' ? 1 : config.vus,
            iterations: 1, maxDuration: '3m', gracefulStop: '2m' };
    const thresholds = {
        'http_reqs{phase:measure}': ['count>0'],
        'checks{phase:measure}': ['rate>0.99'],
        'http_req_failed{phase:measure}': ['rate<0.01']
    };
    for (const operation of operations) {
        thresholds[`http_reqs{phase:measure,endpoint:${operation}}`] = ['count>0'];
        thresholds[`http_req_duration{phase:measure,endpoint:${operation}}`] = [`p(95)<${config.p95}`];
    }
    return { scenarios: { workload: scenario }, thresholds,
        summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'] };
}
