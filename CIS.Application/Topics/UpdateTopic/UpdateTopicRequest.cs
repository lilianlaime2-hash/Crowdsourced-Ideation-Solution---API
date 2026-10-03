using CIS.Domain.Constants;
using System.ComponentModel.DataAnnotations;

namespace CIS.Application.Topics.UpdateTopic
{
    public sealed class UpdateTopicRequest
    {
        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(TopicRules.MaxTitleLength)]
        public string Title { get; init; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [MaxLength(TopicRules.MaxDescriptionLength)]
        public string Description { get; init; } = string.Empty;
    }
}