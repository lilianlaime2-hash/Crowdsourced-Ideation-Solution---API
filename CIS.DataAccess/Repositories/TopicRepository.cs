using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
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
    }
}
