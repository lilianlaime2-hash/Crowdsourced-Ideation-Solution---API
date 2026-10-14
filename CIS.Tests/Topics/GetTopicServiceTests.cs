using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.Application.Topics.GetTopic;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Moq;
using Xunit;

namespace CIS.Tests.Topics;

public class GetTopicServiceTests
{
    private static readonly string UserA = Guid.NewGuid().ToString("D");
    private static readonly string UserB = Guid.NewGuid().ToString("D");
    private static readonly DateTime CreatedAt = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITopicRepository> _topicRepository = new();
    private readonly Mock<IUsersApiClient> _usersApiClient = new();

    private GetTopicService CreateService() =>
        new(_topicRepository.Object, _usersApiClient.Object);

    private static Topic NewTopic(string userId, string title = "Topic title") =>
        new(title, "Topic description", userId, CreatedAt);

    private void GivenAuthorNames(Dictionary<string, string> names)
    {
        IReadOnlyDictionary<string, string> readOnlyNames = names;
        _usersApiClient
            .Setup(c => c.GetNamesByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readOnlyNames);
    }
    [Fact]
    public async Task GetByIdAsync_TopicDoesNotExist_ReturnsNull()
    {
        _topicRepository
            .Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic?)null);

        var result = await CreateService().GetByIdAsync(99);

        Assert.Null(result);
        _usersApiClient.Verify(
            c => c.GetNamesByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_TopicIsDeleted_ReturnsNull()
    {
        var topic = NewTopic(UserA);
        topic.Delete(DateTime.UtcNow);

        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

        var result = await CreateService().GetByIdAsync(1);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_TopicExists_ReturnsTopicWithAuthorName()
    {
        var topic = NewTopic(UserA);

        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        GivenAuthorNames(new Dictionary<string, string> { [UserA] = "Ana" });

        var result = await CreateService().GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Topic title", result!.Title);
        Assert.Equal("Topic description", result.Description);
        Assert.Equal(TopicStatus.Active, result.Status);
        Assert.Equal("Ana", result.AuthorName);  
        Assert.Equal(CreatedAt, result.CreatedAt);
        Assert.Empty(result.Tags);
    }

    [Fact]
    public async Task GetByIdAsync_TopicWithTags_ReturnsTagNames()
    {
        var topic = NewTopic(UserA);
        topic.AddTag(new Tag("C#"));
        topic.AddTag(new Tag("dotnet"));

        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        GivenAuthorNames(new Dictionary<string, string> { [UserA] = "Ana" });

        var result = await CreateService().GetByIdAsync(1);

        Assert.Equal(new[] { "C#", "dotnet" }, result!.Tags);
    }

    [Fact]
    public async Task GetByIdAsync_AuthorNotResolved_UsesUnknownUser()
    {
        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic(UserA));
        GivenAuthorNames(new Dictionary<string, string>());

        var result = await CreateService().GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Unknown user", result!.AuthorName);
    }

    [Fact]
    public async Task GetByIdAsync_AsksUsersApiForTheTopicAuthor()
    {
        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic(UserA));
        GivenAuthorNames(new Dictionary<string, string> { [UserA] = "Ana" });

        await CreateService().GetByIdAsync(1);

        _usersApiClient.Verify(
            c => c.GetNamesByIdsAsync(
                It.Is<IEnumerable<string>>(ids => ids.Single() == UserA),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task GetAllAsync_NoTopics_ReturnsEmptyList()
    {
        IReadOnlyList<Topic> noTopics = new List<Topic>();
        _topicRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(noTopics);
        GivenAuthorNames(new Dictionary<string, string>());

        var result = await CreateService().GetAllAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_SeveralTopics_ReturnsAllWithTheirAuthorNames()
    {
        IReadOnlyList<Topic> topics = new List<Topic>
        {
            NewTopic(UserA, "First"),
            NewTopic(UserB, "Second")
        };
        _topicRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);
        GivenAuthorNames(new Dictionary<string, string>
        {
            [UserA] = "Ana",
            [UserB] = "Beto"
        });

        var result = await CreateService().GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("First", result[0].Title);
        Assert.Equal("Ana", result[0].AuthorName);
        Assert.Equal("Second", result[1].Title);
        Assert.Equal("Beto", result[1].AuthorName);
    }

    [Fact]
    public async Task GetAllAsync_SomeAuthorsNotResolved_UsesUnknownUserOnlyForThose()
    {
        IReadOnlyList<Topic> topics = new List<Topic>
        {
            NewTopic(UserA, "First"),
            NewTopic(UserB, "Second")
        };
        _topicRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);
        GivenAuthorNames(new Dictionary<string, string> { [UserA] = "Ana" });

        var result = await CreateService().GetAllAsync();

        Assert.Equal("Ana", result[0].AuthorName);
        Assert.Equal("Unknown user", result[1].AuthorName);
    }
    [Fact]
    public async Task GetPagedAsync_ValidPage_ReturnsTopicsWithMetadata()
    {
        // Arrange
        IReadOnlyList<Topic> topics = new List<Topic>
    {
        NewTopic(UserA, "First Topic"),
        NewTopic(UserB, "Second Topic")
    };

        _topicRepository
            .Setup(r => r.GetPagedAsync(
                2,
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Topic>(topics, 5));

        GivenAuthorNames(new Dictionary<string, string>
        {
            [UserA] = "Ana",
            [UserB] = "Beto"
        });

        // Act
        var result = await CreateService().GetPagedAsync(2, 2);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);

        Assert.Equal("First Topic", result.Items[0].Title);
        Assert.Equal("Ana", result.Items[0].AuthorName);

        Assert.Equal("Second Topic", result.Items[1].Title);
        Assert.Equal("Beto", result.Items[1].AuthorName);
    }
    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetPagedAsync_InvalidParameters_ThrowsException(
    int page,
    int pageSize)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().GetPagedAsync(page, pageSize));

        // Verify
        _topicRepository.Verify(
            r => r.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task GetPagedAsync_EmptyPage_ReturnsEmptyCollection()
    {
        // Arrange
        IReadOnlyList<Topic> noTopics = new List<Topic>();

        _topicRepository
            .Setup(r => r.GetPagedAsync(
                99,
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Topic>(noTopics, 5));

        // Act
        var result = await CreateService().GetPagedAsync(99, 2);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);

        Assert.Equal(99, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);

        // Verify
        _usersApiClient.Verify(
            c => c.GetNamesByIdsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}