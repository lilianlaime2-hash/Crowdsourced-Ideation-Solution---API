using CIS.Domain.Policies;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Domain.Entities
{
    public class Tag
    {
        public long Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string NormalizedName { get; private set; } = string.Empty;

        private Tag() { }

        public Tag(string name)
        {
            var (cleanName, normalizedName) =
                TagNamePolicy.ValidateAndNormalize(name);

            Name = cleanName;
            NormalizedName = normalizedName;
        }
    }
}
