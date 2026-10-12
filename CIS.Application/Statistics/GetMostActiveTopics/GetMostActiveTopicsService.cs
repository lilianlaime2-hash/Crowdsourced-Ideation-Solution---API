using CIS.Application.Abstractions.Persistence;

namespace CIS.Application.Statistics.GetMostActiveTopics;

public sealed class GetMostActiveTopicsService : IGetMostActiveTopicsService
{
    private readonly IStatisticsRepository _statisticsRepository;
    private readonly IUsersApiClient _usersApiClient;

    public GetMostActiveTopicsService(
        IStatisticsRepository statisticsRepository,
        IUsersApiClient usersApiClient)
    {
        _statisticsRepository = statisticsRepository;
        _usersApiClient = usersApiClient;
    }

    public async Task<PagedResponse<MostActiveTopicResponse>> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var skip = (page - 1) * pageSize;

        var result = await _statisticsRepository.GetMostActiveTopicsAsync(
            skip,
            pageSize,
            cancellationToken);

        if (result.Items.Count == 0)
        {
            return PagedResponse<MostActiveTopicResponse>.Create(
                [],
                page,
                pageSize,
                result.TotalItems);
        }

        var authorNames = await _usersApiClient.GetNamesByIdsAsync(
            result.Items.Select(t => t.UserId),
            cancellationToken);

        var items = result.Items
            .Select(t => new MostActiveTopicResponse
            {
                TopicId = t.TopicId,
                Title = t.Title,
                AuthorName = authorNames.TryGetValue(t.UserId, out var authorName)
                    ? authorName
                    : "Unknown user",
                IdeaCount = t.IdeaCount,
                VoteCount = t.VoteCount,
                ActivityScore = t.IdeaCount + t.VoteCount
            })
            .ToList();

        return PagedResponse<MostActiveTopicResponse>.Create(
            items,
            page,
            pageSize,
            result.TotalItems);
    }
}