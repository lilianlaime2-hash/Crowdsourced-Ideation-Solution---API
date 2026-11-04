using System.Net;
using CIS.IntegrationTests.Infrastructure;

namespace CIS.IntegrationTests.Http;

[Collection(MongoDbCollection.Name)]
public sealed class VoteEndpointTests(MongoDbFixture fixture) : HttpIntegrationTest(fixture)
{
    [Theory]
    [InlineData("new", HttpStatusCode.Created)]
    [InlineData("duplicate", HttpStatusCode.Conflict)]
    [InlineData("other-user", HttpStatusCode.Created)]
    [InlineData("missing-idea", HttpStatusCode.NotFound)]
    [InlineData("wrong-topic", HttpStatusCode.NotFound)]
    [InlineData("deleted-idea", HttpStatusCode.NotFound)]
    public async Task Vote_ExistenceAndUniqueness_PersistsOnlyValidVote(string scenario, HttpStatusCode expected)
    {
        var topic = await SeedTopic();
        var idea = await SeedIdea(topic.Id);
        if (scenario is "duplicate" or "other-user") await Votes.AddAsync(TestData.Vote(idea.Id));
        if (scenario == "deleted-idea")
        {
            idea.Delete(TestData.CreatedAt.AddHours(1));
            await Ideas.UpdateAsync(idea);
        }
        var userId = scenario == "other-user" ? TestData.OtherUserId : TestData.UserId;
        var ideaId = scenario == "missing-idea" ? long.MaxValue : idea.Id;
        var topicId = scenario == "wrong-topic" ? (await SeedTopic("Different topic")).Id : topic.Id;
        using var factory = Factory();
        using var client = Client(factory, userId);
        using var response = await client.PostAsync($"/api/topics/{topicId}/ideas/{ideaId}/votes", null);
        Assert.Equal(expected, response.StatusCode);
        var expectedCount = scenario == "other-user" ? 2L : scenario is "new" or "duplicate" ? 1L : 0L;
        Assert.Equal(expectedCount, await Count("votes"));
        if (expected == HttpStatusCode.Created)
        {
            var body = await Body(response);
            var stored = await Votes.GetAsync(idea.Id, userId);
            Assert.NotNull(stored);
            Assert.Equal(stored.Id, body.GetProperty("id").GetInt64());
            Assert.Equal(idea.Id, body.GetProperty("ideaId").GetInt64());
            Assert.Equal(userId, body.GetProperty("userId").GetString());
        }
        if (scenario is "duplicate" or "other-user") Assert.True(await Votes.ExistsAsync(idea.Id, TestData.UserId));
    }

    [Theory]
    [InlineData("own-vote", HttpStatusCode.NoContent)]
    [InlineData("no-vote", HttpStatusCode.NotFound)]
    [InlineData("other-user-vote", HttpStatusCode.NotFound)]
    [InlineData("wrong-topic", HttpStatusCode.NotFound)]
    [InlineData("missing-idea", HttpStatusCode.NotFound)]
    public async Task DeleteVote_ExistenceAndOwnership_PreservesOtherUsersVote(string scenario, HttpStatusCode expected)
    {
        var topic = await SeedTopic();
        var idea = await SeedIdea(topic.Id);
        // Distinguish an empty vote collection from another user's existing vote.
        // Whenever present, that other user's vote must survive even successful deletion.
        if (scenario != "no-vote") await Votes.AddAsync(TestData.Vote(idea.Id, TestData.OtherUserId));
        if (scenario is "own-vote" or "wrong-topic" or "missing-idea") await Votes.AddAsync(TestData.Vote(idea.Id));
        var topicId = scenario == "wrong-topic" ? (await SeedTopic("Different topic")).Id : topic.Id;
        var ideaId = scenario == "missing-idea" ? long.MaxValue : idea.Id;
        using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.DeleteAsync($"/api/topics/{topicId}/ideas/{ideaId}/votes");
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(scenario != "no-vote", await Votes.ExistsAsync(idea.Id, TestData.OtherUserId));
        Assert.Equal(scenario is "wrong-topic" or "missing-idea", await Votes.ExistsAsync(idea.Id, TestData.UserId));
        Assert.Equal(scenario == "no-vote" ? 0L : scenario is "wrong-topic" or "missing-idea" ? 2L : 1L, await Count("votes"));
    }
}
