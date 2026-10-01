using CIS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Abstractions.Persistence
{
    public interface ITagRepository
    {
        Task<Tag?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
    }
}
