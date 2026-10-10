using CIS.Domain.Enums;
namespace CIS.Application.Topics;

public sealed class TopicResponse
{
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TopicStatus Status { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}