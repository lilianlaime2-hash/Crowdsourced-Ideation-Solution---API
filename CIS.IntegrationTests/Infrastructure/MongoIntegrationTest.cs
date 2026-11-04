using CIS.DataAccess.MongoDB;
using CIS.DataAccess.MongoDB.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.IntegrationTests.Infrastructure;

public abstract class MongoIntegrationTest : IAsyncLifetime
{
    protected MongoIntegrationTest(MongoDbFixture fixture)
    {
        Database = fixture.Client.GetDatabase($"cis_integration_{Guid.NewGuid():N}");
        ConnectionString = new MongoUrlBuilder(fixture.ConnectionString)
        {
            DatabaseName = Database.DatabaseNamespace.DatabaseName,
            // Changing the database must not change where the container user authenticates.
            AuthenticationSource = fixture.Client.Settings.Credential.Source
        }.ToString();
        Sequence = new MongoSequence(Database);
        Topics = new MongoTopicRepository(Database, Sequence);
        Ideas = new MongoIdeaRepository(Database, Sequence);
        Votes = new MongoVoteRepository(Database, Sequence);
    }

    protected IMongoDatabase Database { get; }
    protected string ConnectionString { get; }
    protected MongoSequence Sequence { get; }
    protected MongoTopicRepository Topics { get; }
    protected MongoIdeaRepository Ideas { get; }
    protected MongoVoteRepository Votes { get; }
    protected IMongoCollection<BsonDocument> Documents(string collection) =>
        Database.GetCollection<BsonDocument>(collection);

    public async Task InitializeAsync()
    {
        // Same constraints as the post-migration script, in a fresh test database.
        await Documents("tags").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("normalizedName"),
            new CreateIndexOptions { Unique = true, Name = "ux_tags_normalizedName" }));
        await Documents("votes").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("ideaId").Ascending("userId"),
            new CreateIndexOptions { Unique = true, Name = "ux_votes_ideaId_userId" }));
    }

    public Task DisposeAsync() =>
        Database.Client.DropDatabaseAsync(Database.DatabaseNamespace.DatabaseName);
}
