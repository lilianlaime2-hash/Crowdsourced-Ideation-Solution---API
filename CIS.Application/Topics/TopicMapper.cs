using CIS.Domain.Entities;

namespace CIS.Application.Topics
{
    internal static class TopicMapper
    {
        public static TopicResponse ToResponse(Topic topic, IReadOnlyDictionary<string, string> authorNames)
        {
            return new TopicResponse
            {
                Id = topic.Id,
                Title = topic.Title,
                Description = topic.Description,
                Status = topic.Status,
                AuthorName = authorNames.TryGetValue(topic.UserId, out var authorName) ? authorName : "Unknown user",
                Tags = topic.TopicTags.Select(tt => tt.Tag.Name).ToList(),
                CreatedAt = topic.CreatedAt,
                UpdatedAt = topic.UpdatedAt
            };
        }
    }
}