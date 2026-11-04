using CIS.Domain.Entities;

namespace CIS.IntegrationTests.Infrastructure;

internal static class TestData
{
    public const string UserId = "11111111-1111-4111-8111-111111111111";
    public const string OtherUserId = "22222222-2222-4222-8222-222222222222";
    // BSON dates have millisecond precision; use exact round-trip values.
    public static readonly DateTime CreatedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public static Topic Topic(string title = "MongoDB integration", DateTime? createdAt = null) =>
        new(title, "A topic persisted in real MongoDB.", UserId, createdAt ?? CreatedAt);

    public static Idea Idea(long topicId, string title = "Integration idea", DateTime? createdAt = null) =>
        new(topicId, title, "An idea persisted in real MongoDB.", UserId, createdAt ?? CreatedAt);

    public static Vote Vote(long ideaId, string userId = UserId) => new(ideaId, userId, CreatedAt);
}
