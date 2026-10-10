using CIS.Application.Ideas.CreateIdea;
using CIS.Domain.Entities;

namespace CIS.Application.Ideas;

internal static class IdeaMapper
{
    public static CreateIdeaResponse ToResponse(
        Idea idea,
        IReadOnlyDictionary<string, string> authorNames)
    {
        return new CreateIdeaResponse
        {
            Id = idea.Id,
            TopicId = idea.TopicId,
            AuthorName = authorNames.TryGetValue(idea.UserId, out var authorName)
                ? authorName
                : "Unknown user",
            Title = idea.Title,
            Description = idea.Description,
            CreatedAt = idea.CreatedAt,
            UpdatedAt = idea.UpdatedAt
        };
    }
}