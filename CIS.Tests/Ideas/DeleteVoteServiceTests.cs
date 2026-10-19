using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Ideas.DeleteVote;
using CIS.Domain.Entities;
using Moq;
using Xunit;

namespace CIS.Tests.Ideas;

public class DeleteVoteServiceTests
{
    private readonly Mock<IIdeaRepository> _ideaRepository = new();
    private readonly Mock<IVoteRepository> _voteRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact]
    public async Task ExecuteAsync_UserRemovesVote_DeletesVoteAndReturnsTrue()
    {
        // Arrange
        long topicId = 1;
        long ideaId = 10;
        string authorId = Guid.NewGuid().ToString("D");
        string voterId = Guid.NewGuid().ToString("D");
        var idea = new Idea(topicId, "Idea title", "Idea description", authorId, DateTime.UtcNow);
        var vote = new Vote(ideaId, voterId, DateTime.UtcNow);

        // Id has a private setter, so we set it with reflection
        typeof(Idea).GetProperty(nameof(Idea.Id))!.SetValue(idea, ideaId);

        _currentUser
            .Setup(u => u.UserId)
            .Returns(voterId);

        _ideaRepository
            .Setup(r => r.GetByIdAsync(ideaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(idea);

        _voteRepository
            .Setup(r => r.GetAsync(ideaId, voterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vote);

        var service = new DeleteVoteService(
            _ideaRepository.Object,
            _voteRepository.Object,
            _currentUser.Object);

        // Act
        var result = await service.ExecuteAsync(topicId, ideaId);

        // Assert
        Assert.True(result);
        _voteRepository.Verify(
            r => r.DeleteAsync(vote, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
