using CIS.DataAccess.MongoDB.Repositories;
using CIS.IntegrationTests.Infrastructure;

namespace CIS.IntegrationTests;

[Collection(MongoDbCollection.Name)]
public sealed class MongoStatisticsRepositoryIntegrationTests(MongoDbFixture fixture) : MongoIntegrationTest(fixture)
{
    [Fact]
    public async Task GetStatisticsAsync_RealData_RanksPaginatesAndExcludesDeletedAndOrphanRecords()
    {
        // Insert lower-ranked records first so natural/_id order cannot satisfy ranking assertions.
        var secondTopic = await Topics.AddAsync(TestData.Topic("Second topic"));
        var deletedTopic = await Topics.AddAsync(TestData.Topic("Deleted topic"));
        var firstTopic = await Topics.AddAsync(TestData.Topic("Most active topic"));
        var zeroVoteIdea = await Ideas.AddAsync(TestData.Idea(firstTopic.Id, "No votes"));
        var secondIdea = await Ideas.AddAsync(TestData.Idea(secondTopic.Id, "One vote"));
        var firstIdea = await Ideas.AddAsync(TestData.Idea(firstTopic.Id, "Two votes"));
        var deletedIdea = await Ideas.AddAsync(TestData.Idea(firstTopic.Id, "Deleted idea"));
        var hiddenByTopic = await Ideas.AddAsync(TestData.Idea(deletedTopic.Id, "Deleted parent"));
        var orphan = await Ideas.AddAsync(TestData.Idea(long.MaxValue, "Missing parent"));

        await Votes.AddAsync(TestData.Vote(firstIdea.Id));
        await Votes.AddAsync(TestData.Vote(firstIdea.Id, TestData.OtherUserId));
        await Votes.AddAsync(TestData.Vote(secondIdea.Id));
        foreach (var hiddenId in new[] { deletedIdea.Id, hiddenByTopic.Id, orphan.Id })
        {
            for (var i = 0; i < 3; i++)
                await Votes.AddAsync(TestData.Vote(hiddenId, Guid.NewGuid().ToString("D")));
        }
        deletedIdea.Delete(TestData.CreatedAt.AddMinutes(1));
        await Ideas.UpdateAsync(deletedIdea);
        deletedTopic.Delete(TestData.CreatedAt.AddMinutes(1));
        await Topics.UpdateAsync(deletedTopic);

        var statistics = new MongoStatisticsRepository(Database);
        var mostVoted = await statistics.GetMostVotedIdeasAsync(0, 10);
        Assert.Equal(3L, mostVoted.TotalItems);
        Assert.Equal(new[] { firstIdea.Id, secondIdea.Id, zeroVoteIdea.Id }, mostVoted.Items.Select(i => i.IdeaId));
        Assert.Equal(new[] { 2, 1, 0 }, mostVoted.Items.Select(i => i.VoteCount));
        var winner = mostVoted.Items[0];
        Assert.Equal(firstTopic.Id, winner.TopicId);
        Assert.Equal(TestData.UserId, winner.UserId);
        Assert.Equal("Two votes", winner.Title);
        Assert.Equal("An idea persisted in real MongoDB.", winner.Description);
        var ideaPage = await statistics.GetMostVotedIdeasAsync(1, 1);
        Assert.Equal(3L, ideaPage.TotalItems);
        Assert.Equal(secondIdea.Id, Assert.Single(ideaPage.Items).IdeaId);
        var emptyIdeas = await statistics.GetMostVotedIdeasAsync(3, 1);
        Assert.Empty(emptyIdeas.Items);
        Assert.Equal(3L, emptyIdeas.TotalItems);

        var mostActive = await statistics.GetMostActiveTopicsAsync(0, 10);
        Assert.Equal(2L, mostActive.TotalItems);
        Assert.Equal(new[] { firstTopic.Id, secondTopic.Id }, mostActive.Items.Select(t => t.TopicId));
        Assert.Equal(new[] { 2, 1 }, mostActive.Items.Select(t => t.IdeaCount));
        Assert.Equal(new[] { 2, 1 }, mostActive.Items.Select(t => t.VoteCount));
        Assert.Equal("Most active topic", mostActive.Items[0].Title);
        Assert.Equal(TestData.UserId, mostActive.Items[0].UserId);
        var topicPage = await statistics.GetMostActiveTopicsAsync(1, 1);
        Assert.Equal(2L, topicPage.TotalItems);
        Assert.Equal(secondTopic.Id, Assert.Single(topicPage.Items).TopicId);
        var emptyTopics = await statistics.GetMostActiveTopicsAsync(2, 1);
        Assert.Empty(emptyTopics.Items);
        Assert.Equal(2L, emptyTopics.TotalItems);
    }
}
