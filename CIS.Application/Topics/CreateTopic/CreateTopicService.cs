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

        public CreateTopicService(
            ITopicRepository topicRepository,
            ICurrentUser currentUser,
            TimeProvider timeProvider,
            ITagRepository tagRepository)
        {
            _topicRepository = topicRepository;
            _currentUser = currentUser;
            _timeProvider = timeProvider;
            _tagRepository = tagRepository;
        }

        public async Task<CreateTopicResponse> ExecuteAsync(CreateTopicRequest request, CancellationToken cancellationToken = default)
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

            return new CreateTopicResponse
            {
                Id = createdTopic.Id,
                Title = createdTopic.Title,
                Description = createdTopic.Description,
                Status = createdTopic.Status,
                UserId = createdTopic.UserId,
                Tags = createdTopic.TopicTags.Select(topicTag => topicTag.Tag.Name).ToList(),
                CreatedAt = createdTopic.CreatedAt,
                UpdatedAt = createdTopic.UpdatedAt
            };
        }
    }
}
