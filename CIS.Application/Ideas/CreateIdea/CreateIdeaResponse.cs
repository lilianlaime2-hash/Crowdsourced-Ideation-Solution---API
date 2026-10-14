using CIS.Domain.Enums;
using System.Text.Json.Serialization;

namespace CIS.Application.Ideas.CreateIdea
{
    public sealed class CreateIdeaResponse
    {
        public long Id { get; init; }

        public long TopicId { get; init; }

        public string AuthorName { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public IdeaStatus Status { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }
    }
}
