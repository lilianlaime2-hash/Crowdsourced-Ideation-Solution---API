using CIS.Domain.Entities;
using CIS.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CIS.DataAccess.MongoDB.Documents;

[BsonIgnoreExtraElements]
internal sealed class TopicDocument
{
    [BsonId]
    public long Id { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public TopicStatus Status { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }

    // The tags are stored as a list of ids, the topic_tags table is not needed
    [BsonElement("tagIds")]
    public List<long> TagIds { get; set; } = new();

    public static TopicDocument From(Topic topic) => new()
    {
        Id = topic.Id,
        Title = topic.Title,
        Description = topic.Description,
        Status = topic.Status,
        UserId = topic.UserId,
        CreatedAt = topic.CreatedAt,
        UpdatedAt = topic.UpdatedAt,
        TagIds = topic.TopicTags.Select(tt => tt.Tag.Id).ToList()
    };
}