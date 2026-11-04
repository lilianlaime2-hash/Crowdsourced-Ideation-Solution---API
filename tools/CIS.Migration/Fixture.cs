using CIS.DataAccess.Context;
using CIS.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MySql.Data.MySqlClient;
namespace CIS.Migration;

internal static class Fixture
{
    private static readonly string[] Users = ["0149d2c8-2bb3-4fb3-a9df-08d252fa6e8c", "2820efe4-a386-4b2f-9fef-c403a1686ea3", "db297278-a905-4ce8-a78e-18e79e0b83db", "5aa675c4-5c08-4331-8159-4e7675bd02cf"];
    private static readonly string[] Logins = ["AdrianCis", "ARuiz", "AdrianRuiz", "jroca2"];
    private static DateTime Date(long offset) => new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(offset);

    internal static async Task SeedAsync(string connectionString)
    {
        var settings = new MySqlConnectionStringBuilder(connectionString);
        if (!settings.Database.StartsWith("cis_phase2_", StringComparison.Ordinal) || settings.Database.Length <= "cis_phase2_".Length)
            throw new InvalidOperationException("Seed requires an existing empty database named cis_phase2_<suffix>.");
        await using var connection = new MySqlConnection(connectionString); await connection.OpenAsync();
        await using (var check = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()", connection))
            if (Convert.ToInt64(await check.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("Seed refuses a nonempty schema; no tables were changed.");
        await using (var context = new CisDbContext(new DbContextOptionsBuilder<CisDbContext>().UseMySQL(connectionString).Options))
            await context.Database.EnsureCreatedAsync();
        await new MySqlCommand("CREATE TABLE users (id varchar(36) NOT NULL PRIMARY KEY, name varchar(200) NOT NULL, login varchar(20) NOT NULL UNIQUE, password varchar(100) NOT NULL) ENGINE=InnoDB", connection).ExecuteNonQueryAsync();
        await using var transaction = connection.BeginTransaction();
        var data = Build();
        foreach (var entity in new[] { "users", "tags", "topics", "ideas", "votes", "topic_tags" })
        {
            var columns = Snapshots.SqlFields[entity].ToList(); if (entity == "users") columns.Add("password");
            await using var insert = new MySqlCommand($"INSERT INTO `{entity}` ({string.Join(',', columns.Select(c => $"`{c}`"))}) VALUES ({string.Join(',', columns.Select((_, i) => "@p" + i))})", connection, transaction);
            for (var i = 0; i < columns.Count; i++) insert.Parameters.AddWithValue("@p" + i, DBNull.Value);
            foreach (var document in data[entity])
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    if (columns[i] == "password") { insert.Parameters[i].Value = "phase2-test-password"; continue; }
                    var v = document[Canonical.Fields[entity][i]];
                    insert.Parameters[i].Value = v.IsBsonDateTime ? v.ToUniversalTime() : v.IsInt64 ? v.AsInt64 : v.AsString;
                }
                await insert.ExecuteNonQueryAsync();
            }
        }
        await transaction.CommitAsync();
    }

    internal static Dictionary<string, List<BsonDocument>> Build()
    {
        var data = Canonical.Entities.ToDictionary(e => e, _ => new List<BsonDocument>());
        for (var u = 0; u < 4; u++) data["users"].Add(new BsonDocument { { "_id", Users[u] }, { "name", "Phase 2 " + Logins[u] }, { "login", Logins[u] } });
        for (long id = 1; id <= 200; id++)
        {
            var tag = TagNamePolicy.ValidateAndNormalize($"Tag {id:D3}");
            data["tags"].Add(new BsonDocument { { "_id", id }, { "name", tag.Name }, { "normalizedName", tag.NormalizedName } });
        }
        long ideaId = 0;
        for (long topicId = 1; topicId <= 500; topicId++)
        {
            var author = topicId <= 350 ? 0 : 1 + (int)((topicId - 351) / 50);
            var topic = new BsonDocument { { "_id", topicId }, { "title", $"Topic {topicId:D3}" }, { "description", $"Deterministic phase 2 topic {topicId:D3}." }, { "status", "Active" }, { "userId", Users[author] }, { "createdAt", new BsonDateTime(Date(topicId)) }, { "updatedAt", new BsonDateTime(Date(topicId + 500)) }, { "tagIds", new BsonArray() } };
            data["topics"].Add(topic);
            var count = topicId <= 100 ? 0 : topicId <= 300 ? 4 : 5;
            for (var t = 0; t < count; t++)
            {
                var tagId = ((topicId - 1) * 5 + t) % 200 + 1;
                topic["tagIds"].AsBsonArray.Add(new BsonInt64(tagId));
                data["topic_tags"].Add(new BsonDocument { { "topicId", topicId }, { "tagId", tagId } });
            }
            var ideas = topicId <= 100 ? 0 : topicId <= 200 ? 1 : topicId <= 300 ? 2 : topicId <= 400 ? 5 : 12;
            for (var i = 0; i < ideas; i++)
            {
                ideaId++;
                author = ideaId <= 1400 ? 0 : 1 + (int)((ideaId - 1401) / 200);
                data["ideas"].Add(new BsonDocument { { "_id", ideaId }, { "topicId", topicId }, { "userId", Users[author] }, { "title", $"Idea {ideaId:D4}" }, { "description", $"Deterministic phase 2 idea {ideaId:D4}." }, { "status", "Active" }, { "createdAt", new BsonDateTime(Date(1000 + ideaId)) }, { "updatedAt", new BsonDateTime(Date(4000 + ideaId)) } });
            }
        }
        // Adrian votes on all 1,800 nonzero ideas. Remaining seats are allocated to
        // the largest remaining quota, keeping each (idea,user) pair unique.
        var remaining = new[] { 1800, 1067, 1067, 1066 };
        long voteId = 0;
        for (long idea = 1; idea <= 2000; idea++)
        {
            var count = idea <= 200 ? 0 : idea <= 400 ? 1 : idea <= 800 ? 2 : idea <= 1600 ? 3 : 4;
            var authors = new List<int>();
            if (count > 0) authors.Add(0);
            authors.AddRange(Enumerable.Range(1, 3).OrderByDescending(u => remaining[u]).ThenBy(u => u).Take(Math.Max(0, count - 1)));
            foreach (var author in authors)
            {
                remaining[author]--; voteId++;
                data["votes"].Add(new BsonDocument { { "_id", voteId }, { "ideaId", idea }, { "userId", Users[author] }, { "createdAt", new BsonDateTime(Date(10000 + voteId)) } });
            }
        }
        if (remaining.Any(n => n != 0)) throw new InvalidOperationException("Fixture vote quotas cannot be satisfied.");
        Snapshots.Validate(data);
        return data;
    }
}
