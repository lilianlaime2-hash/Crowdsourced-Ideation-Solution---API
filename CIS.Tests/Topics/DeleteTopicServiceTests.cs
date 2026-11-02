using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Topics.DeleteTopic;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Moq;
using Xunit;

namespace CIS.Tests.Topics;

public class DeleteTopicServiceTests
{
    private readonly Mock<ITopicRepository> _topicRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact]
    public async Task ExecuteAsync_OwnerDeletesTopic_SoftDeletesAndReturnsTrue()
    {
        // Arrange
        long topicId = 1;
        string ownerId = Guid.NewGuid().ToString("D");
        var topic = new Topic("Topic title", "Topic description", ownerId, DateTime.UtcNow);

        _currentUser
            .Setup(u => u.UserId)
            .Returns(ownerId);

        _topicRepository
            .Setup(r => r.GetByIdAsync(topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

        var service = new DeleteTopicService(
            _topicRepository.Object,
            _currentUser.Object,
            TimeProvider.System);

        // Act
        var result = await service.ExecuteAsync(topicId);

        // Assert
        Assert.True(result);
        Assert.Equal(TopicStatus.Deleted, topic.Status);
    }
}
