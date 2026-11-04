using System.Data;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using MySql.Data.MySqlClient;
namespace CIS.Migration;

internal static class Snapshots
{
    internal static readonly Dictionary<string, string[]> SqlFields = new()
    {
        ["users"] = ["id", "name", "login"],
        ["tags"] = ["id", "name", "normalized_name"],
        ["topics"] = ["id", "title", "description", "status", "user_id", "created_at", "updated_at"],
        ["ideas"] = ["id", "topic_id", "user_id", "title", "description", "status", "created_at", "updated_at"],
        ["votes"] = ["id", "idea_id", "user_id", "created_at"],
        ["topic_tags"] = ["topic_id", "tag_id"]
    };

    internal static async Task<Dictionary<string, List<BsonDocument>>> ReadMySqlAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        // All six reads share one InnoDB consistent snapshot; no source data is changed.
        var result = new Dictionary<string, List<BsonDocument>>();
        foreach (var (entity, columns) in SqlFields)
        {
            var docs = new List<BsonDocument>();
            await using var command = new MySqlCommand($"SELECT {string.Join(',', columns.Select(c => $"`{c}`"))} FROM `{entity}`", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var doc = new BsonDocument();
                var fields = Canonical.Fields[entity];
                for (var i = 0; i < columns.Length; i++)
                {
                    var field = fields[i];
                    if (reader.IsDBNull(i)) throw new InvalidDataException($"{entity}.{field}: null source value.");
                    if (field is "createdAt" or "updatedAt")
                    {
                        var date = DateTime.SpecifyKind(reader.GetDateTime(i), DateTimeKind.Utc);
                        if (date.Ticks % TimeSpan.TicksPerMillisecond != 0)
                            throw new InvalidDataException($"{entity}.{field}: sub-millisecond source precision cannot be preserved in BSON DateTime.");
                        doc[field] = new BsonDateTime(date);
                    }
                    else if ((field == "_id" && entity != "users") || field is "topicId" or "ideaId" or "tagId") doc[field] = new BsonInt64(reader.GetInt64(i));
                    else doc[field] = reader.GetString(i);
                }
                if (entity == "topics") doc["tagIds"] = new BsonArray();
                docs.Add(doc);
            }
            result[entity] = docs;
        }
        var topics = result["topics"].ToDictionary(d => d["_id"].AsInt64);
        foreach (var link in result["topic_tags"])
        {
            if (!topics.TryGetValue(link["topicId"].AsInt64, out var topic)) throw new InvalidDataException("topic_tags: missing topic.");
            topic["tagIds"].AsBsonArray.Add(link["tagId"]);
        }
        await transaction.CommitAsync();
        return result;
    }

    internal static async Task<Dictionary<string, List<BsonDocument>>> ReadMongoAsync(IMongoDatabase database)
    {
        var result = new Dictionary<string, List<BsonDocument>>();
        foreach (var entity in Canonical.Entities.Where(e => e != "topic_tags"))
            result[entity] = await database.GetCollection<BsonDocument>(entity).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        result["topic_tags"] = [];
        foreach (var topic in result["topics"])
        {
            // A missing/invalid array still fails canonical validation of the topic.
            if (topic.TryGetValue("tagIds", out var tags) && tags.IsBsonArray)
                foreach (var id in tags.AsBsonArray)
                    result["topic_tags"].Add(new BsonDocument { { "topicId", topic.GetValue("_id", BsonNull.Value) }, { "tagId", id } });
        }
        return result;
    }

    internal static SortedDictionary<string, string> Rows(string entity, List<BsonDocument> docs)
    {
        var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var doc in docs)
        {
            var canonical = Canonical.Encode(entity, doc);
            var key = entity == "topic_tags" ? $"{doc["topicId"].AsInt64}/{doc["tagId"].AsInt64}" : doc["_id"].ToString()!;
            if (!rows.TryAdd(key, canonical)) throw new InvalidDataException($"{entity}: duplicate identifier.");
        }
        return rows;
    }
    internal static string Hash(SortedDictionary<string, string> rows) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows.Values) + "\n")));

    internal static void Validate(Dictionary<string, List<BsonDocument>> data)
    {
        foreach (var entity in Canonical.Entities) Rows(entity, data[entity]);
        var users = data["users"].Select(d => d["_id"].AsString).ToHashSet(StringComparer.Ordinal);
        var topics = data["topics"].Select(d => d["_id"].AsInt64).ToHashSet();
        var ideas = data["ideas"].Select(d => d["_id"].AsInt64).ToHashSet();
        var tags = data["tags"].Select(d => d["_id"].AsInt64).ToHashSet();
        Unique(data["users"].Select(d => d["login"].AsString), "users.login");
        Unique(data["tags"].Select(d => d["normalizedName"].AsString), "tags.normalizedName");
        Unique(data["votes"].Select(d => $"{d["ideaId"].AsInt64}/{d["userId"].AsString}"), "votes idea/user pair");
        foreach (var entity in new[] { "topics", "ideas", "votes" })
            if (data[entity].Any(d => !users.Contains(d["userId"].AsString))) throw new InvalidDataException($"{entity}: missing user foreign key.");
        if (data["ideas"].Any(d => !topics.Contains(d["topicId"].AsInt64))) throw new InvalidDataException("ideas: missing topic foreign key.");
        if (data["votes"].Any(d => !ideas.Contains(d["ideaId"].AsInt64))) throw new InvalidDataException("votes: missing idea foreign key.");
        if (data["topic_tags"].Any(d => !topics.Contains(d["topicId"].AsInt64) || !tags.Contains(d["tagId"].AsInt64))) throw new InvalidDataException("topic_tags: missing foreign key.");
    }
    private static void Unique(IEnumerable<string> values, string field)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (values.Any(v => !seen.Add(v))) throw new InvalidDataException($"{field}: duplicate value.");
    }
}
