using CIS.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace CIS.DataAccess.MongoDB.Documents;

[BsonIgnoreExtraElements]
internal sealed class TagDocument
{
    [BsonId]
    public long Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("normalizedName")]
    public string NormalizedName { get; set; } = string.Empty;

    public static TagDocument From(Tag tag) => new()
    {
        Id = tag.Id,
        Name = tag.Name,
        NormalizedName = tag.NormalizedName
    };
}