using CIS.Application.Abstractions.Persistence;

namespace CIS.Application.Topics.GetTopic
{
    public sealed class GetTopicService : IGetTopicService
    {
        private readonly ITopicRepository _topicRepository;
        public GetTopicService(ITopicRepository topicRepository)
        {
            _topicRepository = topicRepository;
        }
        public async Task<TopicResponse?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var topic = await _topicRepository.GetByIdAsync(id, cancellationToken);

            return topic is null ? null : TopicMapper.ToResponse(topic);
        }
        public async Task<IReadOnlyList<TopicResponse>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var topics = await _topicRepository.GetAllAsync(cancellationToken);
            return topics.Select(TopicMapper.ToResponse).ToList();
        }
    }
}