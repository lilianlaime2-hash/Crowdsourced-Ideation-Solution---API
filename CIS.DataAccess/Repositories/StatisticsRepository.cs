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
    public async Task<StatisticsPage<MostActiveTopicData>> GetMostActiveTopicsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var activeTopics = _context.Topics
            .AsNoTracking()
            .Where(t => t.Status != TopicStatus.Deleted);

        var totalItems = await activeTopics.LongCountAsync(cancellationToken);

        var rows = await activeTopics
            .Select(t => new
            {
                t.Id,
                t.UserId,
                t.Title,
                t.CreatedAt,
                IdeaCount = _context.Ideas.Count(i =>
                    i.TopicId == t.Id && i.Status != IdeaStatus.Deleted),
                VoteCount = _context.Votes.Count(v =>
                    _context.Ideas.Any(i =>
                        i.Id == v.IdeaId &&
                        i.TopicId == t.Id &&
                        i.Status != IdeaStatus.Deleted))
            })
            .OrderByDescending(x => x.IdeaCount + x.VoteCount)
            .ThenByDescending(x => x.IdeaCount)
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new MostActiveTopicData(
                x.Id,
                x.UserId,
                x.Title,
                x.IdeaCount,
                x.VoteCount))
            .ToList();

        return new StatisticsPage<MostActiveTopicData>(items, totalItems);
    }
}