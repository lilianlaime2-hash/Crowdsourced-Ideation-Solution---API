using System.Text.RegularExpressions;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Application.Ideas.BrowseIdeas;
using CIS.DataAccess.MongoDB.Documents;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using SortDirection = CIS.Application.Common.Sorting.SortDirection;

namespace CIS.DataAccess.MongoDB.Repositories;

public sealed class MongoIdeaRepository : IIdeaRepository
{
    private readonly IMongoCollection<IdeaDocument> _ideas;
    private readonly MongoSequence _sequence;

    public MongoIdeaRepository(IMongoDatabase database, MongoSequence sequence)
    {
        _ideas = database.GetCollection<IdeaDocument>(MongoCollections.Ideas);
        _sequence = sequence;
    }

    public async Task<Idea> AddAsync(Idea idea, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idea);

        var id = await _sequence.NextAsync(MongoCollections.Ideas, cancellationToken);
        Rehydrator.Set(idea, nameof(Idea.Id), id);

        await _ideas.InsertOneAsync(IdeaDocument.From(idea), cancellationToken: cancellationToken);
        return idea;
    }

    public async Task<IReadOnlyList<Idea>> GetByTopicIdAsync(
        long topicId,
        CancellationToken cancellationToken = default)
    {
        var docs = await _ideas
            .Find(i => i.TopicId == topicId)
            .SortByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

        return docs.Select(Rehydrator.ToIdea).ToList();
    }

    public async Task<Idea?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var doc = await _ideas.Find(i => i.Id == id).FirstOrDefaultAsync(cancellationToken);
        return doc is null ? null : Rehydrator.ToIdea(doc);
    }

    public async Task UpdateAsync(Idea idea, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idea);

        await _ideas.ReplaceOneAsync(
            i => i.Id == idea.Id,
            IdeaDocument.From(idea),
            cancellationToken: cancellationToken);
    }

    public async Task<PageResult<Idea>> GetPagedByTopicIdAsync(
        long topicId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var fb = Builders<IdeaDocument>.Filter;
        var filter = fb.Eq(i => i.TopicId, topicId) & fb.Ne(i => i.Status, IdeaStatus.Deleted);

        var total = await _ideas.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var docs = await _ideas
            .Find(filter)
            .SortByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Skip(PaginationRules.CalculateSkip(page, pageSize))
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Idea>(docs.Select(Rehydrator.ToIdea).ToList(), total);
    }

    public async Task<PageResult<Idea>> BrowseAsync(
        long topicId,
        IdeaBrowseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        PaginationRules.Validate(options.Page, options.PageSize);

        var fb = Builders<IdeaDocument>.Filter;
        var filter = fb.Eq(i => i.TopicId, topicId) & fb.Ne(i => i.Status, IdeaStatus.Deleted);

        if (options.Status.HasValue)
        {
            filter &= fb.Eq(i => i.Status, options.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(options.UserId))
        {
            filter &= fb.Eq(i => i.UserId, options.UserId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var regex = new BsonRegularExpression(Regex.Escape(options.Search.Trim()), "i");
            filter &= fb.Or(
                fb.Regex(i => i.Title, regex),
                fb.Regex(i => i.Description, regex));
        }

        var total = await _ideas.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var docs = await _ideas
            .Find(filter)
            .Sort(BuildSort(options.SortBy, options.SortDirection))
            .Skip(PaginationRules.CalculateSkip(options.Page, options.PageSize))
            .Limit(options.PageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Idea>(docs.Select(Rehydrator.ToIdea).ToList(), total);
    }

    private static SortDefinition<IdeaDocument> BuildSort(IdeaSortField sortBy, SortDirection direction)
    {
        var sb = Builders<IdeaDocument>.Sort;
        var desc = direction == SortDirection.Desc;

        var primary = sortBy switch
        {
            IdeaSortField.UpdatedAt => desc ? sb.Descending(i => i.UpdatedAt) : sb.Ascending(i => i.UpdatedAt),
            IdeaSortField.Title => desc ? sb.Descending(i => i.Title) : sb.Ascending(i => i.Title),
            _ => desc ? sb.Descending(i => i.CreatedAt) : sb.Ascending(i => i.CreatedAt)
        };

        var tieBreaker = desc ? sb.Descending(i => i.Id) : sb.Ascending(i => i.Id);
        return sb.Combine(primary, tieBreaker);
    }
}