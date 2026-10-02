namespace CIS.Application.Topics.GetTopic
{
    public interface IGetTopicService
    {
        Task<TopicResponse?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TopicResponse>> GetAllAsync(
            CancellationToken cancellationToken = default);
    }
}