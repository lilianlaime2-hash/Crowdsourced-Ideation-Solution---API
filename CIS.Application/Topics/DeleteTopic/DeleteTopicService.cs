using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Domain.Enums;

namespace CIS.Application.Topics.DeleteTopic;

public sealed class DeleteTopicService : IDeleteTopicService
{
    private readonly ITopicRepository _topicRepository;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;


    public DeleteTopicService(ITopicRepository topicRepository, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _topicRepository = topicRepository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<bool> ExecuteAsync(long id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("An authenticated user is required");
        }

        var topic = await _topicRepository.GetByIdAsync(id, cancellationToken);

        if (topic is null || topic.Status == TopicStatus.Deleted)
        {
            return false;
        }

        if (!string.Equals(topic.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        topic.Delete(now);

        await _topicRepository.UpdateAsync(topic, cancellationToken);
        return true;
    }
}