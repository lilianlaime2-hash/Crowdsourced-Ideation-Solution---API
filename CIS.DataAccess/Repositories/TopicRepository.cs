using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;
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
                .Include(t => t.TopicTags)
                .ThenInclude(tt => tt.Tag)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
