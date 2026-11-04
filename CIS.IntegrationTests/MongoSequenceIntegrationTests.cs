using CIS.IntegrationTests.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.IntegrationTests;

[Collection(MongoDbCollection.Name)]
public sealed class MongoSequenceIntegrationTests(MongoDbFixture fixture) : MongoIntegrationTest(fixture)
{
    [Fact]
    public async Task NextAsync_ExistingCounter_GeneratesNextIds()
    {
        await Documents("counters").InsertOneAsync(new BsonDocument
        {
            { "_id", "topics" }, { "seq", new BsonInt64(5) }
        });

        Assert.Equal(6L, await Sequence.NextAsync("topics"));
        Assert.Equal(7L, await Sequence.NextAsync("topics"));

        var counter = await Documents("counters").Find(new BsonDocument("_id", "topics")).SingleAsync();
        Assert.Equal(7L, counter["seq"].ToInt64());
    }

    [Fact]
    public async Task NextAsync_NewCounterWith50ConcurrentCalls_GeneratesUniqueConsecutiveIds()
    {
        var ids = await ConcurrentRequests.RunAsync(50, _ => Sequence.NextAsync("ideas"));

        Assert.Equal(50, ids.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 50).Select(id => (long)id), ids.Order());
        var counter = await Documents("counters").Find(new BsonDocument("_id", "ideas")).SingleAsync();
        Assert.Equal(50L, counter["seq"].ToInt64());
        Assert.Equal(1L, await Documents("counters").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
}
