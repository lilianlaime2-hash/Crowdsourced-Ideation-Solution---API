using CIS.Application.Topics.BrowseTopics;
using CIS.DataAccess.MongoDB.Repositories;
using Tag = CIS.Domain.Entities.Tag;
using CIS.Domain.Enums;
using CIS.IntegrationTests.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.IntegrationTests;

[Collection(MongoDbCollection.Name)]
public sealed class MongoTopicRepositoryIntegrationTests(MongoDbFixture fixture) : MongoIntegrationTest(fixture)
{
    [Fact]
    public async Task AddAsync_NewTopic_PersistsFieldsWithGeneratedId()
    {
        var topic = await Topics.AddAsync(TestData.Topic());

        Assert.True(topic.Id > 0);
        var stored = await Documents("topics").Find(new BsonDocument("_id", topic.Id)).SingleAsync();
        Assert.Equal("MongoDB integration", stored["title"].AsString);
        Assert.Equal("A topic persisted in real MongoDB.", stored["description"].AsString);
        Assert.Equal(TestData.UserId, stored["userId"].AsString);
        Assert.Equal("Active", stored["status"].AsString);
        Assert.Equal(TestData.CreatedAt, stored["createdAt"].ToUniversalTime());
        Assert.Equal(TestData.CreatedAt, stored["updatedAt"].ToUniversalTime());
        Assert.Empty(stored["tagIds"].AsBsonArray);
    }

    [Fact]
    public async Task GetAsync_PersistedTopics_RehydratesAndPaginatesWithoutDeletedTopics()
    {
        var older = await Topics.AddAsync(TestData.Topic("Older topic"));
        var newer = await Topics.AddAsync(TestData.Topic("Newer topic", TestData.CreatedAt.AddMinutes(1)));
        var deleted = await Topics.AddAsync(TestData.Topic("Deleted topic", TestData.CreatedAt.AddMinutes(2)));
        deleted.Delete(TestData.CreatedAt.AddMinutes(3));
        await Topics.UpdateAsync(deleted);

        var loaded = await Topics.GetByIdAsync(older.Id);
        Assert.NotNull(loaded);
        Assert.Equal(older.Id, loaded.Id);
        Assert.Equal("Older topic", loaded.Title);
        Assert.Equal("A topic persisted in real MongoDB.", loaded.Description);
        Assert.Equal(TestData.UserId, loaded.UserId);
        Assert.Equal(TopicStatus.Active, loaded.Status);
        Assert.Equal(TestData.CreatedAt, loaded.CreatedAt);
        Assert.Equal(TestData.CreatedAt, loaded.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Null(await Topics.GetByIdAsync(long.MaxValue));

        Assert.Equal(new[] { newer.Id, older.Id }, (await Topics.GetAllAsync()).Select(t => t.Id));
        var firstPage = await Topics.GetPagedAsync(1, 1);
        var secondPage = await Topics.GetPagedAsync(2, 1);
        var emptyPage = await Topics.GetPagedAsync(3, 1);
        Assert.Equal(2L, firstPage.TotalItems);
        Assert.Equal(newer.Id, Assert.Single(firstPage.Items).Id);
        Assert.Equal(2L, secondPage.TotalItems);
        Assert.Equal(older.Id, Assert.Single(secondPage.Items).Id);
        Assert.Equal(2L, emptyPage.TotalItems);
        Assert.Empty(emptyPage.Items);
    }

    [Fact]
    public async Task AddAndBrowseAsync_SharedTag_ReusesStoredTagAndFiltersNormalizedName()
    {
        var topic = TestData.Topic("C# persistence");
        topic.AddTag(new Tag("  MongoDB  "));
        topic.AddTag(new Tag("C#"));
        await Topics.AddAsync(topic);

        var tags = new MongoTagRepository(Database);
        var shared = await tags.GetByNormalizedNameAsync("MONGODB");
        Assert.NotNull(shared);
        Assert.True(shared.Id > 0);
        Assert.Equal("MongoDB", shared.Name);
        Assert.Equal("MONGODB", shared.NormalizedName);
        Assert.Null(await tags.GetByNormalizedNameAsync("MISSING"));

        var second = TestData.Topic("Another topic");
        second.AddTag(shared);
        await Topics.AddAsync(second);
        await Topics.AddAsync(TestData.Topic("Untagged topic"));

        var loaded = await Topics.GetByIdAsync(topic.Id);
        Assert.NotNull(loaded);
        Assert.Equal(new[] { "C#", "MongoDB" }, loaded.TopicTags.Select(t => t.Tag.Name).Order());
        Assert.Contains(loaded.TopicTags, t => t.Tag.Id == shared.Id && t.Tag.NormalizedName == "MONGODB");
        Assert.Equal(2L, await Documents("tags").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        var stored = await Documents("topics").Find(new BsonDocument("_id", second.Id)).SingleAsync();
        Assert.Equal(shared.Id, Assert.Single(stored["tagIds"].AsBsonArray).ToInt64());

        var filtered = await Topics.BrowseAsync(new TopicBrowseOptions { Tag = " mongodb " });
        Assert.Equal(2L, filtered.TotalItems);
        Assert.Equal(new[] { topic.Id, second.Id }, filtered.Items.Select(t => t.Id).Order());
        var searched = await Topics.BrowseAsync(new TopicBrowseOptions { Tag = " c# ", Search = "C#" });
        Assert.Equal(1L, searched.TotalItems);
        Assert.Equal(topic.Id, Assert.Single(searched.Items).Id);
        var missing = await Topics.BrowseAsync(new TopicBrowseOptions { Tag = "missing" });
        Assert.Empty(missing.Items);
        Assert.Equal(0L, missing.TotalItems);
    }
}
