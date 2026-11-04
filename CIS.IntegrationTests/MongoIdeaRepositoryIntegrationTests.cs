using CIS.Application.Ideas.BrowseIdeas;
using CIS.Domain.Enums;
using CIS.IntegrationTests.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.IntegrationTests;

[Collection(MongoDbCollection.Name)]
public sealed class MongoIdeaRepositoryIntegrationTests(MongoDbFixture fixture) : MongoIntegrationTest(fixture)
{
    [Fact]
    public async Task AddAsync_NewIdea_PersistsFieldsWithGeneratedId()
    {
        var topic = await Topics.AddAsync(TestData.Topic());
        var idea = await Ideas.AddAsync(TestData.Idea(topic.Id));

        Assert.True(idea.Id > 0);
        var stored = await Documents("ideas").Find(new BsonDocument("_id", idea.Id)).SingleAsync();
        Assert.Equal(topic.Id, stored["topicId"].ToInt64());
        Assert.Equal(TestData.UserId, stored["userId"].AsString);
        Assert.Equal("Integration idea", stored["title"].AsString);
        Assert.Equal("An idea persisted in real MongoDB.", stored["description"].AsString);
        Assert.Equal("Active", stored["status"].AsString);
        Assert.Equal(TestData.CreatedAt, stored["createdAt"].ToUniversalTime());
        Assert.Equal(TestData.CreatedAt, stored["updatedAt"].ToUniversalTime());
    }

    [Fact]
    public async Task GetAsync_MultipleTopics_FiltersAndPaginatesVisibleIdeasWithStableOrder()
    {
        var topic = await Topics.AddAsync(TestData.Topic());
        var otherTopic = await Topics.AddAsync(TestData.Topic("Other topic"));
        var first = await Ideas.AddAsync(TestData.Idea(topic.Id, "First idea"));
        var second = await Ideas.AddAsync(TestData.Idea(topic.Id, "Second idea"));
        await Ideas.AddAsync(TestData.Idea(otherTopic.Id, "Unrelated idea"));
        var deleted = await Ideas.AddAsync(TestData.Idea(topic.Id, "Deleted idea"));
        deleted.Delete(TestData.CreatedAt.AddMinutes(1));
        await Ideas.UpdateAsync(deleted);

        var loaded = await Ideas.GetByIdAsync(first.Id);
        Assert.NotNull(loaded);
        Assert.Equal(first.Id, loaded.Id);
        Assert.Equal(topic.Id, loaded.TopicId);
        Assert.Equal("First idea", loaded.Title);
        Assert.Equal("An idea persisted in real MongoDB.", loaded.Description);
        Assert.Equal(TestData.UserId, loaded.UserId);
        Assert.Equal(IdeaStatus.Active, loaded.Status);
        Assert.Equal(TestData.CreatedAt, loaded.CreatedAt);
        Assert.Equal(TestData.CreatedAt, loaded.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Null(await Ideas.GetByIdAsync(long.MaxValue));
        // Unpaged repository lookup includes soft-deleted records by design.
        Assert.Equal(new[] { deleted.Id, second.Id, first.Id },
            (await Ideas.GetByTopicIdAsync(topic.Id)).Select(i => i.Id));

        var firstPage = await Ideas.GetPagedByTopicIdAsync(topic.Id, 1, 1);
        var secondPage = await Ideas.GetPagedByTopicIdAsync(topic.Id, 2, 1);
        Assert.Equal(2L, firstPage.TotalItems);
        Assert.Equal(second.Id, Assert.Single(firstPage.Items).Id);
        Assert.Equal(2L, secondPage.TotalItems);
        Assert.Equal(first.Id, Assert.Single(secondPage.Items).Id);
        var missingTopic = await Ideas.GetPagedByTopicIdAsync(long.MaxValue, 1, 10);
        Assert.Empty(missingTopic.Items);
        Assert.Equal(0L, missingTopic.TotalItems);
        var filtered = await Ideas.BrowseAsync(topic.Id, new IdeaBrowseOptions { Search = "second", UserId = TestData.UserId });
        Assert.Equal(1L, filtered.TotalItems);
        Assert.Equal(second.Id, Assert.Single(filtered.Items).Id);
    }

    [Fact]
    public async Task UpdateAsync_ChangedThenDeletedIdea_PersistsUpdateAndSoftDeletion()
    {
        var topic = await Topics.AddAsync(TestData.Topic());
        var idea = await Ideas.AddAsync(TestData.Idea(topic.Id));
        var updatedAt = TestData.CreatedAt.AddMinutes(1);
        idea.Update("Updated title", "Updated description", updatedAt);
        await Ideas.UpdateAsync(idea);

        var updated = await Ideas.GetByIdAsync(idea.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated title", updated.Title);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(updatedAt, updated.UpdatedAt);
        Assert.Equal(TestData.CreatedAt, updated.CreatedAt);
        Assert.Equal(topic.Id, updated.TopicId);
        Assert.Equal(TestData.UserId, updated.UserId);
        Assert.Equal(IdeaStatus.Active, updated.Status);

        var deletedAt = updatedAt.AddMinutes(1);
        updated.Delete(deletedAt);
        await Ideas.UpdateAsync(updated);

        var stored = await Documents("ideas").Find(new BsonDocument("_id", idea.Id)).SingleAsync();
        Assert.Equal("Deleted", stored["status"].AsString);
        Assert.Equal(deletedAt, stored["updatedAt"].ToUniversalTime());
        Assert.Equal("Updated title", stored["title"].AsString);
        var visible = await Ideas.GetPagedByTopicIdAsync(topic.Id, 1, 10);
        Assert.Empty(visible.Items);
        Assert.Equal(0L, visible.TotalItems);
        var browsed = await Ideas.BrowseAsync(topic.Id, new IdeaBrowseOptions());
        Assert.Empty(browsed.Items);
        Assert.Equal(0L, browsed.TotalItems);
    }
}
