using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Application.Ideas.BrowseIdeas;
using CIS.Application.Ideas.GetIdeas;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
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
    [Fact]
    public async Task GetPagedByTopicIdAsync_ValidPage_ReturnsMetadata()
    {
        // Arrange
        _topicRepository
            .Setup(r => r.GetByIdAsync(
                1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> ideas = new List<Idea>
    {
        NewIdea(UserA, "First Idea")
    };

        _ideaRepository
            .Setup(r => r.GetPagedByTopicIdAsync(
                1, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Idea>(ideas, 5));

        IReadOnlyDictionary<string, string> names =
            new Dictionary<string, string>
            {
                [UserA] = "Ana"
            };

        _usersApiClient
            .Setup(c => c.GetNamesByIdsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(names);

        // Act
        var result = await CreateService()
            .GetPagedByTopicIdAsync(1, 1, 2);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal("Ana", result.Items[0].AuthorName);
    }
    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetPagedByTopicIdAsync_InvalidParameters_ThrowsException(
    int page,
    int pageSize)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().GetPagedByTopicIdAsync(
                1,
                page,
                pageSize));

        // Verify
        _ideaRepository.Verify(
            r => r.GetPagedByTopicIdAsync(
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task GetPagedByTopicIdAsync_TopicDoesNotExist_ReturnsNull()
    {
        // Arrange
        _topicRepository
            .Setup(r => r.GetByIdAsync(
                99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic?)null);

        // Act
        var result = await CreateService()
            .GetPagedByTopicIdAsync(99, 1, 5);

        // Assert
        Assert.Null(result);

        _ideaRepository.Verify(
            r => r.GetPagedByTopicIdAsync(
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task GetPagedByTopicIdAsync_EmptyPage_ReturnsEmptyCollection()
    {
        // Arrange
        _topicRepository
            .Setup(r => r.GetByIdAsync(
                1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> noIdeas = new List<Idea>();

        _ideaRepository
            .Setup(r => r.GetPagedByTopicIdAsync(
                1, 99, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Idea>(noIdeas, 3));

        // Act
        var result = await CreateService()
            .GetPagedByTopicIdAsync(1, 99, 5);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
        Assert.Equal(99, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.TotalPages);

        _usersApiClient.Verify(
            c => c.GetNamesByIdsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task BrowseAsync_ValidFilters_ReturnsFilteredIdeas()
    {
        // Arrange
        var options = new IdeaBrowseOptions
        {
            Page = 1,
            PageSize = 2,
            Status = IdeaStatus.Active,
            UserId = UserA,
            Search = "software",
            SortBy = IdeaSortField.Title,
            SortDirection = SortDirection.Asc
        };

        _topicRepository
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> ideas = new List<Idea>
    {
        NewIdea(UserA, "Software Idea")
    };

        _ideaRepository
            .Setup(r => r.BrowseAsync(
                1,
                It.IsAny<IdeaBrowseOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Idea>(ideas, 1));

        IReadOnlyDictionary<string, string> names =
            new Dictionary<string, string>
            {
                [UserA] = "Ana"
            };

        _usersApiClient
            .Setup(c => c.GetNamesByIdsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(names);

        // Act
        var result = await CreateService().BrowseAsync(
            1,
            options);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result!.Items);

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);

        Assert.Equal("Software Idea", result.Items[0].Title);
        Assert.Equal("Ana", result.Items[0].AuthorName);

        // Verify
        _ideaRepository.Verify(
            r => r.BrowseAsync(
                1,
                It.Is<IdeaBrowseOptions>(o =>
                    o.Status == IdeaStatus.Active &&
                    o.UserId == UserA &&
                    o.Search == "software" &&
                    o.SortBy == IdeaSortField.Title &&
                    o.SortDirection == SortDirection.Asc &&
                    o.Page == 1 &&
                    o.PageSize == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(999, 1, 2, null)]
    [InlineData(1, 999, 2, null)]
    [InlineData(1, 1, 999, null)]
    [InlineData(1, 1, 2, "abc123")]
    public async Task BrowseAsync_InvalidFilters_ThrowsException(
        int status,
        int sortBy,
        int sortDirection,
        string? userId)
    {
        // Arrange
        var options = new IdeaBrowseOptions
        {
            Page = 1,
            PageSize = 5,
            Status = (IdeaStatus)status,
            UserId = userId,
            SortBy = (IdeaSortField)sortBy,
            SortDirection = (SortDirection)sortDirection
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().BrowseAsync(1, options));

        // Verify
        _ideaRepository.Verify(
            r => r.BrowseAsync(
                It.IsAny<long>(),
                It.IsAny<IdeaBrowseOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BrowseAsync_TopicNotFound_ReturnsNull()
    {
        // Arrange
        var options = new IdeaBrowseOptions
        {
            Page = 1,
            PageSize = 5,
            Status = IdeaStatus.Active
        };

        _topicRepository
            .Setup(r => r.GetByIdAsync(
                999,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic?)null);

        // Act
        var result = await CreateService().BrowseAsync(
            999,
            options);

        // Assert
        Assert.Null(result);

        // Verify
        _ideaRepository.Verify(
            r => r.BrowseAsync(
                It.IsAny<long>(),
                It.IsAny<IdeaBrowseOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BrowseAsync_DefaultSorting_UsesCreatedAtDescending()
    {
        // Arrange
        var options = new IdeaBrowseOptions();

        _topicRepository
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> ideas = new List<Idea>();

        _ideaRepository
            .Setup(r => r.BrowseAsync(
                1,
                It.IsAny<IdeaBrowseOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Idea>(ideas, 0));

        // Act
        var result = await CreateService().BrowseAsync(1, options);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result!.Items);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);

        // Verify default sorting
        _ideaRepository.Verify(
            r => r.BrowseAsync(
                1,
                It.Is<IdeaBrowseOptions>(o =>
                    o.SortBy == IdeaSortField.CreatedAt &&
                    o.SortDirection == SortDirection.Desc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(IdeaSortField.CreatedAt, SortDirection.Asc)]
    [InlineData(IdeaSortField.CreatedAt, SortDirection.Desc)]
    [InlineData(IdeaSortField.UpdatedAt, SortDirection.Asc)]
    [InlineData(IdeaSortField.UpdatedAt, SortDirection.Desc)]
    [InlineData(IdeaSortField.Title, SortDirection.Asc)]
    [InlineData(IdeaSortField.Title, SortDirection.Desc)]
    public async Task BrowseAsync_IdeaSortFields_SendsCorrectOptions(
        IdeaSortField sortBy,
        SortDirection direction)
    {
        // Arrange
        var options = new IdeaBrowseOptions
        {
            Page = 1,
            PageSize = 5,
            SortBy = sortBy,
            SortDirection = direction
        };

        _topicRepository
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewTopic());

        IReadOnlyList<Idea> ideas = new List<Idea>();

        _ideaRepository
            .Setup(r => r.BrowseAsync(
                1,
                It.IsAny<IdeaBrowseOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Idea>(ideas, 0));

        // Act
        var result = await CreateService().BrowseAsync(1, options);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result!.Items);

        _ideaRepository.Verify(
            r => r.BrowseAsync(
                1,
                It.Is<IdeaBrowseOptions>(o =>
                    o.SortBy == sortBy &&
                    o.SortDirection == direction &&
                    o.Page == 1 &&
                    o.PageSize == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}