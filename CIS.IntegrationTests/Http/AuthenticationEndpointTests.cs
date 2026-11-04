using System.Net;
using System.Net.Http.Json;
using CIS.Domain.Enums;
using CIS.IntegrationTests.Infrastructure;

namespace CIS.IntegrationTests.Http;

[Collection(MongoDbCollection.Name)]
public sealed class AuthenticationEndpointTests(MongoDbFixture fixture) : HttpIntegrationTest(fixture)
{
    [Theory]
    [InlineData("GET", "/api/topics")]
    [InlineData("GET", "/api/topics/{topic}")]
    [InlineData("POST", "/api/topics")]
    [InlineData("PUT", "/api/topics/{topic}")]
    [InlineData("DELETE", "/api/topics/{topic}")]
    [InlineData("GET", "/api/topics/{topic}/ideas")]
    [InlineData("POST", "/api/topics/{topic}/ideas")]
    [InlineData("PUT", "/api/ideas/{idea}")]
    [InlineData("DELETE", "/api/topics/{topic}/ideas/{idea}")]
    [InlineData("POST", "/api/topics/{topic}/ideas/{idea}/votes")]
    [InlineData("DELETE", "/api/topics/{topic}/ideas/{idea}/votes")]
    [InlineData("GET", "/api/statistics/ideas/most-voted")]
    [InlineData("GET", "/api/statistics/topics/most-active")]
    public async Task Endpoint_MissingJwt_Returns401WithoutMutation(string method, string routeTemplate)
    {
        var topic = await SeedTopic();
        var idea = await SeedIdea(topic.Id);
        await Votes.AddAsync(TestData.Vote(idea.Id));
        var route = routeTemplate.Replace("{topic}", topic.Id.ToString()).Replace("{idea}", idea.Id.ToString());
        using var factory = Factory();
        using var client = Client(factory, userId: null);
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { title = "Rejected", description = "Must not persist" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1L, await Count("topics"));
        Assert.Equal(1L, await Count("ideas"));
        Assert.Equal(1L, await Count("votes"));
        var storedTopic = await Topics.GetByIdAsync(topic.Id);
        var storedIdea = await Ideas.GetByIdAsync(idea.Id);
        Assert.NotNull(storedTopic);
        Assert.NotNull(storedIdea);
        Assert.Equal(topic.Title, storedTopic.Title);
        Assert.Equal(idea.Title, storedIdea.Title);
        Assert.Equal(topic.UpdatedAt, storedTopic.UpdatedAt);
        Assert.Equal(idea.UpdatedAt, storedIdea.UpdatedAt);
        Assert.Equal(TopicStatus.Active, storedTopic.Status);
        Assert.Equal(IdeaStatus.Active, storedIdea.Status);
        Assert.True(await Votes.ExistsAsync(idea.Id, TestData.UserId));
    }
}
