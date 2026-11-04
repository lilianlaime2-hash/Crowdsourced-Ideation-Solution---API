using System.Net;
using CIS.Domain.Enums;
using CIS.IntegrationTests.Infrastructure;

namespace CIS.IntegrationTests.Http;

[Collection(MongoDbCollection.Name)]
public sealed class ResourceEndpointTests(MongoDbFixture fixture) : HttpIntegrationTest(fixture)
{
    [Theory]
    [InlineData("existing", HttpStatusCode.OK)]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("deleted", HttpStatusCode.NotFound)]
    [InlineData("other-reader", HttpStatusCode.OK)]
    public async Task TopicDetail_ExistenceAndVisibility(string scenario, HttpStatusCode expected)
    {
        var topic = await SeedTopic();
        if (scenario == "deleted")
        {
            topic.Delete(TestData.CreatedAt.AddHours(1));
            await Topics.UpdateAsync(topic);
        }
        using var factory = Factory();
        using var client = Client(factory, scenario == "other-reader" ? TestData.OtherUserId : TestData.UserId);
        using var response = await client.GetAsync($"/api/topics/{(scenario == "missing" ? long.MaxValue : topic.Id)}");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await Body(response);
            Assert.Equal(topic.Id, body.GetProperty("id").GetInt64());
            Assert.Equal(topic.Title, body.GetProperty("title").GetString());
            Assert.Equal(topic.Description, body.GetProperty("description").GetString());
        }
        var stored = await Topics.GetByIdAsync(topic.Id);
        Assert.NotNull(stored);
        Assert.Equal(topic.Status, stored.Status);
        Assert.Equal(topic.UpdatedAt, stored.UpdatedAt);
    }

    [Theory]
    [InlineData("topic", "owner", HttpStatusCode.NoContent)]
    [InlineData("topic", "missing", HttpStatusCode.NotFound)]
    [InlineData("topic", "other-owner", HttpStatusCode.NotFound)]
    [InlineData("topic", "already-deleted", HttpStatusCode.NotFound)]
    [InlineData("idea", "owner", HttpStatusCode.NoContent)]
    [InlineData("idea", "missing", HttpStatusCode.NotFound)]
    [InlineData("idea", "other-owner", HttpStatusCode.NotFound)]
    [InlineData("idea", "already-deleted", HttpStatusCode.NotFound)]
    [InlineData("idea", "wrong-topic", HttpStatusCode.NotFound)]
    public async Task Delete_ExistenceAndOwnership_PersistsOnlyAuthorizedSoftDelete(string resource, string scenario, HttpStatusCode expected)
    {
        var topic = await SeedTopic();
        var idea = await SeedIdea(topic.Id);
        if (scenario == "already-deleted")
        {
            if (resource == "topic")
            {
                topic.Delete(TestData.CreatedAt.AddHours(1));
                await Topics.UpdateAsync(topic);
            }
            else
            {
                idea.Delete(TestData.CreatedAt.AddHours(1));
                await Ideas.UpdateAsync(idea);
            }
        }
        var resourceId = scenario == "missing" ? long.MaxValue : resource == "topic" ? topic.Id : idea.Id;
        var topicId = scenario == "wrong-topic" ? (await SeedTopic("Different topic")).Id : topic.Id;
        var route = resource == "topic" ? $"/api/topics/{resourceId}" : $"/api/topics/{topicId}/ideas/{resourceId}";
        using var factory = Factory();
        using var client = Client(factory, scenario == "other-owner" ? TestData.OtherUserId : TestData.UserId);
        using var response = await client.DeleteAsync(route);
        Assert.Equal(expected, response.StatusCode);
        if (resource == "topic")
        {
            var stored = await Topics.GetByIdAsync(topic.Id);
            Assert.NotNull(stored);
            Assert.Equal(scenario is "owner" or "already-deleted" ? TopicStatus.Deleted : TopicStatus.Active, stored.Status);
            if (scenario == "owner") Assert.True(stored.UpdatedAt > stored.CreatedAt);
            else Assert.Equal(topic.UpdatedAt, stored.UpdatedAt);
        }
        else
        {
            var stored = await Ideas.GetByIdAsync(idea.Id);
            Assert.NotNull(stored);
            Assert.Equal(scenario is "owner" or "already-deleted" ? IdeaStatus.Deleted : IdeaStatus.Active, stored.Status);
            if (scenario == "owner") Assert.True(stored.UpdatedAt > stored.CreatedAt);
            else Assert.Equal(idea.UpdatedAt, stored.UpdatedAt);
        }
        Assert.Equal(1L, await Count("ideas"));
        Assert.Equal(scenario == "wrong-topic" ? 2L : 1L, await Count("topics"));
    }
}
