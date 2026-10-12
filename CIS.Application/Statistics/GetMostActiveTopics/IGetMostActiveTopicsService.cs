namespace CIS.Application.Statistics.GetMostActiveTopics;

public interface IGetMostActiveTopicsService
{
    Task<PagedResponse<MostActiveTopicResponse>> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}