using CIS.Migration;
using MongoDB.Bson;
using MongoDB.Driver;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text;
using Testcontainers.MongoDb;
using Testcontainers.MySql;
namespace CIS.Migration.Tests;

[CollectionDefinition("migration")]
public class MigrationCollection : ICollectionFixture<MigrationContainers>;
public sealed class MigrationContainers : IAsyncLifetime
{
    private readonly MySqlContainer mysql = new MySqlBuilder("mysql:8.4").WithDatabase("cis_phase2_bootstrap").WithUsername("root").WithPassword("phase2-container-only").Build();
    private readonly MongoDbContainer mongo = new MongoDbBuilder("mongo:8.0").Build();
    public async Task InitializeAsync() => await Task.WhenAll(mysql.StartAsync(), mongo.StartAsync());
    public async Task DisposeAsync() { await mysql.DisposeAsync(); await mongo.DisposeAsync(); }
    public async Task<TestDatabases> FreshAsync()
    {
        var name = "cis_phase2_" + Guid.NewGuid().ToString("N");
        var builder = new MySqlConnectionStringBuilder(mysql.GetConnectionString());
        await using (var connection = new MySqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await new MySqlCommand($"CREATE DATABASE `{name}`", connection).ExecuteNonQueryAsync();
        }
        builder.Database = name;
        var url = new MongoUrlBuilder(mongo.GetConnectionString()) { DatabaseName = name, AuthenticationSource = "admin" };
        return new TestDatabases(builder.ConnectionString, url.ToString());
    }
}
public sealed record TestDatabases(string MySql, string Mongo)
{
    public MigrationRunner Runner => new(MySql, Mongo);
    public IMongoDatabase Destination => new MongoClient(Mongo).GetDatabase(MongoUrl.Create(Mongo).DatabaseName);
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new MySqlConnection(MySql); await connection.OpenAsync();
        await new MySqlCommand(sql, connection).ExecuteNonQueryAsync();
    }
    public async Task<string> SourceFingerprintAsync()
    {
        // Independent SQL observation: includes every source field, including only
        // the synthetic fixture password, and is never exported to a report.
        var text = new StringBuilder();
        await using var connection = new MySqlConnection(MySql); await connection.OpenAsync();
        foreach (var table in new[] { "users", "tags", "topics", "ideas", "votes", "topic_tags" })
        {
            await using var reader = await new MySqlCommand($"SELECT * FROM {table} ORDER BY 1,2", connection).ExecuteReaderAsync();
            while (await reader.ReadAsync())
                for (var i = 0; i < reader.FieldCount; i++) text.Append(System.Text.Json.JsonSerializer.Serialize(reader.GetValue(i))).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
[Collection("migration")]
public class MigrationIntegrationTests(MigrationContainers containers)
{
    [Fact]
    public async Task FullFixtureMigratesDeterministicallyAndDoesNotMutateSource()
    {
        var first = await containers.FreshAsync(); var second = await containers.FreshAsync();
        await first.Runner.SeedAsync(); await second.Runner.SeedAsync();
        var before = await first.SourceFingerprintAsync();
        await first.Runner.MigrateAsync(); await second.Runner.MigrateAsync();
        var a = await first.Runner.VerifyAsync(); var b = await second.Runner.VerifyAsync();
        Assert.True(a.Passed, string.Join(";", a.Errors)); Assert.True(b.Passed, string.Join(";", b.Errors));
        var expected = new Dictionary<string, long> { ["users"] = 4, ["tags"] = 200, ["topics"] = 500, ["ideas"] = 2000, ["votes"] = 5000, ["topic_tags"] = 1800 };
        foreach (var (entity, count) in expected)
        {
            Assert.Equal(count, a.Entities[entity].SourceCount); Assert.Equal(count, a.Entities[entity].DestinationCount);
            Assert.Equal(a.Entities[entity].SourceSha256, b.Entities[entity].SourceSha256);
            Assert.Equal(a.Entities[entity].SourceSha256, a.Entities[entity].DestinationSha256);
        }
        Assert.Equal(500L, (await first.Destination.GetCollection<BsonDocument>("counters").Find(new BsonDocument("_id", "topics")).SingleAsync())["seq"].AsInt64);
        await using var connection = new MySqlConnection(first.MySql); await connection.OpenAsync();
        Assert.Equal("phase2-test-password", await new MySqlCommand("SELECT password FROM users ORDER BY login LIMIT 1", connection).ExecuteScalarAsync());
        Assert.False((await first.Destination.GetCollection<BsonDocument>("users").Find(FilterDefinition<BsonDocument>.Empty).FirstAsync()).Contains("password"));
        Assert.Equal(before, await first.SourceFingerprintAsync());
        var topics = await first.Destination.GetCollection<BsonDocument>("topics").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var ideas = await first.Destination.GetCollection<BsonDocument>("ideas").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var votes = await first.Destination.GetCollection<BsonDocument>("votes").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(new[] { (0, 100), (4, 200), (5, 200) }, topics.GroupBy(t => t["tagIds"].AsBsonArray.Count).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToArray());
        var ideasPerTopic = ideas.GroupBy(i => i["topicId"].AsInt64).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new[] { (0, 100), (1, 100), (2, 100), (5, 100), (12, 100) }, topics.GroupBy(t => ideasPerTopic.GetValueOrDefault(t["_id"].AsInt64)).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToArray());
        var votesPerIdea = votes.GroupBy(v => v["ideaId"].AsInt64).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new[] { (0, 200), (1, 200), (2, 400), (3, 800), (4, 400) }, ideas.GroupBy(i => votesPerIdea.GetValueOrDefault(i["_id"].AsInt64)).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToArray());
        var users = await first.Destination.GetCollection<BsonDocument>("users").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var quotas = new Dictionary<string, (int Topics, int Ideas, int Votes)> { ["AdrianCis"] = (350, 1400, 1800), ["ARuiz"] = (50, 200, 1067), ["AdrianRuiz"] = (50, 200, 1067), ["jroca2"] = (50, 200, 1066) };
        foreach (var user in users)
        {
            var quota = quotas[user["login"].AsString]; var id = user["_id"].AsString;
            Assert.Equal(quota, (topics.Count(d => d["userId"] == id), ideas.Count(d => d["userId"] == id), votes.Count(d => d["userId"] == id)));
        }
        var duplicateVote = votes[0].DeepClone().AsBsonDocument.Set("_id", 999999L);
        await Assert.ThrowsAsync<MongoWriteException>(() => first.Destination.GetCollection<BsonDocument>("votes").InsertOneAsync(duplicateVote));
        var duplicateTag = new BsonDocument { { "_id", 999999L }, { "name", "Tag 001" }, { "normalizedName", "TAG 001" } };
        await Assert.ThrowsAsync<MongoWriteException>(() => first.Destination.GetCollection<BsonDocument>("tags").InsertOneAsync(duplicateTag));
    }

    [Fact]
    public async Task SameCountTamperingInEveryEntityAndRelationsIsDetectedReadOnly()
    {
        var db = await containers.FreshAsync(); await db.Runner.SeedAsync(); await db.Runner.MigrateAsync();
        var mutations = new (string Entity, string Field, BsonValue Value)[] {
            ("users", "name", "Tampered"), ("tags", "name", "Changed tag"), ("topics", "title", "Changed"),
            ("ideas", "description", "Changed"), ("votes", "createdAt", new BsonDateTime(DateTime.UnixEpoch)),
            ("topics", "tagIds", new BsonArray { 199L }), ("topics", "status", "Closed"),
            ("ideas", "status", "Deleted"), ("topics", "createdAt", new BsonDateTime(DateTime.UnixEpoch)),
            ("ideas", "updatedAt", BsonNull.Value), ("topics", "status", "unsupported"),
            ("topics", "tagIds", new BsonArray { 1L, 1L }), ("votes", "ideaId", 999999L),
            ("ideas", "topicId", 999999L), ("votes", "userId", "00000000-0000-0000-0000-000000000001"),
            ("topics", "tagIds", new BsonArray { 999999L }), ("topics", "_id", 9007199254740993L)
        };
        foreach (var mutation in mutations)
        {
            var collection = db.Destination.GetCollection<BsonDocument>(mutation.Entity);
            var original = await collection.Find(FilterDefinition<BsonDocument>.Empty).Sort(new BsonDocument("_id", 1)).FirstAsync();
            var changed = original.DeepClone().AsBsonDocument.Set(mutation.Field, mutation.Value);
            await collection.DeleteOneAsync(new BsonDocument("_id", original["_id"])); await collection.InsertOneAsync(changed);
            var result = await db.Runner.VerifyAsync();
            Assert.False(result.Passed, $"Undetected {mutation.Entity}.{mutation.Field}");
            if (mutation.Field == "tagIds") Assert.NotEqual(result.Entities["topic_tags"].SourceSha256, result.Entities["topic_tags"].DestinationSha256);
            Assert.Equal(changed, await collection.Find(new BsonDocument("_id", changed["_id"])).SingleAsync());
            await collection.DeleteOneAsync(new BsonDocument("_id", changed["_id"])); await collection.InsertOneAsync(original);
        }
        await db.Destination.GetCollection<BsonDocument>("topics").UpdateOneAsync(new BsonDocument("_id", 1L), new BsonDocument("$unset", new BsonDocument("status", "")));
        Assert.False((await db.Runner.VerifyAsync()).Passed);
    }

    [Fact]
    public async Task VerifyCliWritesSafeReportAndReturnsNonzeroForSameCountMismatch()
    {
        var db = await containers.FreshAsync(); await db.Runner.SeedAsync(); await db.Runner.MigrateAsync();
        var oldMySql = Environment.GetEnvironmentVariable("CIS_MYSQL_CONNECTION");
        var oldMongo = Environment.GetEnvironmentVariable("CIS_MONGO_CONNECTION");
        var reportPath = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable("CIS_MYSQL_CONNECTION", db.MySql);
            Environment.SetEnvironmentVariable("CIS_MONGO_CONNECTION", db.Mongo);
            Assert.Equal(0, await Program.Main(["verify", "--report", reportPath]));
            await db.Destination.GetCollection<BsonDocument>("ideas").UpdateOneAsync(new BsonDocument("_id", 1L), new BsonDocument("$set", new BsonDocument("title", "Tampered")));
            Assert.Equal(1, await Program.Main(["verify", "--report", reportPath]));
            var json = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("\"Passed\": false", json); Assert.Contains("\"SourceCount\": 2000", json);
            Assert.DoesNotContain("phase2-test-password", json); Assert.DoesNotContain("phase2-container-only", json);
            Assert.DoesNotContain(db.MySql, json); Assert.DoesNotContain(db.Mongo, json);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CIS_MYSQL_CONNECTION", oldMySql);
            Environment.SetEnvironmentVariable("CIS_MONGO_CONNECTION", oldMongo); File.Delete(reportPath);
        }
    }

    [Fact]
    public async Task VerificationAcceptsSafeAdvancedSequencesButRejectsSequencesBelowMigratedIds()
    {
        var db = await containers.FreshAsync();
        await db.Runner.SeedAsync();
        await db.Runner.MigrateAsync();
        var counters = db.Destination.GetCollection<BsonDocument>("counters");
        await counters.UpdateOneAsync(new BsonDocument("_id", "topics"), new BsonDocument("$set", new BsonDocument("seq", 600L)));
        Assert.True((await db.Runner.VerifyAsync()).Passed, "A safely advanced sequence must not invalidate equivalent data.");
        await counters.UpdateOneAsync(new BsonDocument("_id", "topics"), new BsonDocument("$set", new BsonDocument("seq", 499L)));
        Assert.False((await db.Runner.VerifyAsync()).Passed);
    }

    [Fact]
    public async Task InvalidCountersAndDuplicateVotePairsAreReported()
    {
        var db = await containers.FreshAsync(); await db.Runner.SeedAsync(); await db.Runner.MigrateAsync();
        var counters = db.Destination.GetCollection<BsonDocument>("counters");
        await counters.UpdateOneAsync(new BsonDocument("_id", "topics"), new BsonDocument("$set", new BsonDocument("seq", 500)));
        var report = await db.Runner.VerifyAsync(); Assert.False(report.Passed); Assert.Contains(report.Errors, e => e.Contains("counters.topics"));
        Assert.True((await counters.Find(new BsonDocument("_id", "topics")).SingleAsync())["seq"].IsInt32);
        await counters.UpdateOneAsync(new BsonDocument("_id", "topics"), new BsonDocument("$set", new BsonDocument("seq", 500L)));
        var votes = db.Destination.GetCollection<BsonDocument>("votes"); await votes.Indexes.DropOneAsync("ux_votes_ideaId_userId");
        var originals = await votes.Find(FilterDefinition<BsonDocument>.Empty).Sort(new BsonDocument("_id", 1)).Limit(2).ToListAsync();
        var duplicate = originals[0].DeepClone().AsBsonDocument.Set("_id", originals[1]["_id"]);
        await votes.ReplaceOneAsync(new BsonDocument("_id", originals[1]["_id"]), duplicate);
        report = await db.Runner.VerifyAsync(); Assert.False(report.Passed);
        Assert.Contains(report.Errors, e => e.Contains("duplicate value"));
        Assert.Equal(5000L, report.Entities["votes"].DestinationCount);
    }

    [Fact]
    public async Task RefusesNonemptySeedAndTargetWithoutDeletingAnything()
    {
        var db = await containers.FreshAsync(); await db.ExecuteAsync("CREATE TABLE sentinel (id int)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Runner.SeedAsync());
        var wrong = new MySqlConnectionStringBuilder(db.MySql) { Database = "sd3" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MigrationRunner(wrong.ConnectionString, db.Mongo).SeedAsync());
        db = await containers.FreshAsync(); await db.Runner.SeedAsync();
        await db.Destination.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument("_id", "sentinel"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Runner.MigrateAsync());
        Assert.Equal(1, await db.Destination.GetCollection<BsonDocument>("users").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0, await db.Destination.GetCollection<BsonDocument>("topics").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task LargeLongIdsSurviveWithoutFloatingPointLoss()
    {
        var db = await containers.FreshAsync(); await db.Runner.SeedAsync();
        await db.ExecuteAsync("SET FOREIGN_KEY_CHECKS=0; UPDATE topics SET id=id+9007199254740992; UPDATE ideas SET id=id+9007199254740992,topic_id=topic_id+9007199254740992; UPDATE votes SET id=id+9007199254740992,idea_id=idea_id+9007199254740992; UPDATE tags SET id=id+9007199254740992; UPDATE topic_tags SET topic_id=topic_id+9007199254740992,tag_id=tag_id+9007199254740992; SET FOREIGN_KEY_CHECKS=1;");
        await db.Runner.MigrateAsync(); Assert.True((await db.Runner.VerifyAsync()).Passed);
        var first = await db.Destination.GetCollection<BsonDocument>("topics").Find(new BsonDocument("_id", 9007199254740993L)).SingleAsync();
        Assert.True(first["_id"].IsInt64); Assert.Equal(9007199254740993L, first["_id"].AsInt64);
    }

    [Fact]
    public async Task UnsupportedSourceStatusOrPrecisionIsRejectedBeforeTargetWrites()
    {
        var db = await containers.FreshAsync(); await db.Runner.SeedAsync();
        await db.ExecuteAsync("UPDATE topics SET status='unsupported' WHERE id=1");
        await Assert.ThrowsAsync<InvalidDataException>(() => db.Runner.MigrateAsync());
        Assert.Empty(await db.Destination.ListCollectionNames().ToListAsync());
        await db.ExecuteAsync("UPDATE topics SET status='Active',created_at='2025-01-01 00:00:00.000001' WHERE id=1");
        await Assert.ThrowsAsync<InvalidDataException>(() => db.Runner.MigrateAsync());
        Assert.Empty(await db.Destination.ListCollectionNames().ToListAsync());
    }
}
