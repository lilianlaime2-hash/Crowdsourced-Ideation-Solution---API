namespace CIS.Application.Statistics;

public sealed class MostVotedIdeaResponse
{
    public long IdeaId { get; init; }

    public long TopicId { get; init; }

    public string Title { get; init; } = string.Empty;
    
    public string Description { get; init; } = string.Empty;

    public string AuthorName { get; init; } = string.Empty;
    
    public int VoteCount { get; init; }
}