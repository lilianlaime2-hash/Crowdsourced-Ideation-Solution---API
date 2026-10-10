using System;

namespace CIS.Domain.Entities;

public sealed class Vote
{
    public long Id { get; private set; }

    public long IdeaId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    private Vote() { }

    public Vote(long ideaId, string userId, DateTime createdAt)
    {
        if (ideaId <= 0)
        {
            throw new ArgumentException("A valid idea ID is required.", nameof(ideaId));
        }

        if (!Guid.TryParseExact(userId, "D", out var parsedUserId))
        {
            throw new ArgumentException("A valid user ID is required.", nameof(userId));
        }

        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Creation date must be in UTC.", nameof(createdAt));
        }

        IdeaId = ideaId;
        UserId = parsedUserId.ToString("D");
        CreatedAt = createdAt;
    }
}