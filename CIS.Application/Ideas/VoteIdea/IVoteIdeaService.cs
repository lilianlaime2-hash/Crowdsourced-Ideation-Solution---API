namespace CIS.Application.Ideas.VoteIdea;

public interface IVoteIdeaService
{
    Task<VoteIdeaResponse?> ExecuteAsync(long topicId, long ideaId, CancellationToken cancellationToken = default);
}