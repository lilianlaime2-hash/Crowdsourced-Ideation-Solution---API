import http from 'k6/http';
import { Counter } from 'k6/metrics';
import { performanceOptions } from './helpers/config.js';
import { config, login, request, parameters, isId, pause } from './helpers/api.js';

const operations = ['create_topic', 'update_topic', 'create_idea', 'update_idea',
    'create_vote', 'delete_vote', 'delete_idea', 'delete_topic'];
const cleanupFailures = new Counter('cleanup_failures');
export const options = performanceOptions(__ENV, operations);
options.thresholds.cleanup_failures = ['count==0'];

export function setup() { return login(); }

function cleanup(path, session) {
    try {
        const response = http.del(config.cisUrl + path, null, parameters(session.token, 'cleanup', 'cleanup'));
        if (![204, 404].includes(response.status)) {
            cleanupFailures.add(1);
            console.error(`Limpieza pendiente: DELETE ${path}, HTTP ${response.status}`);
        }
    } catch (_) {
        cleanupFailures.add(1);
        console.error(`Limpieza pendiente: DELETE ${path}`);
    }
}

export default function (session) {
    let topicId;
    let ideaId;
    cleanupFailures.add(0);
    const suffix = `${Date.now()}-${__VU}-${__ITER}`;
    const title = `K6 topic ${suffix}`;
    try {
        // Each iteration owns its records; no shared seeded topic/idea is updated.
        const topic = request('POST', '/api/topics', session, 'create_topic', 201,
            { title, description: 'Temporary topic created by the k6 CRUD workload.', tags: [] },
            data => data !== null && isId(data.id) && data.title === title);
        topicId = topic.id;
        request('PUT', `/api/topics/${topicId}`, session, 'update_topic', 200,
            { title: `${title} updated`, description: 'Updated by k6.' },
            data => data !== null && data.id === topicId && data.title === `${title} updated`);

        const ideaTitle = `K6 idea ${suffix}`;
        const idea = request('POST', `/api/topics/${topicId}/ideas`, session, 'create_idea', 201,
            { title: ideaTitle, description: 'Temporary idea created by the k6 CRUD workload.' },
            data => data !== null && isId(data.id) && data.topicId === topicId && data.title === ideaTitle);
        ideaId = idea.id;
        request('PUT', `/api/ideas/${ideaId}`, session, 'update_idea', 200,
            { title: `${ideaTitle} updated`, description: 'Updated by k6.' },
            data => data !== null && data.id === ideaId && data.title === `${ideaTitle} updated`);

        request('POST', `/api/topics/${topicId}/ideas/${ideaId}/votes`, session, 'create_vote', 201,
            undefined, data => data !== null && isId(data.id) && data.ideaId === ideaId);
        request('DELETE', `/api/topics/${topicId}/ideas/${ideaId}/votes`, session, 'delete_vote', 204);
        request('DELETE', `/api/topics/${topicId}/ideas/${ideaId}`, session, 'delete_idea', 204);
        ideaId = undefined;
        request('DELETE', `/api/topics/${topicId}`, session, 'delete_topic', 204);
        topicId = undefined;
    } finally {
        // Best effort after a failed step. All targets were created by this iteration.
        // A failed/malformed POST response may still represent a committed vote.
        if (topicId && ideaId) cleanup(`/api/topics/${topicId}/ideas/${ideaId}/votes`, session);
        if (topicId && ideaId) cleanup(`/api/topics/${topicId}/ideas/${ideaId}`, session);
        if (topicId) cleanup(`/api/topics/${topicId}`, session);
    }
    pause();
}
