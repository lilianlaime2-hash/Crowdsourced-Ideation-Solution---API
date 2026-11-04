using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.MongoDB.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.DataAccess.MongoDB.Repositories;

public sealed class MongoStatisticsRepository : IStatisticsRepository
{
    private readonly IMongoCollection<IdeaDocument> _ideas;
    private readonly IMongoCollection<TopicDocument> _topics;

    public MongoStatisticsRepository(IMongoDatabase database)
    {
        _ideas = database.GetCollection<IdeaDocument>(MongoCollections.Ideas);
        _topics = database.GetCollection<TopicDocument>(MongoCollections.Topics);
    }

    public async Task<StatisticsPage<MostVotedIdeaData>> GetMostVotedIdeasAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var stages = new[]
        {
            BsonDocument.Parse("""{ "$match": { "status": { "$ne": "Deleted" } } }"""),
            BsonDocument.Parse("""{ "$lookup": { "from": "topics", "localField": "topicId", "foreignField": "_id", "as": "topic" } }"""),
            BsonDocument.Parse("""{ "$match": { "topic": { "$elemMatch": { "status": { "$ne": "Deleted" } } } } }"""),
            BsonDocument.Parse("""{ "$lookup": { "from": "votes", "localField": "_id", "foreignField": "ideaId", "as": "votes" } }"""),
            BsonDocument.Parse("""{ "$addFields": { "voteCount": { "$size": "$votes" } } }"""),
            BsonDocument.Parse("""{ "$sort": { "voteCount": -1, "createdAt": -1, "_id": 1 } }"""),
            BsonDocument.Parse($$"""
                { "$facet": {
                    "items": [
                        { "$skip": {{skip}} },
                        { "$limit": {{take}} },
                        { "$project": { "topicId": 1, "userId": 1, "title": 1, "description": 1, "voteCount": 1 } }
                    ],
                    "total": [ { "$count": "n" } ]
                } }
                """)
        };

        var pipeline = PipelineDefinition<IdeaDocument, BsonDocument>.Create(stages);
        var result = await _ideas.Aggregate(pipeline).FirstOrDefaultAsync(cancellationToken);

        var items = new List<MostVotedIdeaData>();
        long total = 0;

        if (result is not null)
        {
            foreach (var value in result["items"].AsBsonArray)
            {
                var d = value.AsBsonDocument;
                items.Add(new MostVotedIdeaData(
                    d["_id"].ToInt64(),
                    d["topicId"].ToInt64(),
                    d["userId"].AsString,
                    d["title"].AsString,
                    d["description"].AsString,
                    d["voteCount"].ToInt32()));
            }

            var totalArray = result["total"].AsBsonArray;
            if (totalArray.Count > 0)
            {
                total = totalArray[0]["n"].ToInt64();
            }
        }

        return new StatisticsPage<MostVotedIdeaData>(items, total);
    }

    public async Task<StatisticsPage<MostActiveTopicData>> GetMostActiveTopicsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var stages = new[]
        {
            BsonDocument.Parse("""{ "$match": { "status": { "$ne": "Deleted" } } }"""),
            BsonDocument.Parse("""
                { "$lookup": {
                    "from": "ideas",
                    "let": { "tid": "$_id" },
                    "pipeline": [
                        { "$match": { "$expr": { "$and": [
                            { "$eq": [ "$topicId", "$$tid" ] },
                            { "$ne": [ "$status", "Deleted" ] }
                        ] } } },
                        { "$lookup": { "from": "votes", "localField": "_id", "foreignField": "ideaId", "as": "v" } },
                        { "$project": { "voteCount": { "$size": "$v" } } }
                    ],
                    "as": "ideas"
                } }
                """),
            BsonDocument.Parse("""{ "$addFields": { "ideaCount": { "$size": "$ideas" }, "voteCount": { "$sum": "$ideas.voteCount" } } }"""),
            BsonDocument.Parse("""{ "$addFields": { "activity": { "$add": [ "$ideaCount", "$voteCount" ] } } }"""),
            BsonDocument.Parse("""{ "$sort": { "activity": -1, "ideaCount": -1, "createdAt": -1, "_id": 1 } }"""),
            BsonDocument.Parse($$"""
                { "$facet": {
                    "items": [
                        { "$skip": {{skip}} },
                        { "$limit": {{take}} },
                        { "$project": { "userId": 1, "title": 1, "ideaCount": 1, "voteCount": 1 } }
                    ],
                    "total": [ { "$count": "n" } ]
                } }
                """)
        };

        var pipeline = PipelineDefinition<TopicDocument, BsonDocument>.Create(stages);
        var result = await _topics.Aggregate(pipeline).FirstOrDefaultAsync(cancellationToken);

        var items = new List<MostActiveTopicData>();
        long total = 0;

        if (result is not null)
        {
            foreach (var value in result["items"].AsBsonArray)
            {
                var d = value.AsBsonDocument;
                items.Add(new MostActiveTopicData(
                    d["_id"].ToInt64(),
                    d["userId"].AsString,
                    d["title"].AsString,
                    d["ideaCount"].ToInt32(),
                    d["voteCount"].ToInt32()));
            }

            var totalArray = result["total"].AsBsonArray;
            if (totalArray.Count > 0)
            {
                total = totalArray[0]["n"].ToInt64();
            }
        }

        return new StatisticsPage<MostActiveTopicData>(items, total);
    }
}
