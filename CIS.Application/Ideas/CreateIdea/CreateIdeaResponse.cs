using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Ideas.CreateIdea
{
    public sealed class CreateIdeaResponse
    {
        public long Id { get; init; }

        public long TopicId { get; init; }

        public string AuthorName { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }
    }
}
