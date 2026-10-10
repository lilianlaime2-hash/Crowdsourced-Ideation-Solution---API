using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Domain.Enums;

namespace CIS.Application.Ideas.DeleteVote;

public sealed class DeleteVoteService : IDeleteVoteService
{
    private readonly IIdeaRepository _ideaRepository;
    private readonly IVoteRepository _voteRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteVoteService(IIdeaRepository ideaRepository, IVoteRepository voteRepository, ICurrentUser currentUser)
    {
        _ideaRepository = ideaRepository;
        _voteRepository = voteRepository;
        _currentUser = currentUser;
    }

    public async Task<bool> ExecuteAsync(long topicId, long ideaId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("An authenticated user is required.");
        }

        var idea = await _ideaRepository.GetByIdAsync(ideaId, cancellationToken);
        if (idea is null || idea.TopicId != topicId || idea.Status == IdeaStatus.Deleted)
        {
            return false;
        }

        var vote = await _voteRepository.GetAsync(idea.Id, userId, cancellationToken);
        if (vote is null)
        {
            return false;
        }

        await _voteRepository.DeleteAsync(vote, cancellationToken);
        return true;
    }
}