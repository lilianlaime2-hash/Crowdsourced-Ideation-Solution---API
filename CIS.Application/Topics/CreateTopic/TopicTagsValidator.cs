using CIS.Domain.Constants;
using CIS.Domain.Policies;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Topics.CreateTopic
{
    public static class TopicTagsValidator
    {
        public static IReadOnlyList<string> Validate(
            IEnumerable<string>? tags)
        {
            if (tags is null)
            {
                return [];
            }

            var result = new List<string>();
            var normalizedNames = new HashSet<string>(
                StringComparer.Ordinal);

            foreach (var tag in tags)
            {
                var (name, normalizedName) =
                    TagNamePolicy.ValidateAndNormalize(tag);

                if (!normalizedNames.Add(normalizedName))
                {
                    continue;
                }

                result.Add(name);

                if (result.Count > TagRules.MaxTagsPerTopic)
                {
                    throw new ArgumentException(
                        $"A topic cannot have more than " +
                        $"{TagRules.MaxTagsPerTopic} tags.");
                }
            }

            return result;
        }
    }
}
