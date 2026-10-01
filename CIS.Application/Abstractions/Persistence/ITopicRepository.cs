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
    }
}
