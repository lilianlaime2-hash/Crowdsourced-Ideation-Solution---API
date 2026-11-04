using CIS.Domain.Entities;
using CIS.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CIS.DataAccess.MongoDB.Documents;

[BsonIgnoreExtraElements]
internal sealed class IdeaDocument
{
    [BsonId]
    public long Id { get; set; }

    [BsonElement("topicId")]
    public long TopicId { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public IdeaStatus Status { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }

    public static IdeaDocument From(Idea idea) => new()
    {
        Id = idea.Id,
        TopicId = idea.TopicId,
        UserId = idea.UserId,
        Title = idea.Title,
        Description = idea.Description,
        Status = idea.Status,
        CreatedAt = idea.CreatedAt,
        UpdatedAt = idea.UpdatedAt
    };
}