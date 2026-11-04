using CIS.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace CIS.DataAccess.MongoDB.Documents;

[BsonIgnoreExtraElements]
internal sealed class VoteDocument
{
    [BsonId]
    public long Id { get; set; }

    [BsonElement("ideaId")]
    public long IdeaId { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    public static VoteDocument From(Vote vote) => new()
    {
        Id = vote.Id,
        IdeaId = vote.IdeaId,
        UserId = vote.UserId,
        CreatedAt = vote.CreatedAt
    };
}