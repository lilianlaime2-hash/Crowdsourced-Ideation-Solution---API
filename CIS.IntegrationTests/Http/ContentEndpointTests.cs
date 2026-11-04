using System.Net;
using System.Net.Http.Json;
using CIS.Domain.Enums;
using CIS.IntegrationTests.Infrastructure;

namespace CIS.IntegrationTests.Http;

[Collection(MongoDbCollection.Name)]
public sealed class ContentEndpointTests(MongoDbFixture fixture) : HttpIntegrationTest(fixture)
{
    // Each row is independently discovered and gets its own MongoIntegrationTest database.
    [Theory]
    [InlineData("topic", "POST", 1, 1)]
    [InlineData("topic", "POST", 150, 1000)]
    [InlineData("topic", "POST", 75, 500)]
    [InlineData("topic", "PUT", 1, 1)]
    [InlineData("topic", "PUT", 150, 1000)]
    [InlineData("topic", "PUT", 75, 500)]
    [InlineData("idea", "POST", 1, 1)]
    [InlineData("idea", "POST", 150, 1000)]
    [InlineData("idea", "POST", 75, 500)]
    [InlineData("idea", "PUT", 1, 1)]
    [InlineData("idea", "PUT", 150, 1000)]
    [InlineData("idea", "PUT", 75, 500)]
    public async Task Content_ValidBounds_PersistsExactContent(string resource, string method, int titleLength, int descriptionLength)
    {
        var topic = await SeedTopic();
        var idea = resource == "idea" && method == "PUT" ? await SeedIdea(topic.Id) : null;
        var title = new string('T', titleLength);
        var description = new string('D', descriptionLength);
        var route = Route(resource, method, topic.Id, idea?.Id);
        using var factory = Factory();
        using var client = Client(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), route)
        {
            Content = JsonContent.Create(new { title, description })
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(method == "POST" ? HttpStatusCode.Created : HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        var id = body.GetProperty("id").GetInt64();
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal(description, body.GetProperty("description").GetString());
        Assert.Equal("Integration author", body.GetProperty("authorName").GetString());
        if (resource == "topic")
        {
            var stored = await Topics.GetByIdAsync(id);
            Assert.NotNull(stored);
            Assert.Equal(title, stored.Title);
            Assert.Equal(description, stored.Description);
            Assert.Equal(TestData.UserId, stored.UserId);
            Assert.Equal(TopicStatus.Active, stored.Status);
            if (method == "PUT") Assert.True(stored.UpdatedAt > stored.CreatedAt);
            Assert.Equal(method == "POST" ? 2L : 1L, await Count("topics"));
        }
        else
        {
            var stored = await Ideas.GetByIdAsync(id);
            Assert.NotNull(stored);
            Assert.Equal(title, stored.Title);
            Assert.Equal(description, stored.Description);
            Assert.Equal(TestData.UserId, stored.UserId);
            Assert.Equal(topic.Id, stored.TopicId);
            Assert.Equal(IdeaStatus.Active, stored.Status);
            if (method == "PUT") Assert.True(stored.UpdatedAt > stored.CreatedAt);
            Assert.Equal(1L, await Count("ideas"));
        }
    }

    public static TheoryData<string, string, int, int> InvalidContent => BuildInvalidContent();

    private static TheoryData<string, string, int, int> BuildInvalidContent()
    {
        var data = new TheoryData<string, string, int, int>();
        foreach (var resource in new[] { "topic", "idea" })
            foreach (var method in new[] { "POST", "PUT" })
                foreach (var (title, description) in new[] { (0, 10), (151, 10), (10, 0), (10, 1001) })
                    data.Add(resource, method, title, description);
        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidContent))]
    public async Task Content_InvalidBounds_Returns400WithoutMutation(string resource, string method, int titleLength, int descriptionLength)
    {
        var topic = await SeedTopic();
        var idea = await SeedIdea(topic.Id);
        using var factory = Factory();
        using var client = Client(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), Route(resource, method, topic.Id, idea.Id))
        {
            Content = JsonContent.Create(new { title = new string('T', titleLength), description = new string('D', descriptionLength) })
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1L, await Count("topics"));
        Assert.Equal(1L, await Count("ideas"));
        var storedTopic = await Topics.GetByIdAsync(topic.Id);
        var storedIdea = await Ideas.GetByIdAsync(idea.Id);
        Assert.NotNull(storedTopic);
        Assert.NotNull(storedIdea);
        Assert.Equal(topic.Title, storedTopic.Title);
        Assert.Equal(topic.Description, storedTopic.Description);
        Assert.Equal(topic.UpdatedAt, storedTopic.UpdatedAt);
        Assert.Equal(idea.Title, storedIdea.Title);
        Assert.Equal(idea.Description, storedIdea.Description);
        Assert.Equal(idea.UpdatedAt, storedIdea.UpdatedAt);
    }

    [Theory]
    [InlineData("topic", "other-owner", HttpStatusCode.Forbidden)]
    [InlineData("topic", "missing", HttpStatusCode.NotFound)]
    [InlineData("idea", "other-owner", HttpStatusCode.Forbidden)]
    [InlineData("idea", "missing", HttpStatusCode.NotFound)]
    public async Task Update_OwnershipAndExistence_DoNotMutate(string resource, string scenario, HttpStatusCode expected)
    {
        var topic = await SeedTopic();
        var idea = await SeedIdea(topic.Id);
        var id = scenario == "missing" ? long.MaxValue : resource == "topic" ? topic.Id : idea.Id;
        using var factory = Factory();
        using var client = Client(factory, scenario == "other-owner" ? TestData.OtherUserId : TestData.UserId);
        using var response = await client.PutAsJsonAsync(resource == "topic" ? $"/api/topics/{id}" : $"/api/ideas/{id}",
            new { title = "Unauthorized update", description = "Must not persist" });
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(topic.Title, (await Topics.GetByIdAsync(topic.Id))!.Title);
        Assert.Equal(idea.Title, (await Ideas.GetByIdAsync(idea.Id))!.Title);
    }

    [Fact]
    public async Task CreateIdea_MissingTopic_Returns404WithoutInsertion()
    {
        using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.PostAsJsonAsync($"/api/topics/{long.MaxValue}/ideas", new { title = "Valid", description = "Valid" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0L, await Count("ideas"));
    }

    private static string Route(string resource, string method, long topicId, long? ideaId) =>
        resource == "topic"
            ? method == "POST" ? "/api/topics" : $"/api/topics/{topicId}"
            : method == "POST" ? $"/api/topics/{topicId}/ideas" : $"/api/ideas/{ideaId}";
}
