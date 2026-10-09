using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Exceptions;
using CIS.Domain.Entities;
using CIS.Domain.Enums;

namespace CIS.Application.Ideas.VoteIdea;

public sealed class VoteIdeaService : IVoteIdeaService
{
    
    private readonly IIdeaRepository _ideaRepository;
    private readonly IVoteRepository _voteRepository;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public VoteIdeaService(IIdeaRepository ideaRepository, IVoteRepository voteRepository, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _ideaRepository = ideaRepository;
        _voteRepository = voteRepository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<VoteIdeaResponse?> ExecuteAsync(long topicId, long ideaId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("An authenticated user is required.");
        }
        
        var idea = await _ideaRepository.GetByIdAsync(ideaId, cancellationToken);
        if (idea is null || idea.TopicId != topicId || idea.Status == IdeaStatus.Deleted)
        {
            return null;
        }
        
        var alreadyVoted = await _voteRepository.ExistsAsync(idea.Id, userId, cancellationToken);
        if (alreadyVoted)
        {
            throw new ConflictException("The user has already voted for this idea.");
        }
        
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var vote = new Vote(idea.Id, userId, now);

        await _voteRepository.AddAsync(vote, cancellationToken);
        
        return new VoteIdeaResponse
        {
            Id = vote.Id,
            IdeaId = vote.IdeaId,
            UserId = vote.UserId,
            CreatedAt = vote.CreatedAt
        };
    }
}