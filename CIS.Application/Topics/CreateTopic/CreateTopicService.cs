using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Domain.Entities;
using CIS.Domain.Policies;

namespace CIS.Application.Topics.CreateTopic
{
    public sealed class CreateTopicService : ICreateTopicService
    {
        private readonly ITopicRepository _topicRepository;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _timeProvider;
        private readonly ITagRepository _tagRepository;
        private readonly IUsersApiClient _usersApiClient;

        public CreateTopicService(
            ITopicRepository topicRepository,
            ICurrentUser currentUser,
            IUsersApiClient usersApiClient,
            TimeProvider timeProvider,
            ITagRepository tagRepository)
        {
            _topicRepository = topicRepository;
            _currentUser = currentUser;
            _usersApiClient = usersApiClient;
            _timeProvider = timeProvider;
            _tagRepository = tagRepository;
        }

        public async Task<TopicResponse> ExecuteAsync(CreateTopicRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var userId = _currentUser.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("An authenticated user is required.");
            }

            var validatedTags = TopicTagsValidator.Validate(request.Tags);

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var topic = new Topic(
                title: request.Title,
                description: request.Description,
                userId: userId,
                createdAt: now
            );
            var authorNames = await _usersApiClient.GetNamesByIdsAsync(
                [userId],
                cancellationToken);

            foreach (var tagName in validatedTags)
            {
                var (_, normalizedName) = TagNamePolicy.ValidateAndNormalize(tagName);

                var tag = await _tagRepository.GetByNormalizedNameAsync(normalizedName, cancellationToken);

                if (tag is null)
                {
                    tag = new Tag(tagName);
                }

                topic.AddTag(tag);
            }

            var createdTopic = await _topicRepository.AddAsync(topic, cancellationToken
            );

            return new TopicResponse
            {
                Id = createdTopic.Id,
                Title = createdTopic.Title,
                Description = createdTopic.Description,
                Status = createdTopic.Status,
                AuthorName = authorNames.TryGetValue(createdTopic.UserId, out var authorName)
                                ? authorName : "Unknown user",
                Tags = createdTopic.TopicTags.Select(topicTag => topicTag.Tag.Name).ToList(),
                CreatedAt = createdTopic.CreatedAt,
                UpdatedAt = createdTopic.UpdatedAt
            };
        }
    }
}
