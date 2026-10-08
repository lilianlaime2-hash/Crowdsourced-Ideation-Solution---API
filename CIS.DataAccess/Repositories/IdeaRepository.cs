using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;
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
    }
}
