using System.Text.RegularExpressions;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Application.Topics.BrowseTopics;
using CIS.DataAccess.MongoDB.Documents;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using CIS.Domain.Policies;
using MongoDB.Bson;
using MongoDB.Driver;
using SortDirection = CIS.Application.Common.Sorting.SortDirection;
using Tag = CIS.Domain.Entities.Tag;
namespace CIS.DataAccess.MongoDB.Repositories;

public sealed class MongoTopicRepository : ITopicRepository
{
    private readonly IMongoCollection<TopicDocument> _topics;
    private readonly IMongoCollection<TagDocument> _tags;
    private readonly MongoSequence _sequence;

    public MongoTopicRepository(IMongoDatabase database, MongoSequence sequence)
    {
        _topics = database.GetCollection<TopicDocument>(MongoCollections.Topics);
        _tags = database.GetCollection<TagDocument>(MongoCollections.Tags);
        _sequence = sequence;
    }

    public async Task<Topic> AddAsync(Topic topic, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);

        // New tags (Id = 0) are saved first so the topic can reference their ids
        foreach (var topicTag in topic.TopicTags)
        {
            var tag = topicTag.Tag;

            if (tag.Id == 0)
            {
                var tagId = await _sequence.NextAsync(MongoCollections.Tags, cancellationToken);
                Rehydrator.Set(tag, nameof(Tag.Id), tagId);

                await _tags.InsertOneAsync(TagDocument.From(tag), cancellationToken: cancellationToken);
            }
        }

        var id = await _sequence.NextAsync(MongoCollections.Topics, cancellationToken);
        Rehydrator.Set(topic, nameof(Topic.Id), id);

        await _topics.InsertOneAsync(TopicDocument.From(topic), cancellationToken: cancellationToken);
        return topic;
    }

    public async Task<Topic?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var doc = await _topics.Find(t => t.Id == id).FirstOrDefaultAsync(cancellationToken);

        if (doc is null)
        {
            return null;
        }

        var topics = await ToTopicsAsync([doc], cancellationToken);
        return topics[0];
    }

    public async Task<IReadOnlyList<Topic>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var docs = await _topics
            .Find(t => t.Status != TopicStatus.Deleted)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return await ToTopicsAsync(docs, cancellationToken);
    }

    public async Task UpdateAsync(Topic topic, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);

        await _topics.ReplaceOneAsync(
            t => t.Id == topic.Id,
            TopicDocument.From(topic),
            cancellationToken: cancellationToken);
    }

    public async Task<PageResult<Topic>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PaginationRules.Validate(page, pageSize);

        var filter = Builders<TopicDocument>.Filter.Ne(t => t.Status, TopicStatus.Deleted);

        var total = await _topics.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var docs = await _topics
            .Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip(PaginationRules.CalculateSkip(page, pageSize))
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Topic>(await ToTopicsAsync(docs, cancellationToken), total);
    }

    public async Task<PageResult<Topic>> BrowseAsync(
        TopicBrowseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        PaginationRules.Validate(options.Page, options.PageSize);

        var fb = Builders<TopicDocument>.Filter;
        var filter = fb.Ne(t => t.Status, TopicStatus.Deleted);

        if (options.Status.HasValue)
        {
            filter &= fb.Eq(t => t.Status, options.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(options.UserId))
        {
            filter &= fb.Eq(t => t.UserId, options.UserId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(options.Tag))
        {
            var normalizedTag = TagNamePolicy.ValidateAndNormalize(options.Tag).NormalizedName;

            var tag = await _tags
                .Find(t => t.NormalizedName == normalizedTag)
                .FirstOrDefaultAsync(cancellationToken);

            if (tag is null)
            {
                return new PageResult<Topic>([], 0);
            }

            filter &= fb.AnyEq(t => t.TagIds, tag.Id);
        }

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var regex = new BsonRegularExpression(Regex.Escape(options.Search.Trim()), "i");
            filter &= fb.Or(
                fb.Regex(t => t.Title, regex),
                fb.Regex(t => t.Description, regex));
        }

        var total = await _topics.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var docs = await _topics
            .Find(filter)
            .Sort(BuildSort(options.SortBy, options.SortDirection))
            .Skip(PaginationRules.CalculateSkip(options.Page, options.PageSize))
            .Limit(options.PageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Topic>(await ToTopicsAsync(docs, cancellationToken), total);
    }

    // Loads the tags of all the topics in one query and rebuilds the entities
    private async Task<List<Topic>> ToTopicsAsync(
        List<TopicDocument> docs,
        CancellationToken cancellationToken)
    {
        var tagIds = docs.SelectMany(d => d.TagIds).Distinct().ToList();
        var tagDocs = tagIds.Count == 0
            ? []
            : await _tags
                .Find(Builders<TagDocument>.Filter.In(t => t.Id, tagIds))
                .ToListAsync(cancellationToken);

        var tags = tagDocs.ToDictionary(t => t.Id, Rehydrator.ToTag);

        return docs.Select(d => Rehydrator.ToTopic(d, tags)).ToList();
    }

    private static SortDefinition<TopicDocument> BuildSort(TopicSortField sortBy, SortDirection direction)
    {
        var sb = Builders<TopicDocument>.Sort;
        var desc = direction == SortDirection.Desc;

        var primary = sortBy switch
        {
            TopicSortField.Title => desc ? sb.Descending(t => t.Title) : sb.Ascending(t => t.Title),
            TopicSortField.UpdatedAt => desc ? sb.Descending(t => t.UpdatedAt) : sb.Ascending(t => t.UpdatedAt),
            TopicSortField.Status => desc ? sb.Descending(t => t.Status) : sb.Ascending(t => t.Status),
            _ => desc ? sb.Descending(t => t.CreatedAt) : sb.Ascending(t => t.CreatedAt)
        };

        var tieBreaker = desc ? sb.Descending(t => t.Id) : sb.Ascending(t => t.Id);
        return sb.Combine(primary, tieBreaker);
    }
}