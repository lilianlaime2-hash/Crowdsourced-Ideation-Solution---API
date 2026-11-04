using MongoDB.Driver;
using MongoDB.Bson;
namespace CIS.Migration;

public sealed record EntityVerification(long SourceCount, long DestinationCount, string? SourceSha256, string? DestinationSha256, string[] Mismatches);
public sealed record VerificationReport(bool Passed, Dictionary<string, EntityVerification> Entities, string[] Errors);
public sealed class MigrationRunner(string mysqlConnection, string mongoConnection)
{
    private IMongoDatabase Database()
    {
        var url = MongoUrl.Create(mongoConnection);
        if (string.IsNullOrWhiteSpace(url.DatabaseName)) throw new InvalidOperationException("CIS_MONGO_CONNECTION must include a database name.");
        return new MongoClient(url).GetDatabase(url.DatabaseName);
    }
    public Task SeedAsync() => Fixture.SeedAsync(mysqlConnection);
    public async Task MigrateAsync()
    {
        var database = Database();
        await EnsureEmptyAsync(database);
        var source = await Snapshots.ReadMySqlAsync(mysqlConnection);
        Snapshots.Validate(source);
        // Check again after reading the snapshot, before creating indexes/documents.
        await EnsureEmptyAsync(database);
        await database.GetCollection<BsonDocument>("tags").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("normalizedName"), new CreateIndexOptions { Unique = true, Name = "ux_tags_normalizedName" }));
        await database.GetCollection<BsonDocument>("votes").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("ideaId").Ascending("userId"), new CreateIndexOptions { Unique = true, Name = "ux_votes_ideaId_userId" }));
        foreach (var entity in Canonical.Entities.Where(e => e != "topic_tags"))
            if (source[entity].Count > 0) await database.GetCollection<BsonDocument>(entity).InsertManyAsync(source[entity]);
        var counters = new[] { "tags", "topics", "ideas", "votes" }.Select(entity => new BsonDocument { { "_id", entity }, { "seq", source[entity].Select(d => d["_id"].AsInt64).DefaultIfEmpty(0).Max() } });
        await database.GetCollection<BsonDocument>("counters").InsertManyAsync(counters);
    }
    private static async Task EnsureEmptyAsync(IMongoDatabase database)
    {
        foreach (var name in await database.ListCollectionNames().ToListAsync())
            if (await database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty) > 0)
                throw new InvalidOperationException("Migration refuses a nonempty destination database; nothing was overwritten.");
    }
    public async Task<VerificationReport> VerifyAsync()
    {
        var errors = new List<string>();
        var entities = new Dictionary<string, EntityVerification>();
        Dictionary<string, List<BsonDocument>> source;
        try { source = await Snapshots.ReadMySqlAsync(mysqlConnection); }
        catch (InvalidDataException ex) { return new(false, entities, ["source: " + ex.Message]); }
        var database = Database();
        var target = await Snapshots.ReadMongoAsync(database);
        foreach (var entity in Canonical.Entities)
        {
            SortedDictionary<string, string>? a = null, b = null;
            try { a = Snapshots.Rows(entity, source[entity]); } catch (InvalidDataException ex) { errors.Add("source: " + ex.Message); }
            try { b = Snapshots.Rows(entity, target[entity]); } catch (InvalidDataException ex) { errors.Add("destination: " + ex.Message); }
            var mismatch = a is null || b is null ? ["invalid schema"] : a.Keys.Union(b.Keys).Where(k => !a.TryGetValue(k, out var av) || !b.TryGetValue(k, out var bv) || av != bv).Take(20).ToArray();
            entities[entity] = new(source[entity].Count, target[entity].Count, a is null ? null : Snapshots.Hash(a), b is null ? null : Snapshots.Hash(b), mismatch);
            if (a is null || b is null || entities[entity].SourceSha256 != entities[entity].DestinationSha256 || source[entity].Count != target[entity].Count) errors.Add($"{entity}: count/content mismatch.");
        }
        try { Snapshots.Validate(source); } catch (InvalidDataException ex) { errors.Add("source: " + ex.Message); }
        try { Snapshots.Validate(target); } catch (InvalidDataException ex) { errors.Add("destination: " + ex.Message); }
        var counters = await database.GetCollection<BsonDocument>("counters").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        foreach (var entity in new[] { "tags", "topics", "ideas", "votes" })
        {
            var rows = counters.Where(d => d.GetValue("_id", BsonNull.Value) == entity).ToArray();
            var expected = source[entity].Select(d => d["_id"].AsInt64).DefaultIfEmpty(0).Max();
            if (rows.Length != 1 || !rows[0].TryGetValue("seq", out var seq) || !seq.IsInt64 || seq.AsInt64 < expected || rows[0].ElementCount != 2)
                errors.Add($"counters.{entity}: invalid BSON Long or sequence below the migrated maximum.");
        }
        if (counters.Count != 4) errors.Add("counters: expected exactly four sequences.");
        return new(errors.Count == 0, entities, errors.ToArray());
    }
}
