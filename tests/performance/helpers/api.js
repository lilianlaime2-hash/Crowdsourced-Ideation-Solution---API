import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { configuration } from './config.js';

export const config = configuration(__ENV);

export function json(response) {
    try { return response.json(); } catch (_) { return null; }
}

export function isId(value) {
    return Number.isSafeInteger(value) && value > 0;
}

export function isPage(value) {
    return value !== null && Array.isArray(value.items) &&
        Number.isSafeInteger(value.totalItems) && value.totalItems >= value.items.length &&
        value.page === 1 && value.pageSize === config.pageSize &&
        value.items.length <= config.pageSize &&
        value.totalPages === Math.ceil(value.totalItems / config.pageSize);
}

export function parameters(token, endpoint, phase = 'measure', withJson = false) {
    const headers = { Authorization: `Bearer ${token}`, Accept: 'application/json' };
    if (withJson) headers['Content-Type'] = 'application/json';
    return { headers, timeout: '10s', redirects: 0,
        tags: { phase, endpoint, name: endpoint } };
}

export function login() {
    if (__ENV.K6_TOKEN) return { token: __ENV.K6_TOKEN };
    if (!__ENV.K6_LOGIN || !__ENV.K6_PASSWORD) {
        throw new Error('Configura K6_LOGIN y K6_PASSWORD, o proporciona K6_TOKEN.');
    }
    const response = http.post(`${config.usersUrl}/auth/login`, JSON.stringify({
        login: __ENV.K6_LOGIN, password: __ENV.K6_PASSWORD
    }), { headers: { 'Content-Type': 'application/json' }, timeout: '10s', redirects: 0,
        tags: { phase: 'auth', endpoint: 'login', name: 'login' } });
    const data = json(response);
    if (response.status !== 200 || !data || typeof data.token !== 'string' || !data.token) {
        throw new Error(`Login fallido: HTTP ${response.status}. Verifica Users API y las credenciales.`);
    }
    return { token: data.token };
}

export function setupRead(operation) {
    const session = login();
    if (!['get_topic', 'get_ideas'].includes(operation)) return session;
    if (config.topicId) return { ...session, topicId: config.topicId };

    // Discover IDs from real responses rather than assuming titles/IDs from a seed.
    const path = operation === 'get_ideas'
        ? '/api/statistics/topics/most-active?page=1&pageSize=1'
        : '/api/topics?page=1&pageSize=1';
    const response = http.get(config.cisUrl + path, parameters(session.token, 'discover_topic', 'prepare'));
    const data = json(response);
    const first = data && Array.isArray(data.items) ? data.items[0] : null;
    const topicId = first && (operation === 'get_ideas' ? first.topicId : first.id);
    if (response.status !== 200 || !isId(topicId) ||
        (operation === 'get_ideas' && !(first.ideaCount > 0))) {
        throw new Error('No se encontro un tema utilizable. Configura PERF_TOPIC_ID con un tema existente con ideas.');
    }
    return { ...session, topicId };
}

export function request(method, path, session, endpoint, status, body, validateBody) {
    const response = http.request(method, config.cisUrl + path,
        body === undefined ? null : JSON.stringify(body), parameters(session.token, endpoint, 'measure', body !== undefined));
    const data = status === 204 ? null : json(response);
    let validData = false;
    try {
        validData = validateBody ? Boolean(validateBody(data)) : status === 204 || data !== null;
    } catch (_) {
        // Malformed JSON shapes must fail checks, even when HTTP status is successful.
    }
    const accepted = check(response, {
        [`${endpoint}: HTTP ${status}`]: r => r.status === status,
        [`${endpoint}: contenido correcto`]: r => r.status === status && validData
    }, { phase: 'measure', endpoint });
    if (!accepted) fail(`${endpoint} fallo: HTTP ${response.status}.`);
    return data;
}

export function pause() {
    if (config.sleep > 0) sleep(config.sleep);
}

export function read(operation, session) {
    const page = `page=1&pageSize=${config.pageSize}`;
    switch (operation) {
        case 'get_topics':
            request('GET', `/api/topics?${page}`, session, operation, 200, undefined,
                data => isPage(data) && data.items.every(item => isId(item.id) && typeof item.title === 'string'));
            break;
        case 'get_topic':
            request('GET', `/api/topics/${session.topicId}`, session, operation, 200, undefined,
                data => data !== null && data.id === session.topicId && typeof data.title === 'string');
            break;
        case 'get_ideas':
            request('GET', `/api/topics/${session.topicId}/ideas?${page}`, session, operation, 200, undefined,
                data => isPage(data) && data.items.every(item => isId(item.id) && item.topicId === session.topicId));
            break;
        case 'get_most_voted':
            request('GET', `/api/statistics/ideas/most-voted?${page}`, session, operation, 200, undefined,
                data => isPage(data) && data.items.every((item, index, items) =>
                    isId(item.ideaId) && Number.isSafeInteger(item.voteCount) && item.voteCount >= 0 &&
                    (index === 0 || items[index - 1].voteCount >= item.voteCount)));
            break;
        case 'get_most_active':
            request('GET', `/api/statistics/topics/most-active?${page}`, session, operation, 200, undefined,
                data => isPage(data) && data.items.every((item, index, items) =>
                    isId(item.topicId) && Number.isSafeInteger(item.ideaCount) && item.ideaCount >= 0 &&
                    Number.isSafeInteger(item.voteCount) && item.voteCount >= 0 &&
                    item.activityScore === item.ideaCount + item.voteCount &&
                    (index === 0 || items[index - 1].activityScore >= item.activityScore)));
            break;
        default: throw new Error(`Operacion de lectura desconocida: ${operation}`);
    }
    pause();
}
