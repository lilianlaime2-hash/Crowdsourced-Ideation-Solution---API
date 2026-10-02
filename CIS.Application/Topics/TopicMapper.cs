using CIS.Domain.Entities;

namespace CIS.Application.Topics
{
    internal static class TopicMapper
    {
        public static TopicResponse ToResponse(Topic topic)
        {
            return new TopicResponse
            {
                Id = topic.Id,
                Title = topic.Title,
                Description = topic.Description,
                Status = topic.Status,
                UserId = topic.UserId,
                Tags = topic.TopicTags.Select(tt => tt.Tag.Name).ToList(),
                CreatedAt = topic.CreatedAt,
                UpdatedAt = topic.UpdatedAt
            };
        }
    }
}