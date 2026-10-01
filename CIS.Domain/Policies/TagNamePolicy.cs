using CIS.Domain.Constants;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CIS.Domain.Policies
{
    public static class TagNamePolicy
    {
        public static (string Name, string NormalizedName)
            ValidateAndNormalize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Tag name is required.");
            }

            // Normalize Unicode and remove extra spaces
            var cleanName = Regex.Replace(
                name.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ");

            // Validate length
            if (cleanName.Length < TagRules.MinNameLength ||
                cleanName.Length > TagRules.MaxNameLength)
            {
                throw new ArgumentException(
                    $"Tag name must contain between " +
                    $"{TagRules.MinNameLength} and " +
                    $"{TagRules.MaxNameLength} characters.");
            }

            // Allow letters, numbers, spaces and common
            // programming-related symbols
            if (!Regex.IsMatch(cleanName, @"^[\p{L}\p{M}\p{N} #+.\-]+$"))
            {
                throw new ArgumentException("Tag name contains invalid characters.");
            }

            // Require at least one letter
            if (!Regex.IsMatch(cleanName, @"[\p{L}]"))
            {
                throw new ArgumentException("Tag name must contain at least one letter.");
            }

            var normalizedName = cleanName.ToUpperInvariant();

            return (cleanName, normalizedName);
        }
    }
}
