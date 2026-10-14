using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Application.Ideas.BrowseIdeas;
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
        public async Task<PageResult<Idea>> BrowseAsync(
            long topicId,
            IdeaBrowseOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            PaginationRules.Validate(options.Page, options.PageSize);

            IQueryable<Idea> query = _context.Ideas
                .AsNoTracking()
                .Where(i => i.TopicId == topicId &&
                            i.Status != IdeaStatus.Deleted);

            // Filter by status
            if (options.Status.HasValue)
            {
                query = query.Where(i =>
                    i.Status == options.Status.Value);
            }

            // Filter by user
            if (!string.IsNullOrWhiteSpace(options.UserId))
            {
                var userId = options.UserId.Trim();

                query = query.Where(i =>
                    i.UserId == userId);
            }

            // Search by title or description
            if (!string.IsNullOrWhiteSpace(options.Search))
            {
                var search = options.Search.Trim();

                query = query.Where(i =>
                    i.Title.Contains(search) ||
                    i.Description.Contains(search));
            }

            var totalItems = await query.LongCountAsync(
                cancellationToken);

            var orderedQuery = ApplySorting(
                query,
                options.SortBy,
                options.SortDirection);

            // Apply pagination
            var skip = PaginationRules.CalculateSkip(
                options.Page,
                options.PageSize);

            var items = await orderedQuery
                .Skip(skip)
                .Take(options.PageSize)
                .ToListAsync(cancellationToken);

            return new PageResult<Idea>(items, totalItems);
        }
        private static IOrderedQueryable<Idea> ApplySorting(
            IQueryable<Idea> query,
            IdeaSortField sortBy,
            SortDirection direction)
        {
            bool descending = direction == SortDirection.Desc;

            return sortBy switch
            {
                IdeaSortField.UpdatedAt => descending
                    ? query.OrderByDescending(i => i.UpdatedAt)
                           .ThenByDescending(i => i.Id)
                    : query.OrderBy(i => i.UpdatedAt)
                           .ThenBy(i => i.Id),

                IdeaSortField.Title => descending
                    ? query.OrderByDescending(i => i.Title)
                           .ThenByDescending(i => i.Id)
                    : query.OrderBy(i => i.Title)
                           .ThenBy(i => i.Id),

                _ => descending
                    ? query.OrderByDescending(i => i.CreatedAt)
                           .ThenByDescending(i => i.Id)
                    : query.OrderBy(i => i.CreatedAt)
                           .ThenBy(i => i.Id)
            };
        }
    }
}
