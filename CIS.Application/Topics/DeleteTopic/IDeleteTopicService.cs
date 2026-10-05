namespace CIS.Application.Topics.DeleteTopic;

public interface IDeleteTopicService
{
    Task<bool> ExecuteAsync(long id, CancellationToken cancellationToken = default);
}