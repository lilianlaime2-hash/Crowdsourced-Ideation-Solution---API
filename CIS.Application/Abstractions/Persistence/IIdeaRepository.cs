using CIS.Application.Common.Pagination;
using CIS.Application.Ideas.BrowseIdeas;
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
        Task<Idea?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
        Task UpdateAsync(Idea idea, CancellationToken cancellationToken = default);
        Task<PageResult<Idea>> GetPagedByTopicIdAsync(
            long topicId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<PageResult<Idea>> BrowseAsync(
            long topicId,
            IdeaBrowseOptions options,
            CancellationToken cancellationToken = default);
    }

}
