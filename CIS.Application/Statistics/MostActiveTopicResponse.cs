namespace CIS.Application.Statistics;

public sealed class MostActiveTopicResponse
{
    public long TopicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public int IdeaCount { get; init; }
    public int VoteCount { get; init; }
    public int ActivityScore { get; init; }
}