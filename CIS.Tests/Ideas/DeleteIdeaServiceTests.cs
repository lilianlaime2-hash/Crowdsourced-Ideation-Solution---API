using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Ideas.DeleteIdea;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Moq;
using Xunit;

namespace CIS.Tests.Ideas;

public class DeleteIdeaServiceTests
{
    private readonly Mock<IIdeaRepository> _ideaRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact]
    public async Task ExecuteAsync_OwnerDeletesIdea_SoftDeletesAndReturnsTrue()
    {
        // Arrange
        long topicId = 1;
        long ideaId = 10;
        string ownerId = Guid.NewGuid().ToString("D");
        var idea = new Idea(topicId, "Idea title", "Idea description", ownerId, DateTime.UtcNow);

        _currentUser
            .Setup(u => u.UserId)
            .Returns(ownerId);

        _ideaRepository
            .Setup(r => r.GetByIdAsync(ideaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(idea);

        var service = new DeleteIdeaService(
            _ideaRepository.Object,
            _currentUser.Object,
            TimeProvider.System);

        // Act
        var result = await service.ExecuteAsync(topicId, ideaId);

        // Assert
        Assert.True(result);
        Assert.Equal(IdeaStatus.Deleted, idea.Status);
    }
}
