using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.MongoDB.Documents;
using CIS.Domain.Entities;
using MongoDB.Driver;

namespace CIS.DataAccess.MongoDB.Repositories;

public sealed class MongoVoteRepository : IVoteRepository
{
    private readonly IMongoCollection<VoteDocument> _votes;
    private readonly MongoSequence _sequence;

    public MongoVoteRepository(IMongoDatabase database, MongoSequence sequence)
    {
        _votes = database.GetCollection<VoteDocument>(MongoCollections.Votes);
        _sequence = sequence;
    }

    public async Task<bool> ExistsAsync(long ideaId, string userId, CancellationToken cancellationToken = default)
    {
        return await _votes
            .Find(v => v.IdeaId == ideaId && v.UserId == userId)
            .AnyAsync(cancellationToken);
    }

    public async Task<Vote> AddAsync(Vote vote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vote);

        var id = await _sequence.NextAsync(MongoCollections.Votes, cancellationToken);
        Rehydrator.Set(vote, nameof(Vote.Id), id);

        await _votes.InsertOneAsync(VoteDocument.From(vote), cancellationToken: cancellationToken);
        return vote;
    }

    public async Task<Vote?> GetAsync(long ideaId, string userId, CancellationToken cancellationToken = default)
    {
        var doc = await _votes
            .Find(v => v.IdeaId == ideaId && v.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : Rehydrator.ToVote(doc);
    }

    public async Task DeleteAsync(Vote vote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vote);
        await _votes.DeleteOneAsync(v => v.Id == vote.Id, cancellationToken);
    }
}