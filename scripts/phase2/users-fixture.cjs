'use strict';

const { createServer } = require('node:http');
const { createHmac } = require('node:crypto');

// These are fixed fixture identities; no developer or production secret is read.
const fixtureUsers = [
    { id: '0149d2c8-2bb3-4fb3-a9df-08d252fa6e8c', name: 'Fixture Adrian', login: 'AdrianCis' },
    { id: '2820efe4-a386-4b2f-9fef-c403a1686ea3', name: 'Fixture ARuiz', login: 'ARuiz' },
    { id: 'db297278-a905-4ce8-a78e-18e79e0b83db', name: 'Fixture Adrian Ruiz', login: 'AdrianRuiz' },
    { id: '5aa675c4-5c08-4331-8159-4e7675bd02cf', name: 'Fixture Roca', login: 'jroca2' }
];
const fixturePassword = 'phase2-test-password';
const fixtureSigningKey = 'cis-phase2-fixture-signing-key-only-never-production';

function signToken(user) {
    const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
    const content = encode({ alg: 'HS256', typ: 'JWT' }) + '.' +
        encode({ sub: user.id, exp: Math.floor(Date.now() / 1000) + 3600 });
    return content + '.' + createHmac('sha256', fixtureSigningKey).update(content).digest('base64url');
}

async function startUsersFixture() {
    const respond = (response, status, body) => {
        response.writeHead(status, { 'Content-Type': 'application/json' });
        response.end(JSON.stringify(body));
    };
    const server = createServer(async (request, response) => {
        try {
            if (request.method === 'POST' && request.url === '/auth/login') {
                let body = '';
                for await (const chunk of request) {
                    body += chunk;
                    if (body.length > 8192) return respond(response, 413, { message: 'Fixture request too large' });
                }
                const credentials = JSON.parse(body);
                const user = fixtureUsers.find(candidate => candidate.login === credentials?.login);
                return user && credentials.password === fixturePassword
                    ? respond(response, 200, { ...user, token: signToken(user) })
                    : respond(response, 401, { message: 'Invalid fixture credentials' });
            }
            const user = fixtureUsers.find(candidate => request.url === '/users/' + candidate.id);
            if (request.method === 'GET' && user) return respond(response, 200, user);
            respond(response, 404, { message: 'Fixture resource not found' });
        } catch (_) { respond(response, 400, { message: 'Malformed fixture request' }); }
    });
    await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(0, '127.0.0.1', resolve);
    });
    return { url: `http://127.0.0.1:${server.address().port}`,
        stop: () => new Promise(resolve => server.close(resolve)) };
}

module.exports = { startUsersFixture, fixtureUsers, fixturePassword, fixtureSigningKey, signToken };
