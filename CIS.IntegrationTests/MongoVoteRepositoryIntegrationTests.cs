using CIS.IntegrationTests.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.IntegrationTests;

[Collection(MongoDbCollection.Name)]
public sealed class MongoVoteRepositoryIntegrationTests(MongoDbFixture fixture) : MongoIntegrationTest(fixture)
{
    [Fact]
    public async Task AddAndGetAsync_NewVote_PersistsAndFindsExactIdeaUserPair()
    {
        var topic = await Topics.AddAsync(TestData.Topic());
        var idea = await Ideas.AddAsync(TestData.Idea(topic.Id));
        Assert.False(await Votes.ExistsAsync(idea.Id, TestData.UserId));
        Assert.Null(await Votes.GetAsync(idea.Id, TestData.UserId));

        var vote = await Votes.AddAsync(TestData.Vote(idea.Id));

        Assert.True(vote.Id > 0);
        Assert.True(await Votes.ExistsAsync(idea.Id, TestData.UserId));
        Assert.False(await Votes.ExistsAsync(idea.Id, TestData.OtherUserId));
        Assert.False(await Votes.ExistsAsync(long.MaxValue, TestData.UserId));
        var loaded = await Votes.GetAsync(idea.Id, TestData.UserId);
        Assert.NotNull(loaded);
        Assert.Equal(vote.Id, loaded.Id);
        Assert.Equal(idea.Id, loaded.IdeaId);
        Assert.Equal(TestData.UserId, loaded.UserId);
        Assert.Equal(TestData.CreatedAt, loaded.CreatedAt);
        var stored = await Documents("votes").Find(new BsonDocument("_id", vote.Id)).SingleAsync();
        Assert.Equal(idea.Id, stored["ideaId"].ToInt64());
        Assert.Equal(TestData.UserId, stored["userId"].AsString);
        Assert.Equal(TestData.CreatedAt, stored["createdAt"].ToUniversalTime());
    }

    [Fact]
    public async Task AddAsync_50ConcurrentVotesForSamePair_StoresOneAndRejectsDuplicates()
    {
        var topic = await Topics.AddAsync(TestData.Topic());
        var idea = await Ideas.AddAsync(TestData.Idea(topic.Id));
        var otherIdea = await Ideas.AddAsync(TestData.Idea(topic.Id, "Other idea"));

        var accepted = await ConcurrentRequests.RunAsync(50, async _ =>
        {
            try
            {
                await Votes.AddAsync(TestData.Vote(idea.Id));
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        });

        Assert.Equal(1, accepted.Count(success => success));
        Assert.Equal(49, accepted.Count(success => !success));
        Assert.Equal(1L, await Documents("votes").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        var duplicate = await Assert.ThrowsAsync<MongoWriteException>(() => Votes.AddAsync(TestData.Vote(idea.Id)));
        Assert.Equal(ServerErrorCategory.DuplicateKey, duplicate.WriteError.Category);

        // The unique constraint is on the pair, not either field individually.
        await Votes.AddAsync(TestData.Vote(idea.Id, TestData.OtherUserId));
        await Votes.AddAsync(TestData.Vote(otherIdea.Id));
        Assert.Equal(3L, await Documents("votes").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task DeleteAsync_ExistingVote_RemovesOnlySelectedVoteAndAllowsVotingAgain()
    {
        var topic = await Topics.AddAsync(TestData.Topic());
        var idea = await Ideas.AddAsync(TestData.Idea(topic.Id));
        var vote = await Votes.AddAsync(TestData.Vote(idea.Id));
        var otherVote = await Votes.AddAsync(TestData.Vote(idea.Id, TestData.OtherUserId));

        await Votes.DeleteAsync(vote);

        Assert.False(await Votes.ExistsAsync(idea.Id, TestData.UserId));
        Assert.Null(await Votes.GetAsync(idea.Id, TestData.UserId));
        var remaining = await Documents("votes").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal(otherVote.Id, remaining["_id"].ToInt64());
        Assert.True(await Votes.ExistsAsync(idea.Id, TestData.OtherUserId));
        var replacement = await Votes.AddAsync(TestData.Vote(idea.Id));
        Assert.True(replacement.Id > vote.Id);
        Assert.True(await Votes.ExistsAsync(idea.Id, TestData.UserId));
    }
}
