using CIS.Domain.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CIS.Application.Topics.CreateTopic
{
    public sealed class CreateTopicRequest
    {
        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(TopicRules.MaxTitleLength)]
        public string Title { get; init; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [MaxLength(TopicRules.MaxDescriptionLength)]
        public string Description { get; init; } = string.Empty;

        public List<string> Tags { get; init; } = [];
    }
}
