const assert = require('node:assert/strict');
const { execFileSync, spawnSync } = require('node:child_process');
const { randomUUID, createHash } = require('node:crypto');
const { readFileSync } = require('node:fs');
const path = require('node:path');
const { test, before, after, beforeEach } = require('node:test');

const container = `cis-post-migration-verify-${randomUUID()}`;
const database = 'sd3db_mongo_perf';
const script = readFileSync(path.join(__dirname, '../../scripts/migration/post-migration-sd3-perf.js'), 'utf8');

function evaluate(code, target = database) {
    return execFileSync('docker', ['exec', container, 'mongosh', target, '--quiet', '--eval', code], {
        encoding: 'utf8', maxBuffer: 16 * 1024 * 1024, stdio: ['pipe', 'pipe', 'pipe']
    }).trim();
}

function run(target = database) {
    return spawnSync('docker', ['exec', '-i', container, 'mongosh', target, '--quiet', '--eval', script], {
        encoding: 'utf8', maxBuffer: 16 * 1024 * 1024
    });
}

function snapshot(target = database) {
    const data = evaluate(`EJSON.stringify(db.getCollectionNames().sort().map(name => ({
        name, documents: db.getCollection(name).find().sort({_id:1}).toArray(),
        indexes: db.getCollection(name).getIndexes()
    })))`, target);
    return createHash('sha256').update(data).digest('hex');
}

function rejectsWithoutWrites(pattern, target = database) {
    const beforeState = snapshot(target);
    const result = run(target);
    assert.equal(result.status, 1, result.stdout + result.stderr);
    assert.match(result.stdout + result.stderr, pattern);
    assert.equal(snapshot(target), beforeState, 'Validation failure changed documents or indexes');
}

before(async () => {
    execFileSync('docker', ['run', '-d', '--rm', '--network', 'none', '--name', container, 'mongo:8.0'], {
        stdio: 'pipe'
    });
    const deadline = Date.now() + 30000;
    while (true) {
        try {
            evaluate('db.runCommand({ping:1})');
            break;
        } catch (error) {
            if (Date.now() >= deadline) throw error;
            await new Promise(resolve => setTimeout(resolve, 250));
        }
    }
});

after(() => {
    execFileSync('docker', ['rm', '-f', container], { stdio: 'pipe' });
});

beforeEach(() => {
    // Controlled data, independent of the script's constants and of application databases.
    evaluate(`
        db.dropDatabase();
        const ids = [
            '0149d2c8-2bb3-4fb3-a9df-08d252fa6e8c',
            '2820efe4-a386-4b2f-9fef-c403a1686ea3',
            'db297278-a905-4ce8-a78e-18e79e0b83db',
            '5aa675c4-5c08-4331-8159-4e7675bd02cf'
        ];
        const logins = ['AdrianCis','ARuiz','AdrianRuiz','jroca2'];
        db.users.insertMany(ids.map((_id,n) => ({_id, login:logins[n]})));
        db.tags.insertMany(Array.from({length:200},(_,n) => ({
            _id:NumberLong(String(n+1)), name:'Tag '+(n+1), normalizedName:'TAG '+(n+1)
        })));
        const topics = [];
        const ideas = [];
        for (let n=1; n<=500; n++) {
            const author = n<=350 ? 0 : n<=400 ? 1 : n<=450 ? 2 : 3;
            const tagCount = n<=100 ? 0 : n<=300 ? 4 : 5;
            topics.push({_id:NumberLong(String(n)), userId:ids[author],
                tagIds:Array.from({length:tagCount},(_,j)=>NumberLong(String((n+j)%200+1)))});
            const ideaCount = n<=100 ? 0 : n<=200 ? 1 : n<=300 ? 2 : n<=400 ? 5 : 12;
            for(let j=0;j<ideaCount;j++) {
                const id=ideas.length+1;
                const ideaAuthor=id<=1400?0:id<=1600?1:id<=1800?2:3;
                ideas.push({_id:NumberLong(String(id)),topicId:NumberLong(String(n)),userId:ids[ideaAuthor]});
            }
        }
        db.topics.insertMany(topics);
        db.ideas.insertMany(ideas);
        const votes=[];
        function vote(idea,user) {
            votes.push({_id:NumberLong(String(votes.length+1)),ideaId:NumberLong(String(idea)),userId:ids[user]});
        }
        for(let idea=201;idea<=2000;idea++) {
            vote(idea,0);
            if(idea<=400) continue;
            if(idea<=800) vote(idea,1+(idea-401)%3);
            else if(idea<=1600) {
                const omitted=(idea-801+2)%3;
                for(let user=0;user<3;user++) if(user!==omitted) vote(idea,user+1);
            } else for(let user=1;user<4;user++) vote(idea,user);
        }
        db.votes.insertMany(votes);
        db.counters.insertOne({_id:'topics',seq:NumberLong('600')});
    `);
});

test('accepts the four-user dataset and creates indexes/counters without lowering an existing counter', () => {
    const result = run();
    assert.equal(result.status, 0, result.stdout + result.stderr);
    assert.match(result.stdout, /POSTMIGRACION COMPLETADA/);
    assert.equal(evaluate(`JSON.stringify(db.counters.find().sort({_id:1}).toArray().map(c=>[c._id,c.seq.toString()]))`),
        '[["ideas","2000"],["tags","200"],["topics","600"],["votes","5000"]]');
    assert.equal(evaluate(`db.tags.getIndexes().some(i=>i.name==='ux_tags_normalizedName'&&i.unique===true)`), 'true');
    assert.equal(evaluate(`db.votes.getIndexes().some(i=>i.name==='ux_votes_ideaId_userId'&&i.unique===true)`), 'true');
    const state = snapshot();
    assert.equal(run().status, 0);
    assert.equal(snapshot(), state, 'Repeated execution changed the dataset');
});

test('rejects a different target database before writes', () => {
    rejectsWithoutWrites(/Base incorrecta/, 'unexpected_database');
});

test('rejects an additional synthetic account', () => {
    evaluate(`db.users.insertOne({_id:'99999999-9999-4999-8999-999999999999',login:'perfuser01'})`);
    rejectsWithoutWrites(/users: esperado 4, encontrado 5/);
});

test('rejects a synthetic account substituted for one of the four original accounts', () => {
    evaluate(`db.users.updateOne({login:'jroca2'},{$set:{login:'perfuser01'}})`);
    rejectsWithoutWrites(/Usuario no permitido: perfuser01/);
});

test('rejects an invalid user UUID', () => {
    evaluate(`db.users.deleteOne({login:'jroca2'});db.users.insertOne({_id:'invalid-id',login:'jroca2'})`);
    rejectsWithoutWrites(/UUID de usuario invalido/);
});

test('rejects a changed author quota even when total documents and relationships are valid', () => {
    evaluate(`db.topics.updateOne({_id:NumberLong('1')},{$set:{userId:'2820efe4-a386-4b2f-9fef-c403a1686ea3'}})`);
    rejectsWithoutWrites(/AdrianCis: topics esperado 350, encontrado 349/);
});

test('rejects inconsistent tag normalization without silently repairing migrated data', () => {
    evaluate(`db.tags.updateOne({_id:NumberLong('1')},{$set:{normalizedName:'PERF_TAG_001'}})`);
    rejectsWithoutWrites(/Etiqueta 1: normalizacion inconsistente/);
});

test('rejects duplicate votes before creating indexes or updating counters', () => {
    evaluate(`const first=db.votes.findOne({_id:NumberLong('1')}); db.votes.updateOne({_id:NumberLong('2')},{$set:{ideaId:first.ideaId,userId:first.userId}})`);
    rejectsWithoutWrites(/Voto duplicado/);
});

test('rejects orphan ideas before writes', () => {
    evaluate(`db.ideas.updateOne({_id:NumberLong('1')},{$set:{topicId:NumberLong('999999')}})`);
    rejectsWithoutWrites(/Idea 1: topico inexistente/);
});

test('rejects IDs stored with a BSON type different from Long', () => {
    evaluate(`db.votes.updateOne({_id:NumberLong('1')},{$set:{ideaId:'201'}})`);
    rejectsWithoutWrites(/votes.ideaId debe ser BSON Long/);
});

test('rejects a malformed counter before creating any index', () => {
    evaluate(`db.counters.updateOne({_id:'topics'},{$set:{seq:'600'}})`);
    rejectsWithoutWrites(/topics: contador no es BSON Long/);
});
