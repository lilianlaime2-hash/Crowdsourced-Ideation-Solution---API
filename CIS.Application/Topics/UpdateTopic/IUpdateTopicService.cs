namespace CIS.Application.Topics.UpdateTopic
{
    public interface IUpdateTopicService
    {
        Task<TopicResponse?> ExecuteAsync(
            long id,
            UpdateTopicRequest request,
            CancellationToken cancellationToken = default);
    }
}