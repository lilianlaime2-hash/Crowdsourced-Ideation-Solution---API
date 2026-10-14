using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Application.Topics.BrowseTopics;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using CIS.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace CIS.DataAccess.Repositories
{
    public sealed class TopicRepository : ITopicRepository
    {
        private readonly CisDbContext _context;

        public TopicRepository(CisDbContext context)
        {
            _context = context;
        }

        public async Task<Topic> AddAsync(
            Topic topic,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(topic);

            await _context.Topics.AddAsync(topic, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return topic;
        }
        public async Task<Topic?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Topics
                .Include(t => t.TopicTags)
                .ThenInclude(tt => tt.Tag)
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Topic>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Topics
                .AsNoTracking()
                .Where(t => t.Status != TopicStatus.Deleted)
                .Include(t => t.TopicTags)
                .ThenInclude(tt => tt.Tag)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        public async Task UpdateAsync(
            Topic topic,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(topic);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PageResult<Topic>> GetPagedAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            PaginationRules.Validate(page, pageSize);

            var query = _context.Topics
                .AsNoTracking()
                .Where(t => t.Status != TopicStatus.Deleted);

            var totalItems = await query.LongCountAsync(cancellationToken);

            var skip = PaginationRules.CalculateSkip(page, pageSize);

            var items = await query
                .Include(t => t.TopicTags)
                .ThenInclude(tt => tt.Tag)
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PageResult<Topic>(items, totalItems);
        }
        public async Task<PageResult<Topic>> BrowseAsync(
            TopicBrowseOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            PaginationRules.Validate(options.Page, options.PageSize);

            IQueryable<Topic> query = _context.Topics
                .AsNoTracking()
                .Where(t => t.Status != TopicStatus.Deleted);

            // Filter by status
            if (options.Status.HasValue)
            {
                query = query.Where(t =>
                    t.Status == options.Status.Value);
            }

            // Filter by user
            if (!string.IsNullOrWhiteSpace(options.UserId))
            {
                var userId = options.UserId.Trim();

                query = query.Where(t =>
                    t.UserId == userId);
            }

            // Filter by tag
            if (!string.IsNullOrWhiteSpace(options.Tag))
            {
                var normalizedTag =
                    TagNamePolicy.ValidateAndNormalize(options.Tag)
                        .NormalizedName;

                query = query.Where(t =>
                    t.TopicTags.Any(tt =>
                        tt.Tag.NormalizedName == normalizedTag));
            }

            // Search by title or description
            if (!string.IsNullOrWhiteSpace(options.Search))
            {
                var search = options.Search.Trim();

                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    t.Description.Contains(search));
            }

            // Count filtered records
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
                .Include(t => t.TopicTags)
                .ThenInclude(tt => tt.Tag)
                .Skip(skip)
                .Take(options.PageSize)
                .ToListAsync(cancellationToken);

            return new PageResult<Topic>(items, totalItems);
        }
        private static IOrderedQueryable<Topic> ApplySorting(
            IQueryable<Topic> query,
            TopicSortField sortBy,
            SortDirection direction)
        {
            bool descending = direction == SortDirection.Desc;

            return sortBy switch
            {
                TopicSortField.Title => descending
                    ? query.OrderByDescending(t => t.Title)
                           .ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.Title)
                           .ThenBy(t => t.Id),

                TopicSortField.UpdatedAt => descending
                    ? query.OrderByDescending(t => t.UpdatedAt)
                           .ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.UpdatedAt)
                           .ThenBy(t => t.Id),

                TopicSortField.Status => descending
                    ? query.OrderByDescending(t => t.Status)
                           .ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.Status)
                           .ThenBy(t => t.Id),

                _ => descending
                    ? query.OrderByDescending(t => t.CreatedAt)
                           .ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.CreatedAt)
                           .ThenBy(t => t.Id)
            };
        }
    }
}
