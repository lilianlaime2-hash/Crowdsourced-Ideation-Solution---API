using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.MongoDB.Documents;
using CIS.Domain.Entities;
using MongoDB.Driver;
using Tag = CIS.Domain.Entities.Tag;

namespace CIS.DataAccess.MongoDB.Repositories;

public sealed class MongoTagRepository : ITagRepository
{
    private readonly IMongoCollection<TagDocument> _tags;

    public MongoTagRepository(IMongoDatabase database)
    {
        _tags = database.GetCollection<TagDocument>(MongoCollections.Tags);
    }

    public async Task<Tag?> GetByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
    {
        var doc = await _tags
            .Find(t => t.NormalizedName == normalizedName)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : Rehydrator.ToTag(doc);
    }
}