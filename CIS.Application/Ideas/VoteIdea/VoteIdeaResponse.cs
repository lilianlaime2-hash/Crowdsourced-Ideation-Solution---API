namespace CIS.Application.Ideas.VoteIdea;

public sealed class VoteIdeaResponse
{
    public long Id { get; init; }

    public long IdeaId { get; init; }

    public string UserId { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}