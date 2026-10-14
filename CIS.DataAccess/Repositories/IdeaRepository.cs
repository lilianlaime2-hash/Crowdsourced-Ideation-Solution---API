using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.DataAccess.Repositories
{
    public sealed class IdeaRepository : IIdeaRepository
    {
        private readonly CisDbContext _context;

        public IdeaRepository(CisDbContext context)
        {
            _context = context;
        }

        public async Task<Idea> AddAsync(Idea idea, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(idea);

            await _context.Ideas.AddAsync(idea, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return idea;
        }
        public async Task<IReadOnlyList<Idea>> GetByTopicIdAsync(
            long topicId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Ideas
                .AsNoTracking()
                .Where(i => i.TopicId == topicId)
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .ToListAsync(cancellationToken);
        }
        public async Task<Idea?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Ideas
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        }

        public async Task UpdateAsync(
            Idea idea,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(idea);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PageResult<Idea>> GetPagedByTopicIdAsync(
            long topicId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            PaginationRules.Validate(page, pageSize);

            var query = _context.Ideas
                .AsNoTracking()
                .Where(i => i.TopicId == topicId && i.Status != IdeaStatus.Deleted);

            var totalItems = await query.LongCountAsync(cancellationToken);

            var skip = PaginationRules.CalculateSkip(page, pageSize);

            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PageResult<Idea>(items, totalItems);
        }
    }
}
