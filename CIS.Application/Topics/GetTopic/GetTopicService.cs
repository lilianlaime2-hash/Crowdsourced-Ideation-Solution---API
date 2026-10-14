using CIS.Application.Abstractions.Persistence;
using CIS.Domain.Enums;
using CIS.Application.Common.Pagination;

namespace CIS.Application.Topics.GetTopic
{
    public sealed class GetTopicService : IGetTopicService
    {
        private readonly ITopicRepository _topicRepository;
        private readonly IUsersApiClient _usersApiClient;
        public GetTopicService(
            ITopicRepository topicRepository,
            IUsersApiClient usersApiClient)
        {
            _topicRepository = topicRepository;
            _usersApiClient = usersApiClient;
        }
        public async Task<TopicResponse?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var topic = await _topicRepository.GetByIdAsync(id, cancellationToken);

            if (topic is null || topic.Status == TopicStatus.Deleted)
            {
                return null;
            }

            var authorNames = await _usersApiClient.GetNamesByIdsAsync(
                [topic.UserId],
                cancellationToken);

            return TopicMapper.ToResponse(topic, authorNames);
        }
        public async Task<IReadOnlyList<TopicResponse>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var topics = await _topicRepository.GetAllAsync(cancellationToken);

            var authorNames = await _usersApiClient.GetNamesByIdsAsync(
                topics.Select(t => t.UserId),
                cancellationToken);

            return topics
                .Select(t => TopicMapper.ToResponse(t, authorNames))
                .ToList();
        }
        public async Task<PagedResponse<TopicResponse>> GetPagedAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            PaginationRules.Validate(page, pageSize);

            var result = await _topicRepository.GetPagedAsync(
                page,
                pageSize,
                cancellationToken);

            if (result.Items.Count == 0)
            {
                return PagedResponse<TopicResponse>.Create(
                    [],
                    page,
                    pageSize,
                    result.TotalItems);
            }

            var authorNames = await _usersApiClient.GetNamesByIdsAsync(
                result.Items.Select(t => t.UserId),
                cancellationToken);

            var items = result.Items
                .Select(t => TopicMapper.ToResponse(t, authorNames))
                .ToList();

            return PagedResponse<TopicResponse>.Create(
                items,
                page,
                pageSize,
                result.TotalItems);
        }
    }
}