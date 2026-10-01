using CIS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Topics.CreateTopic
{
    public sealed class CreateTopicResponse
    {
        public long Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public TopicStatus Status { get; init; }

        public string UserId { get; init; } = string.Empty;

        public List<string> Tags { get; init; } = [];

        public DateTime CreatedAt { get; init; }

        public DateTime UpdatedAt { get; init; }
    }
}
