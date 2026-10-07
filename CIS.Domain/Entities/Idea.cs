using CIS.Domain.Constants;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Domain.Entities
{
    public sealed class Idea
    {
        public long Id { get; private set; }

        public long TopicId { get; private set; }

        public string UserId { get; private set; } = string.Empty;

        public string Title { get; private set; } = string.Empty;

        public string Description { get; private set; } = string.Empty;

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        private Idea() { }

        public Idea(
            long topicId,
            string title,
            string description,
            string userId,
            DateTime createdAt)
        {
            if (topicId <= 0)
                throw new ArgumentException(
                    "A valid topic ID is required.",
                    nameof(topicId));

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException(
                    "Idea title is required.",
                    nameof(title));

            if (title.Trim().Length > IdeaRules.MaxTitleLength)
                throw new ArgumentException(
                    "Idea title exceeds the maximum length.",
                    nameof(title));

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException(
                    "Idea description is required.",
                    nameof(description));

            if (description.Trim().Length > IdeaRules.MaxDescriptionLength)
                throw new ArgumentException(
                    "Idea description exceeds the maximum length.",
                    nameof(description));

            if (!Guid.TryParseExact(userId, "D", out var parsedUserId))
                throw new ArgumentException(
                    "A valid user ID is required.",
                    nameof(userId));

            if (createdAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "Creation date must be in UTC.",
                    nameof(createdAt));

            TopicId = topicId;
            Title = title.Trim();
            Description = description.Trim();
            UserId = parsedUserId.ToString("D");
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }
    }
}
