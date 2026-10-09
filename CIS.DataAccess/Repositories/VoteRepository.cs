using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIS.DataAccess.Repositories;

public sealed class VoteRepository : IVoteRepository
{
    private readonly CisDbContext _context;

    public VoteRepository(CisDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(long ideaId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Votes.AnyAsync(v => v.IdeaId == ideaId && v.UserId == userId, cancellationToken);
    }

    public async Task<Vote> AddAsync(Vote vote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vote);

        await _context.Votes.AddAsync(vote, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return vote;
    }
}