using CIS.Application.Abstractions.Persistence;
using CIS.Application.Ideas.GetIdeas;
using CIS.Domain.Entities;
using Moq;
using Xunit;

namespace CIS.Tests.Ideas;

public class GetIdeasServiceTests
{
    private static readonly string UserA = Guid.NewGuid().ToString("D");
    private static readonly string UserB = Guid.NewGuid().ToString("D");

    private readonly Mock<IIdeaRepository> _ideaRepository = new();
    private readonly Mock<ITopicRepository> _topicRepository = new();
    private readonly Mock<IUsersApiClient> _usersApiClient = new();

    private GetIdeasService CreateService() =>
        new(_ideaRepository.Object, _topicRepository.Object, _usersApiClient.Object);

    private static Topic NewTopic() =>
        new("Topic title", "Topic description", UserA, DateTime.UtcNow);

    private static Idea NewIdea(string userId, string title = "Idea title") =>
        new(1, title, "Idea description", userId, DateTime.UtcNow);

    [Fact]
    public async Task GetByTopicIdAsync_TopicDoesNotExist_ReturnsNull()
    {
        _topicRepository
            .Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic?)null);

        var result = await CreateService().GetByTopicIdAsync(99);

        Assert.Null(result);
        _ideaRepository.Verify(
            r => r.GetByTopicIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByTopicIdAsync_TopicIsDeleted_ReturnsNull()
    {
        var topic = NewTopic();
        topic.Delete(DateTime.UtcNow);

        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

        var result = await CreateService().GetByTopicIdAsync(1);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByTopicIdAsync_TopicWithoutIdeas_ReturnsEmptyList()
    {
        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> noIdeas = new List<Idea>();
        _ideaRepository
            .Setup(r => r.GetByTopicIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(noIdeas);

        var result = await CreateService().GetByTopicIdAsync(1);

        Assert.NotNull(result);
        Assert.Empty(result!);
        _usersApiClient.Verify(
            c => c.GetNamesByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByTopicIdAsync_TopicWithIdeas_ReturnsIdeasWithAuthorNames()
    {
        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> ideas = new List<Idea>
        {
            NewIdea(UserA, "First"),
            NewIdea(UserB, "Second")
        };
        _ideaRepository
            .Setup(r => r.GetByTopicIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ideas);

        IReadOnlyDictionary<string, string> names = new Dictionary<string, string>
        {
            [UserA] = "Ana",
            [UserB] = "Beto"
        };
        _usersApiClient
            .Setup(c => c.GetNamesByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(names);

        var result = await CreateService().GetByTopicIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("First", result[0].Title);
        Assert.Equal("Ana", result[0].AuthorName);
        Assert.Equal("Second", result[1].Title);
        Assert.Equal("Beto", result[1].AuthorName);
    }

    [Fact]
    public async Task GetByTopicIdAsync_AuthorNotResolved_UsesUnknownUser()
    {
        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> ideas = new List<Idea> { NewIdea(UserA) };
        _ideaRepository
            .Setup(r => r.GetByTopicIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ideas);
        
        IReadOnlyDictionary<string, string> noNames = new Dictionary<string, string>();
        _usersApiClient
            .Setup(c => c.GetNamesByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(noNames);

        var result = await CreateService().GetByTopicIdAsync(1);

        Assert.Single(result!);
        Assert.Equal("Unknown user", result![0].AuthorName);
    }
}