namespace CIS.Application.Statistics.GetMostVotedIdeas;

public interface IGetMostVotedIdeasService
{
    Task<PagedResponse<MostVotedIdeaResponse>> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}