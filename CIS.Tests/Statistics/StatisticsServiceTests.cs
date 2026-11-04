using CIS.Application.Abstractions.Persistence;
using CIS.Application.Statistics.GetMostActiveTopics;
using CIS.Application.Statistics.GetMostVotedIdeas;
using Moq;
using Xunit;

namespace CIS.Tests.Statistics;

public sealed class StatisticsServiceTests
{
    [Fact]
    public async Task MostVoted_MapsRankedRowsEnrichesAuthorsAndKeepsPaginationTotals()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var repository = new Mock<IStatisticsRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetMostVotedIdeasAsync(2, 2, token)).ReturnsAsync(
            new StatisticsPage<MostVotedIdeaData>([
                new(10, 7, "known", "First", "First description", 8),
                new(11, 9, "missing", "Second", "Second description", 6)], 7));
        var users = new Mock<IUsersApiClient>(MockBehavior.Strict);
        users.Setup(u => u.GetNamesByIdsAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "known", "missing" })), token))
            .ReturnsAsync(new Dictionary<string, string> { ["known"] = "Alice" });

        var result = await new GetMostVotedIdeasService(repository.Object, users.Object).ExecuteAsync(2, 2, token);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(7, result.TotalItems);
        Assert.Equal(4, result.TotalPages);
        Assert.Collection(result.Items, first =>
        {
            Assert.Equal(10, first.IdeaId);
            Assert.Equal(7, first.TopicId);
            Assert.Equal("First", first.Title);
            Assert.Equal("First description", first.Description);
            Assert.Equal("Alice", first.AuthorName);
            Assert.Equal(8, first.VoteCount);
        }, second =>
        {
            Assert.Equal(11, second.IdeaId);
            Assert.Equal("Unknown user", second.AuthorName);
            Assert.Equal(6, second.VoteCount);
        });
    }

    [Fact]
    public async Task MostActive_ComputesActivityScoreAndFallsBackForMissingAuthor()
    {
        var repository = new Mock<IStatisticsRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetMostActiveTopicsAsync(0, 10, default)).ReturnsAsync(
            new StatisticsPage<MostActiveTopicData>([
                new(3, "known", "First", 4, 6), new(5, "missing", "Second", 1, 2)], 2));
        var users = new Mock<IUsersApiClient>();
        users.Setup(u => u.GetNamesByIdsAsync(It.IsAny<IEnumerable<string>>(), default))
            .ReturnsAsync(new Dictionary<string, string> { ["known"] = "Alice" });

        var result = await new GetMostActiveTopicsService(repository.Object, users.Object).ExecuteAsync(1, 10);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        Assert.Collection(result.Items, first =>
        {
            Assert.Equal(3, first.TopicId);
            Assert.Equal("First", first.Title);
            Assert.Equal("Alice", first.AuthorName);
            Assert.Equal(4, first.IdeaCount);
            Assert.Equal(6, first.VoteCount);
            Assert.Equal(10, first.ActivityScore);
        }, second =>
        {
            Assert.Equal(5, second.TopicId);
            Assert.Equal("Unknown user", second.AuthorName);
            Assert.Equal(3, second.ActivityScore);
        });
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(10L)]
    public async Task EmptyPages_PreserveTotalsAndDoNotRequireUserService(long total)
    {
        var repository = new Mock<IStatisticsRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetMostVotedIdeasAsync(10, 10, default))
            .ReturnsAsync(new StatisticsPage<MostVotedIdeaData>([], total));
        repository.Setup(r => r.GetMostActiveTopicsAsync(10, 10, default))
            .ReturnsAsync(new StatisticsPage<MostActiveTopicData>([], total));
        var users = new Mock<IUsersApiClient>(MockBehavior.Strict);
        var ideas = await new GetMostVotedIdeasService(repository.Object, users.Object).ExecuteAsync(2, 10);
        var topics = await new GetMostActiveTopicsService(repository.Object, users.Object).ExecuteAsync(2, 10);
        Assert.Empty(ideas.Items);
        Assert.Empty(topics.Items);
        Assert.Equal(total, ideas.TotalItems);
        Assert.Equal(total, topics.TotalItems);
        Assert.Equal(total == 0 ? 0 : 1, ideas.TotalPages);
        Assert.Equal(total == 0 ? 0 : 1, topics.TotalPages);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1000001, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task InvalidPagination_IsRejectedBeforeAccessingDependencies(int page, int size)
    {
        var repository = new Mock<IStatisticsRepository>(MockBehavior.Strict);
        var users = new Mock<IUsersApiClient>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMostVotedIdeasService(repository.Object, users.Object).ExecuteAsync(page, size));
        await Assert.ThrowsAsync<ArgumentException>(() => new GetMostActiveTopicsService(repository.Object, users.Object).ExecuteAsync(page, size));
    }
}
