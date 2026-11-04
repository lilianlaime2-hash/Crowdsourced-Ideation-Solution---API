
"use strict";


const DATABASE = "sd3db_mongo_perf";

const EXPECTED = {
    users: 4,
    tags: 200,
    topics: 500,
    ideas: 2000,
    votes: 5000
};

const TAG_DISTRIBUTION = {
    0: 100,
    4: 200,
    5: 200
};

const IDEA_DISTRIBUTION = {
    0: 100,
    1: 100,
    2: 100,
    5: 100,
    12: 100
};

const VOTE_DISTRIBUTION = {
    0: 200,
    1: 200,
    2: 400,
    3: 800,
    4: 400,
};

const EXPECTED_AUTHORS = {
    AdrianCis: { topics: 350, ideas: 1400, votes: 1800 },
    ARuiz: { topics: 50, ideas: 200, votes: 1067 },
    AdrianRuiz: { topics: 50, ideas: 200, votes: 1067 },
    jroca2: { topics: 50, ideas: 200, votes: 1066 }
};

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function validate(condition, message) {
    if (!condition) {
        throw new Error(message);
    }
}

function isLong(value) {
    return value !== null &&
           typeof value === "object" &&
           value._bsontype === "Long";
}

function longKey(value, field) {
    validate(isLong(value), `${field} debe ser BSON Long`);
    return value.toString();
}

function verifyDistribution(values, expected, label) {
    const actual = {};

    for (const value of values) {
        actual[value] = (actual[value] || 0) + 1;
    }

    const keys = new Set([
        ...Object.keys(expected),
        ...Object.keys(actual)
    ]);

    for (const key of keys) {
        validate(
            (actual[key] || 0) === (expected[key] || 0),
            `${label}: cantidad incorrecta para ${key}`
        );
    }

    print(`[OK] ${label}: ${JSON.stringify(actual)}`);
}

function normalizedTagName(name, id) {
    validate(typeof name === "string", `Etiqueta ${id}: nombre invalido`);
    const cleanName = name.normalize("NFKC").trim().replace(/\s+/gu, " ");
    validate(
        cleanName.length >= 1 && cleanName.length <= 30 &&
        /^[\p{L}\p{M}\p{N} #+.\-]+$/u.test(cleanName) && /\p{L}/u.test(cleanName),
        `Etiqueta ${id}: nombre no permitido por la API`
    );
    return cleanName.toUpperCase();
}

function main() {
    // 1. PROTEGER LA BASE DE DATOS
    validate(
        db.getName() === DATABASE,
        `Base incorrecta: ${db.getName()}`
    );

    print("========================================");
    print(" CIS - POSTMIGRACION MYSQL A MONGODB");
    print("========================================");
    print(`Base de datos: ${db.getName()}`);

    // 2. VALIDAR CANTIDADES
    print("\n=== 1. VALIDACION DE DOCUMENTOS ===");

    for (const [collection, expected] of Object.entries(EXPECTED)) {
        const actual = db.getCollection(collection).countDocuments({});

        validate(
            actual === expected,
            `${collection}: esperado ${expected}, encontrado ${actual}`
        );

        print(`[OK] ${collection}: ${actual}`);
    }

    // 3. CARGAR DATOS PARA VERIFICAR RELACIONES
    const users = db.users.find({}, { _id: 1, login: 1 }).toArray();

    const tags = db.tags.find(
        {},
        { _id: 1, name: 1, normalizedName: 1 }
    ).toArray();

    const topics = db.topics.find(
        {},
        { _id: 1, userId: 1, tagIds: 1 }
    ).toArray();

    const ideas = db.ideas.find(
        {},
        { _id: 1, topicId: 1, userId: 1 }
    ).toArray();

    const votes = db.votes.find(
        {},
        { _id: 1, ideaId: 1, userId: 1 }
    ).toArray();

    // 4. VALIDAR USUARIOS
    const userIds = new Set();
    const logins = new Set();
    const authorCounts = new Map();

    for (const user of users) {
        validate(
            typeof user._id === "string" && UUID_PATTERN.test(user._id),
            `UUID de usuario invalido: ${user._id}`
        );
        validate(
            Object.hasOwn(EXPECTED_AUTHORS, user.login),
            `Usuario no permitido: ${user.login}`
        );
        validate(
            !logins.has(user.login),
            `Login de usuario repetido: ${user.login}`
        );

        userIds.add(user._id);
        logins.add(user.login);
        authorCounts.set(user._id, { topics: 0, ideas: 0, votes: 0 });
    }

    print(`[OK] Usuarios permitidos: ${users.map(user => user.login).join(", ")}`);

    // 5. VALIDAR ETIQUETAS
    const tagIds = new Set();
    const normalizedNames = new Set();

    for (const tag of tags) {
        const id = longKey(tag._id, "tags._id");
        tagIds.add(id);

        validate(
            typeof tag.normalizedName === "string",
            "normalizedName invalido"
        );

        const expectedName = normalizedTagName(tag.name, id);
        validate(
            tag.normalizedName === expectedName,
            `Etiqueta ${id}: normalizacion inconsistente; esperado '${expectedName}', ` +
            `encontrado '${tag.normalizedName}'. Corrige el origen y vuelve a migrar.`
        );

        validate(
            !normalizedNames.has(tag.normalizedName),
            `Etiqueta duplicada: ${tag.normalizedName}`
        );

        normalizedNames.add(tag.normalizedName);
    }

    // 6. VALIDAR TOPICOS Y ETIQUETAS EMBEBIDAS
    print("\n=== 2. VALIDACION DE RELACIONES ===");

    const topicIds = new Set();
    const tagCounts = [];
    const ideaCounts = new Map();

    let tagRelations = 0;

    for (const topic of topics) {
        const id = longKey(topic._id, "topics._id");

        topicIds.add(id);

        validate(
            userIds.has(topic.userId),
            `Topic ${id}: usuario inexistente`
        );
        authorCounts.get(topic.userId).topics++;

        validate(
            topic.tagIds === undefined ||
            Array.isArray(topic.tagIds),
            `Topic ${id}: tagIds invalido`
        );

        const topicTags = topic.tagIds || [];

        validate(
            topicTags.length <= 5,
            `Topic ${id}: tiene mas de 5 etiquetas`
        );

        const uniqueTags = new Set();

        for (const tag of topicTags) {
            const tagId = longKey(tag, "topics.tagIds");

            validate(
                tagIds.has(tagId),
                `Topic ${id}: etiqueta inexistente`
            );

            validate(
                !uniqueTags.has(tagId),
                `Topic ${id}: etiqueta repetida`
            );

            uniqueTags.add(tagId);
        }

        tagRelations += topicTags.length;
        tagCounts.push(topicTags.length);
        ideaCounts.set(id, 0);
    }

    validate(
        tagRelations === 1800,
        `Se esperaban 1800 relaciones, hay ${tagRelations}`
    );

    print(`[OK] Relaciones topic_tags: ${tagRelations}`);

    verifyDistribution(
        tagCounts,
        TAG_DISTRIBUTION,
        "Etiquetas por topico"
    );

    // 7. VALIDAR IDEAS
    const ideaIds = new Set();
    const voteCounts = new Map();

    for (const idea of ideas) {
        const id = longKey(idea._id, "ideas._id");
        const topicId = longKey(idea.topicId, "ideas.topicId");

        validate(
            topicIds.has(topicId),
            `Idea ${id}: topico inexistente`
        );

        validate(
            userIds.has(idea.userId),
            `Idea ${id}: usuario inexistente`
        );
        authorCounts.get(idea.userId).ideas++;

        ideaIds.add(id);
        ideaCounts.set(topicId, ideaCounts.get(topicId) + 1);
        voteCounts.set(id, 0);
    }

    verifyDistribution(
        ideaCounts.values(),
        IDEA_DISTRIBUTION,
        "Ideas por topico"
    );

    // 8. VALIDAR VOTOS
    const uniqueVotes = new Set();

    for (const vote of votes) {
        const id = longKey(vote._id, "votes._id");
        const ideaId = longKey(vote.ideaId, "votes.ideaId");

        validate(
            ideaIds.has(ideaId),
            `Voto ${id}: idea inexistente`
        );

        validate(
            userIds.has(vote.userId),
            `Voto ${id}: usuario inexistente`
        );

        const uniqueKey = `${ideaId}:${vote.userId}`;

        validate(
            !uniqueVotes.has(uniqueKey),
            `Voto duplicado: ${uniqueKey}`
        );

        uniqueVotes.add(uniqueKey);
        authorCounts.get(vote.userId).votes++;

        voteCounts.set(
            ideaId,
            voteCounts.get(ideaId) + 1
        );
    }

    verifyDistribution(
        voteCounts.values(),
        VOTE_DISTRIBUTION,
        "Votos por idea"
    );

    print("[OK] Integridad referencial validada");
    print("[OK] No existen votos duplicados");

    // Cuotas de autoria tras la redistribucion. No basta con validar los totales.
    for (const user of users) {
        const actual = authorCounts.get(user._id);
        const expected = EXPECTED_AUTHORS[user.login];
        for (const name of ["topics", "ideas", "votes"]) {
            validate(
                actual[name] === expected[name],
                `${user.login}: ${name} esperado ${expected[name]}, encontrado ${actual[name]}`
            );
        }
        print(`[OK] ${user.login}: topics=${actual.topics}, ideas=${actual.ideas}, votes=${actual.votes}`);
    }

    // Comprobar tipos existentes antes de la primera escritura.
    const sequences = ["topics", "tags", "ideas", "votes"].map(name => {
        const lastDocument = db.getCollection(name)
            .find({}, { _id: 1 }).sort({ _id: -1 }).limit(1).toArray()[0];
        const maxId = lastDocument ? lastDocument._id : NumberLong("0");
        validate(isLong(maxId), `${name}: el ID maximo no es BSON Long`);

        const counter = db.counters.findOne({ _id: name });
        if (counter !== null) {
            validate(isLong(counter.seq), `${name}: contador no es BSON Long`);
        }
        return { name, maxId };
    });

    // 9. CREAR INDICES UNICOS
    // Las operaciones de escritura comienzan aqui.
    print("\n=== 3. CREACION DE INDICES ===");

    db.tags.createIndex(
        { normalizedName: 1 },
        {
            unique: true,
            name: "ux_tags_normalizedName"
        }
    );

    print("[OK] Indice unico tags.normalizedName");

    db.votes.createIndex(
        { ideaId: 1, userId: 1 },
        {
            unique: true,
            name: "ux_votes_ideaId_userId"
        }
    );

    print("[OK] Indice unico votes(ideaId, userId)");

    // 10. INICIALIZAR SECUENCIAS
    print("\n=== 4. INICIALIZACION DE CONTADORES ===");

    for (const { name, maxId } of sequences) {
        db.counters.updateOne(
            { _id: name },
            { $max: { seq: maxId } },
            { upsert: true }
        );

        const counter = db.counters.findOne({ _id: name });

        validate(
            isLong(counter.seq),
            `${name}: contador no es BSON Long`
        );
        validate(
            BigInt(counter.seq.toString()) >= BigInt(maxId.toString()),
            `${name}: contador menor al ID maximo`
        );

        print(
            `[OK] ${name}: maxId=${maxId}, contador=${counter.seq}`
        );
    }

    // 11. COMPROBACION FINAL
    print("\n=== 5. COMPROBACION FINAL ===");

    const tagIndex = db.tags.getIndexes().find(
        i => i.name === "ux_tags_normalizedName" &&
             i.unique === true
    );

    const voteIndex = db.votes.getIndexes().find(
        i => i.name === "ux_votes_ideaId_userId" &&
             i.unique === true
    );

    validate(tagIndex, "Falta indice unico de etiquetas");
    validate(voteIndex, "Falta indice unico de votos");

    print("[OK] Indices verificados");

    printjson(
        db.counters.find()
            .sort({ _id: 1 })
            .toArray()
    );

    print("\n========================================");
    print(" POSTMIGRACION COMPLETADA");
    print("========================================");
}

try {
    main();
} catch (error) {
    print(`\n[ERROR] ${error.message || error}`);
    print("No habilites escrituras hasta corregir el error.");
    exit(1);
}
