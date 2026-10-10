using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Exceptions;
using CIS.Application.Ideas.CreateIdea;
using CIS.Domain.Enums;

namespace CIS.Application.Ideas.UpdateIdea;

public sealed class UpdateIdeaService : IUpdateIdeaService
{
    private readonly IIdeaRepository _ideaRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUsersApiClient _usersApiClient;
    private readonly TimeProvider _timeProvider;

    public UpdateIdeaService(
        IIdeaRepository ideaRepository,
        ITopicRepository topicRepository,
        ICurrentUser currentUser,
        IUsersApiClient usersApiClient,
        TimeProvider timeProvider)
    {
        _ideaRepository = ideaRepository;
        _topicRepository = topicRepository;
        _currentUser = currentUser;
        _usersApiClient = usersApiClient;
        _timeProvider = timeProvider;
    }

    public async Task<CreateIdeaResponse?> ExecuteAsync(
        long id,
        CreateIdeaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("An authenticated user is required.");
        }

        var idea = await _ideaRepository.GetByIdAsync(id, cancellationToken);

        if (idea is null)
        {
            return null;
        }
        
        var topic = await _topicRepository.GetByIdAsync(idea.TopicId, cancellationToken);

        if (topic is null || topic.Status == TopicStatus.Deleted)
        {
            return null;
        }
        
        if (!string.Equals(idea.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenOperationException("Only the idea owner can update it.");
        }

        idea.Update(
            request.Title,
            request.Description,
            _timeProvider.GetUtcNow().UtcDateTime);

        await _ideaRepository.UpdateAsync(idea, cancellationToken);

        var authorNames = await _usersApiClient.GetNamesByIdsAsync(
            [idea.UserId],
            cancellationToken);

        return IdeaMapper.ToResponse(idea, authorNames);
    }
}