using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Exceptions;
using CIS.Application.Ideas.CreateIdea;
using CIS.Application.Ideas.UpdateIdea;
using CIS.Domain.Entities;
using Moq;
using Xunit;

namespace CIS.Tests.Ideas;

public class UpdateIdeaServiceTests
{
    private static readonly string OwnerId = Guid.NewGuid().ToString("D");
    private static readonly string OtherUserId = Guid.NewGuid().ToString("D");
    private static readonly DateTime CreatedAt = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IIdeaRepository> _ideaRepository = new();
    private readonly Mock<ITopicRepository> _topicRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IUsersApiClient> _usersApiClient = new();
    private readonly Mock<TimeProvider> _timeProvider = new();

    public UpdateIdeaServiceTests()
    {
        _timeProvider
            .Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(Now));

        IReadOnlyDictionary<string, string> names = new Dictionary<string, string>
        {
            [OwnerId] = "Josefina"
        };
        _usersApiClient
            .Setup(c => c.GetNamesByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(names);
    }

    private UpdateIdeaService CreateService() =>
        new(
            _ideaRepository.Object,
            _topicRepository.Object,
            _currentUser.Object,
            _usersApiClient.Object,
            _timeProvider.Object);

    private static Topic NewTopic() =>
        new("Topic title", "Topic description", OwnerId, CreatedAt);

    private static Idea NewIdea(string userId = null!) =>
        new(1, "Old title", "Old description", userId ?? OwnerId, CreatedAt);

    private static CreateIdeaRequest Request(string title, string description) =>
        new() { Title = title, Description = description };

    private void GivenAuthenticatedUser(string? userId) =>
        _currentUser.Setup(c => c.UserId).Returns(userId);

    private void GivenIdea(Idea? idea) =>
        _ideaRepository
            .Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(idea);

    private void GivenTopic(Topic? topic) =>
        _topicRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

    [Fact]
    public async Task ExecuteAsync_NoAuthenticatedUser_ThrowsUnauthorizedAccess()
    {
        GivenAuthenticatedUser(null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().ExecuteAsync(10, Request("New title", "New description")));

        _ideaRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Idea>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_IdeaDoesNotExist_ReturnsNull()
    {
        GivenAuthenticatedUser(OwnerId);
        GivenIdea(null);

        var result = await CreateService().ExecuteAsync(10, Request("New title", "New description"));

        Assert.Null(result);
        _ideaRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Idea>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TopicDoesNotExist_ReturnsNull()
    {
        GivenAuthenticatedUser(OwnerId);
        GivenIdea(NewIdea());
        GivenTopic(null);

        var result = await CreateService().ExecuteAsync(10, Request("New title", "New description"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_TopicIsDeleted_ReturnsNull()
    {
        var topic = NewTopic();
        topic.Delete(Now);

        GivenAuthenticatedUser(OwnerId);
        GivenIdea(NewIdea());
        GivenTopic(topic);

        var result = await CreateService().ExecuteAsync(10, Request("New title", "New description"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_UserIsNotTheOwner_ThrowsForbidden()
    {
        GivenAuthenticatedUser(OtherUserId);
        GivenIdea(NewIdea(OwnerId));
        GivenTopic(NewTopic());

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            CreateService().ExecuteAsync(10, Request("New title", "New description")));

        _ideaRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Idea>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_UpdatesIdeaAndReturnsResponse()
    {
        var idea = NewIdea();

        GivenAuthenticatedUser(OwnerId);
        GivenIdea(idea);
        GivenTopic(NewTopic());

        var result = await CreateService().ExecuteAsync(
            10, Request("  New title  ", "  New description  "));

        Assert.NotNull(result);
        Assert.Equal("New title", result!.Title);
        Assert.Equal("New description", result.Description);
        Assert.Equal("Josefina", result.AuthorName);
        Assert.Equal(Now, result.UpdatedAt);
        Assert.Equal(CreatedAt, result.CreatedAt);

        _ideaRepository.Verify(
            r => r.UpdateAsync(idea, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("", "New description")]
    [InlineData("   ", "New description")]
    [InlineData("New title", "")]
    [InlineData("New title", "   ")]
    public async Task ExecuteAsync_InvalidData_ThrowsArgumentExceptionAndKeepsPreviousData(
        string title, string description)
    {
        var idea = NewIdea();

        GivenAuthenticatedUser(OwnerId);
        GivenIdea(idea);
        GivenTopic(NewTopic());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().ExecuteAsync(10, Request(title, description)));

        Assert.Equal("Old title", idea.Title);
        Assert.Equal("Old description", idea.Description);
        Assert.Equal(CreatedAt, idea.UpdatedAt);

        _ideaRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Idea>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TitleTooLong_ThrowsArgumentExceptionAndKeepsPreviousData()
    {
        var idea = NewIdea();
        var longTitle = new string('a', 151);

        GivenAuthenticatedUser(OwnerId);
        GivenIdea(idea);
        GivenTopic(NewTopic());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().ExecuteAsync(10, Request(longTitle, "New description")));

        Assert.Equal("Old title", idea.Title);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateService().ExecuteAsync(10, null!));
    }
}