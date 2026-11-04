using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.DataAccess.MongoDB;

public sealed class MongoSequence
{
    private readonly IMongoCollection<BsonDocument> _counters;

    public MongoSequence(IMongoDatabase database)
    {
        _counters = database.GetCollection<BsonDocument>(MongoCollections.Counters);
    }

    public async Task<long> NextAsync(string name, CancellationToken cancellationToken = default)
    {
        var counter = await _counters.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", name),
            Builders<BsonDocument>.Update.Inc("seq", 1L),
            new FindOneAndUpdateOptions<BsonDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        return counter["seq"].ToInt64();
    }
}