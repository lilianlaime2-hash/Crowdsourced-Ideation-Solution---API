using CIS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Abstractions.Persistence
{
    public interface ITopicRepository
    {
        Task<Topic> AddAsync(
            Topic topic,
            CancellationToken cancellationToken = default);
        Task<Topic?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Topic>> GetAllAsync(
            CancellationToken cancellationToken = default);
    }
}
