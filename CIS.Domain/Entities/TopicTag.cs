using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Domain.Entities
{
    public class TopicTag
    {
        public long TopicId { get; private set; }

        public long TagId { get; private set; }

        public Topic Topic { get; private set; } = null!;

        public Tag Tag { get; private set; } = null!;

        // Required by Entity Framework Core
        private TopicTag() { }

        public TopicTag(Topic topic, Tag tag)
        {
            ArgumentNullException.ThrowIfNull(topic);
            ArgumentNullException.ThrowIfNull(tag);

            Topic = topic;
            Tag = tag;
        }
    }
}
