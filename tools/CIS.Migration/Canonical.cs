using MongoDB.Bson;
using CIS.Domain.Policies;
using System.Text.Json;
namespace CIS.Migration;

public static class Canonical
{
    public static readonly string[] Entities = ["users", "tags", "topics", "ideas", "votes", "topic_tags"];
    public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>
    {
        ["users"] = ["_id", "name", "login"],
        ["tags"] = ["_id", "name", "normalizedName"],
        ["topics"] = ["_id", "title", "description", "status", "userId", "createdAt", "updatedAt", "tagIds"],
        ["ideas"] = ["_id", "topicId", "userId", "title", "description", "status", "createdAt", "updatedAt"],
        ["votes"] = ["_id", "ideaId", "userId", "createdAt"],
        ["topic_tags"] = ["topicId", "tagId"]
    };
    public static string Encode(string entity, BsonDocument document)
    {
        if (!Fields.TryGetValue(entity, out var fields)) throw new InvalidDataException("Unsupported entity.");
        if (document.Names.Except(fields).Any(n => entity != "users" || n != "password"))
            throw new InvalidDataException($"{entity}: unsupported field.");
        var values = new List<object?> { entity };
        foreach (var field in fields)
        {
            if (!document.TryGetValue(field, out var value) || value.IsBsonNull)
                throw new InvalidDataException($"{entity}.{field}: required field missing or null.");
            if (field == "tagIds")
            {
                if (!value.IsBsonArray || value.AsBsonArray.Any(v => !v.IsInt64 || v.AsInt64 <= 0))
                    throw new InvalidDataException("topics.tagIds: expected BSON Long array.");
                var ids = value.AsBsonArray.Select(v => v.AsInt64).Order().ToArray();
                if (ids.Length > 5 || ids.Distinct().Count() != ids.Length)
                    throw new InvalidDataException("topics.tagIds: duplicated or too many tags.");
                values.Add(ids);
            }
            else if (field is "createdAt" or "updatedAt")
            {
                if (!value.IsBsonDateTime) throw new InvalidDataException($"{entity}.{field}: expected BSON DateTime.");
                values.Add(value.AsBsonDateTime.MillisecondsSinceEpoch);
            }
            else if ((field == "_id" && entity != "users") || field is "topicId" or "ideaId" or "tagId")
            {
                if (!value.IsInt64 || value.AsInt64 <= 0) throw new InvalidDataException($"{entity}.{field}: expected positive BSON Long.");
                values.Add(value.AsInt64);
            }
            else
            {
                if (!value.IsString) throw new InvalidDataException($"{entity}.{field}: expected string.");
                var s = value.AsString;
                if ((field == "userId" || field == "_id") && (!Guid.TryParseExact(s, "D", out var guid) || guid == Guid.Empty))
                    throw new InvalidDataException($"{entity}.{field}: expected UUID string.");
                if (field == "status" && !(entity == "topics" ? new[] { "Active", "Closed", "Archived", "Deleted" } : new[] { "Active", "Deleted" }).Contains(s))
                    throw new InvalidDataException($"{entity}.status: unsupported status.");
                values.Add(s);
            }
        }
        if (entity == "tags")
        {
            try
            {
                var normalized = TagNamePolicy.ValidateAndNormalize(document["name"].AsString);
                if (normalized.Name != document["name"].AsString || normalized.NormalizedName != document["normalizedName"].AsString)
                    throw new InvalidDataException("tags: source/destination tag name is not normalized.");
            }
            catch (ArgumentException) { throw new InvalidDataException("tags: invalid tag name."); }
        }
        return JsonSerializer.Serialize(values);
    }
}
