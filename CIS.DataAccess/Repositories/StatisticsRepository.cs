using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.Context;
using CIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CIS.DataAccess.Repositories;

public sealed class StatisticsRepository : IStatisticsRepository
{
    private readonly CisDbContext _context;

    public StatisticsRepository(CisDbContext context)
    {
        _context = context;
    }
    public async Task<StatisticsPage<MostVotedIdeaData>> GetMostVotedIdeasAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var activeIdeas = _context.Ideas
            .AsNoTracking()
            .Where(i => i.Status != IdeaStatus.Deleted)
            .Where(i => _context.Topics.Any(t =>
                t.Id == i.TopicId && t.Status != TopicStatus.Deleted));

        var totalItems = await activeIdeas.LongCountAsync(cancellationToken);
        
        var rows = await activeIdeas
            .Select(i => new
            {
                i.Id,
                i.TopicId,
                i.UserId,
                i.Title,
                i.Description,
                i.CreatedAt,
                VoteCount = _context.Votes.Count(v => v.IdeaId == i.Id)
            })
            .OrderByDescending(x => x.VoteCount)
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new MostVotedIdeaData(
                x.Id,
                x.TopicId,
                x.UserId,
                x.Title,
                x.Description,
                x.VoteCount))
            .ToList();

        return new StatisticsPage<MostVotedIdeaData>(items, totalItems);
    }
}