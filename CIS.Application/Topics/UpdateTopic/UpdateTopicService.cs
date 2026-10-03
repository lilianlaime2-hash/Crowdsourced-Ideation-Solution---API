using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Exceptions;

namespace CIS.Application.Topics.UpdateTopic
{
    public sealed class UpdateTopicService : IUpdateTopicService
    {
        private readonly ITopicRepository _topicRepository;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _timeProvider;
        private readonly IUsersApiClient _usersApiClient;

        public UpdateTopicService(
            ITopicRepository topicRepository,
            ICurrentUser currentUser,
            TimeProvider timeProvider,
            IUsersApiClient usersApiClient)
        {
            _topicRepository = topicRepository;
            _currentUser = currentUser;
            _timeProvider = timeProvider;
            _usersApiClient = usersApiClient;
        }

        public async Task<TopicResponse?> ExecuteAsync(
            long id,
            UpdateTopicRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var userId = _currentUser.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("An authenticated user is required.");
            }

            var topic = await _topicRepository.GetByIdAsync(id, cancellationToken);

            if (topic is null)
            {
                return null;
            }
            
            if (!string.Equals(topic.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenOperationException("Only the topic owner can update it.");
            }

            topic.Update(
                request.Title,
                request.Description,
                _timeProvider.GetUtcNow().UtcDateTime);

            await _topicRepository.UpdateAsync(topic, cancellationToken);

            var authorNames = await _usersApiClient.GetNamesByIdsAsync([topic.UserId], cancellationToken);

            return TopicMapper.ToResponse(topic, authorNames);
        }
    }
}