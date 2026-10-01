using CIS.Domain.Constants;
using CIS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Domain.Entities
{
    public class Topic
    {
        public long Id { get; private set; }

        public string Title { get; private set; } = string.Empty;

        public string Description { get; private set; } = string.Empty;

        public TopicStatus Status { get; private set; }

        public string UserId { get; private set; } = string.Empty;

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        private readonly List<TopicTag> _topicTags = new();

        public IReadOnlyCollection<TopicTag> TopicTags => _topicTags.AsReadOnly();


        private Topic() { }

        public Topic(
            string title,
            string description,
            string userId,
            DateTime createdAt)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException(
                    "Topic title is required.",
                    nameof(title));

            if (title.Trim().Length > TopicRules.MaxTitleLength)
                throw new ArgumentException(
                    "Topic title exceeds the maximum length.",
                    nameof(title));

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException(
                    "Topic description is required.",
                    nameof(description));

            if (description.Trim().Length > TopicRules.MaxDescriptionLength)
                throw new ArgumentException(
                    "Topic description exceeds the maximum length.",
                    nameof(description));

            if (!Guid.TryParseExact(userId, "D", out var parsedUserId))
            {
                throw new ArgumentException(
                    "A valid user ID is required.",
                    nameof(userId));
            }

            if (createdAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException(
                    "Creation date must be in UTC.",
                    nameof(createdAt));

            Title = title.Trim();
            Description = description.Trim();
            UserId = userId;

            Status = TopicStatus.Active;

            CreatedAt = createdAt;
            UpdatedAt = createdAt;

            UserId = parsedUserId.ToString("D");
        }

        // Add this method inside the Topic class
        public void AddTag(Tag tag)
        {
            ArgumentNullException.ThrowIfNull(tag);

            // Prevent duplicate tags within the topic
            if (_topicTags.Any(tt => tt.Tag == tag || tt.Tag.NormalizedName == tag.NormalizedName))
            {
                throw new InvalidOperationException($"The tag '{tag.Name}' is already associated with this topic.");
            }

            // Enforce the maximum number of tags
            if (_topicTags.Count >= TagRules.MaxTagsPerTopic)
            {
                throw new InvalidOperationException($"A topic cannot have more than " + $"{TagRules.MaxTagsPerTopic} tags.");
            }

            // Create the relationship
            _topicTags.Add(new TopicTag(this, tag));
        }
    }

}
