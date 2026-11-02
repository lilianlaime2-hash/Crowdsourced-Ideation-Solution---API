using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Exceptions;
using CIS.Application.Topics.UpdateTopic;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Moq;
using Xunit;

namespace CIS.Tests.Topics;

public class UpdateTopicServiceTests
{
    private static readonly string OwnerId = Guid.NewGuid().ToString("D");
    private static readonly string OtherUserId = Guid.NewGuid().ToString("D");
    private static readonly DateTime CreatedAt = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITopicRepository> _topicRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IUsersApiClient> _usersApiClient = new();
    private readonly Mock<TimeProvider> _timeProvider = new();

    public UpdateTopicServiceTests()
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
    private UpdateTopicService CreateService() =>
        new(
            _topicRepository.Object,
            _currentUser.Object,
            _timeProvider.Object,
            _usersApiClient.Object);
    private static Topic NewTopic() =>
        new("Old title", "Old description", OwnerId, CreatedAt);

    private static UpdateTopicRequest Request(string title, string description) =>
        new() { Title = title, Description = description };

    private void GivenAuthenticatedUser(string? userId) =>
        _currentUser.Setup(c => c.UserId).Returns(userId);

    private void GivenTopic(Topic? topic) =>
        _topicRepository
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

    private void VerifyNothingSaved() =>
        _topicRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Topic>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateService().ExecuteAsync(1, null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_NoAuthenticatedUser_ThrowsUnauthorizedAccess(string? userId)
    {
        GivenAuthenticatedUser(userId);
        GivenTopic(NewTopic());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().ExecuteAsync(1, Request("New title", "New description")));

        VerifyNothingSaved();
    }

    [Fact]
    public async Task ExecuteAsync_TopicDoesNotExist_ReturnsNull()
    {
        GivenAuthenticatedUser(OwnerId);
        GivenTopic(null);

        var result = await CreateService().ExecuteAsync(1, Request("New title", "New description"));

        Assert.Null(result);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task ExecuteAsync_TopicIsDeleted_ReturnsNull()
    {
        var topic = NewTopic();
        topic.Delete(Now);

        GivenAuthenticatedUser(OwnerId);
        GivenTopic(topic);

        var result = await CreateService().ExecuteAsync(1, Request("New title", "New description"));

        Assert.Null(result);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task ExecuteAsync_UserIsNotTheOwner_ThrowsForbiddenAndKeepsData()
    {
        var topic = NewTopic();

        GivenAuthenticatedUser(OtherUserId);
        GivenTopic(topic);

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            CreateService().ExecuteAsync(1, Request("New title", "New description")));

        Assert.Equal("Old title", topic.Title);
        Assert.Equal("Old description", topic.Description);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task ExecuteAsync_OwnerIdWithDifferentCase_IsStillTheOwner()
    {
        GivenAuthenticatedUser(OwnerId.ToUpperInvariant());
        GivenTopic(NewTopic());

        var result = await CreateService().ExecuteAsync(1, Request("New title", "New description"));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_UpdatesTopicAndReturnsResponse()
    {
        var topic = NewTopic();
        topic.AddTag(new Tag("dotnet"));

        GivenAuthenticatedUser(OwnerId);
        GivenTopic(topic);

        var result = await CreateService().ExecuteAsync(
            1, Request("  New title  ", "  New description  "));

        Assert.NotNull(result);
        Assert.Equal("New title", result!.Title);
        Assert.Equal("New description", result.Description);
        Assert.Equal("Josefina", result.AuthorName);
        Assert.Equal(TopicStatus.Active, result.Status);
        Assert.Equal(Now, result.UpdatedAt);
        Assert.Equal(CreatedAt, result.CreatedAt);
        Assert.Equal(new[] { "dotnet" }, result.Tags);

        _topicRepository.Verify(
            r => r.UpdateAsync(topic, It.IsAny<CancellationToken>()),
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
        var topic = NewTopic();

        GivenAuthenticatedUser(OwnerId);
        GivenTopic(topic);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().ExecuteAsync(1, Request(title, description)));

        Assert.Equal("Old title", topic.Title);
        Assert.Equal("Old description", topic.Description);
        Assert.Equal(CreatedAt, topic.UpdatedAt);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task ExecuteAsync_TitleTooLong_ThrowsArgumentExceptionAndKeepsPreviousData()
    {
        var topic = NewTopic();

        GivenAuthenticatedUser(OwnerId);
        GivenTopic(topic);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().ExecuteAsync(1, Request(new string('a', 151), "New description")));

        Assert.Equal("Old title", topic.Title);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task ExecuteAsync_DescriptionTooLong_ThrowsArgumentExceptionAndKeepsPreviousData()
    {
        var topic = NewTopic();

        GivenAuthenticatedUser(OwnerId);
        GivenTopic(topic);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().ExecuteAsync(1, Request("New title", new string('a', 1001))));

        Assert.Equal("Old description", topic.Description);
        VerifyNothingSaved();
    }
}