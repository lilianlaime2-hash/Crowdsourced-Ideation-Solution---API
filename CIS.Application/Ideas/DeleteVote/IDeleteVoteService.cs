namespace CIS.Application.Ideas.DeleteVote;

public interface IDeleteVoteService
{
    Task<bool> ExecuteAsync(long topicId, long ideaId, CancellationToken cancellationToken = default);
}