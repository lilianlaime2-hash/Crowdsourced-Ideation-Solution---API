using CIS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Abstractions.Persistence
{
    public interface IIdeaRepository
    {
        Task<Idea> AddAsync(Idea idea, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Idea>> GetByTopicIdAsync(
            long topicId,
            CancellationToken cancellationToken = default);
    }
}
