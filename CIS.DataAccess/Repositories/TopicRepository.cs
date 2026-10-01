using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;

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
    }
}
