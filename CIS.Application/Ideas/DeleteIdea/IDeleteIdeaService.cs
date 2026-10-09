namespace CIS.Application.Ideas.DeleteIdea;

public interface IDeleteIdeaService
{
    Task<bool> ExecuteAsync(long topicId, long id, CancellationToken cancellationToken = default);
}