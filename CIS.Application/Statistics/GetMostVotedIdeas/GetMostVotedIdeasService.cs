using CIS.Application.Abstractions.Persistence;

namespace CIS.Application.Statistics.GetMostVotedIdeas;

public sealed class GetMostVotedIdeasService : IGetMostVotedIdeasService
{
    private readonly IStatisticsRepository _statisticsRepository;
    private readonly IUsersApiClient _usersApiClient;

    public GetMostVotedIdeasService(
        IStatisticsRepository statisticsRepository,
        IUsersApiClient usersApiClient)
    {
        _statisticsRepository = statisticsRepository;
        _usersApiClient = usersApiClient;
    }

    public async Task<PagedResponse<MostVotedIdeaResponse>> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var skip = (page - 1) * pageSize;

        var result = await _statisticsRepository.GetMostVotedIdeasAsync(
            skip,
            pageSize,
            cancellationToken);

        if (result.Items.Count == 0)
        {
            return PagedResponse<MostVotedIdeaResponse>.Create(
                [],
                page,
                pageSize,
                result.TotalItems);
        }

        var authorNames = await _usersApiClient.GetNamesByIdsAsync(
            result.Items.Select(i => i.UserId),
            cancellationToken);

        var items = result.Items
            .Select(i => new MostVotedIdeaResponse
            {
                IdeaId = i.IdeaId,
                TopicId = i.TopicId,
                Title = i.Title,
                Description = i.Description,
                AuthorName = authorNames.TryGetValue(i.UserId, out var authorName)
                    ? authorName
                    : "Unknown user",
                VoteCount = i.VoteCount
            })
            .ToList();

        return PagedResponse<MostVotedIdeaResponse>.Create(
            items,
            page,
            pageSize,
            result.TotalItems);
    }
}