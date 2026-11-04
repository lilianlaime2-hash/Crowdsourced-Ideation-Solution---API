const assert = require('node:assert/strict');
const { test } = require('node:test');
const { createHmac } = require('node:crypto');
const { startUsersFixture, fixtureUsers, fixturePassword, fixtureSigningKey } = require('../../scripts/phase2/users-fixture.cjs');

test('fixture auth checks credentials and signs a usable HS256 UUID token', async () => {
    const service = await startUsersFixture();
    try {
        const login = async body => fetch(service.url + '/auth/login', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
        });
        assert.equal((await login({ login: fixtureUsers[0].login, password: 'wrong' })).status, 401);
        assert.equal((await login({ login: 'missing', password: fixturePassword })).status, 401);
        const response = await login({ login: fixtureUsers[0].login, password: fixturePassword });
        assert.equal(response.status, 200);
        const body = await response.json();
        const [header, payload, signature] = body.token.split('.');
        assert.equal(JSON.parse(Buffer.from(header, 'base64url')).alg, 'HS256');
        assert.equal(JSON.parse(Buffer.from(payload, 'base64url')).sub, fixtureUsers[0].id);
        assert.equal(signature, createHmac('sha256', fixtureSigningKey).update(header + '.' + payload).digest('base64url'));
        assert.equal(body.id, fixtureUsers[0].id);
        const profile = await fetch(service.url + '/users/' + fixtureUsers[1].id);
        assert.equal(profile.status, 200);
        assert.equal((await profile.json()).login, fixtureUsers[1].login);
        assert.equal((await fetch(service.url + '/users/missing')).status, 404);
        const malformed = await fetch(service.url + '/auth/login', { method: 'POST', body: '{' });
        assert.equal(malformed.status, 400);
    } finally { await service.stop(); }
});
