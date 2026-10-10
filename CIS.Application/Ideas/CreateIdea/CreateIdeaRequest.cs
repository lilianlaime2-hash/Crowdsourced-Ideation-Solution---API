using CIS.Domain.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CIS.Application.Ideas.CreateIdea
{
    public sealed class CreateIdeaRequest
    {
        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(IdeaRules.MaxTitleLength)]
        public string Title { get; init; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [MaxLength(IdeaRules.MaxDescriptionLength)]
        public string Description { get; init; } = string.Empty;
    }
}
