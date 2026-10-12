namespace CIS.Application.Abstractions.Persistence;

public sealed record MostVotedIdeaData(
    long IdeaId,
    long TopicId,
    string UserId,
    string Title,
    string Description,
    int VoteCount);
public sealed record MostActiveTopicData(
    long TopicId,
    string UserId,
    string Title,
    int IdeaCount,
    int VoteCount);

public sealed record StatisticsPage<T>(
    IReadOnlyList<T> Items,
    long TotalItems);

public interface IStatisticsRepository
{
    Task<StatisticsPage<MostVotedIdeaData>> GetMostVotedIdeasAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);
    Task<StatisticsPage<MostActiveTopicData>> GetMostActiveTopicsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}