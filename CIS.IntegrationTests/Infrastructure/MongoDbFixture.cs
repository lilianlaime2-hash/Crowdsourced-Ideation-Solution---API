using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace CIS.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class MongoDbCollection : ICollectionFixture<MongoDbFixture>
{
    public const string Name = "MongoDB integration";
}

public sealed class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0").Build();

    public MongoClient Client { get; private set; } = null!;
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Client = new MongoClient(ConnectionString);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
