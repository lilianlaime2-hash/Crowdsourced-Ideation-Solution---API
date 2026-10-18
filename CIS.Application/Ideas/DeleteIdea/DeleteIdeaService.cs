using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Domain.Enums;

namespace CIS.Application.Ideas.DeleteIdea;

public sealed class DeleteIdeaService : IDeleteIdeaService
{
    private readonly IIdeaRepository _ideaRepository;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public DeleteIdeaService(IIdeaRepository ideaRepository, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _ideaRepository = ideaRepository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<bool> ExecuteAsync(long topicId, long id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("An authenticated user is required");
        }

        var idea = await _ideaRepository.GetByIdAsync(id, cancellationToken);

        if (idea is null || idea.TopicId != topicId || idea.Status == IdeaStatus.Deleted)
        {
            return false;
        }

        if (!string.Equals(idea.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        idea.Delete(now);

        await _ideaRepository.UpdateAsync(idea, cancellationToken);
        return true;
    }
}