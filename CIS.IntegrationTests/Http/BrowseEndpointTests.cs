using System.Net;
using CIS.IntegrationTests.Infrastructure;

namespace CIS.IntegrationTests.Http;

[Collection(MongoDbCollection.Name)]
public sealed class BrowseEndpointTests(MongoDbFixture fixture) : HttpIntegrationTest(fixture)
{
    public static TheoryData<string, int> ValidPages => Pages([1, 100, 50]);
    public static TheoryData<string, int> InvalidPageSizes => Pages([0, 101, -1]);

    private static TheoryData<string, int> Pages(int[] sizes)
    {
        var data = new TheoryData<string, int>();
        foreach (var route in new[] { "topics", "ideas", "most-voted", "most-active" })
            foreach (var size in sizes) data.Add(route, size);
        return data;
    }

    [Theory]
    [MemberData(nameof(ValidPages))]
    public async Task Browse_ValidPageSize_ReturnsPersistedRowsAndAccurateTotals(string endpoint, int pageSize)
    {
        // More than 100 rows exercise the minimum, midpoint and maximum page sizes.
        const int totalRows = 101;
        var topic = await SeedTopic("Main topic");
        var mainIdeas = new List<long>();
        for (var index = 0; index < totalRows; index++)
        {
            var idea = await SeedIdea(topic.Id, $"Main idea {index:D2}");
            mainIdeas.Add(idea.Id);
            await Votes.AddAsync(TestData.Vote(idea.Id));
            if (index > 0) await SeedTopic($"Other topic {index:D2}");
        }
        // An extra vote gives the first idea a unique rank.
        await Votes.AddAsync(TestData.Vote(mainIdeas[0], TestData.OtherUserId));
        using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.GetAsync($"{Route(endpoint, topic.Id)}?page=1&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(pageSize, body.GetProperty("pageSize").GetInt32());
        Assert.Equal((long)totalRows, body.GetProperty("totalItems").GetInt64());
        Assert.Equal((totalRows + pageSize - 1) / pageSize, body.GetProperty("totalPages").GetInt32());
        var rows = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(Math.Min(totalRows, pageSize), rows.Length);
        Assert.All(rows, row => Assert.Equal("Integration author", row.GetProperty("authorName").GetString()));
        foreach (var row in rows)
        {
            var idKey = endpoint is "topics" or "ideas" ? "id" : endpoint == "most-voted" ? "ideaId" : "topicId";
            var id = row.GetProperty(idKey).GetInt64();
            if (endpoint is "topics" or "most-active")
            {
                var stored = await Topics.GetByIdAsync(id);
                Assert.NotNull(stored);
                Assert.Equal(stored.Title, row.GetProperty("title").GetString());
            }
            else
            {
                var stored = await Ideas.GetByIdAsync(id);
                Assert.NotNull(stored);
                Assert.Equal(stored.Title, row.GetProperty("title").GetString());
                Assert.Equal(topic.Id, row.GetProperty("topicId").GetInt64());
            }
        }
        if (endpoint == "most-voted")
        {
            Assert.Equal(mainIdeas[0], rows[0].GetProperty("ideaId").GetInt64());
            Assert.Equal(2, rows[0].GetProperty("voteCount").GetInt32());
        }
        if (endpoint == "most-active")
        {
            Assert.Equal(topic.Id, rows[0].GetProperty("topicId").GetInt64());
            Assert.Equal(totalRows, rows[0].GetProperty("ideaCount").GetInt32());
            Assert.Equal(totalRows + 1, rows[0].GetProperty("voteCount").GetInt32());
            Assert.Equal(2 * totalRows + 1, rows[0].GetProperty("activityScore").GetInt32());
        }
        Assert.Equal((long)totalRows, await Count("topics"));
        Assert.Equal((long)totalRows, await Count("ideas"));
        Assert.Equal((long)totalRows + 1, await Count("votes"));
    }

    [Theory]
    [MemberData(nameof(InvalidPageSizes))]
    public async Task Browse_InvalidPageSize_Returns400(string endpoint, int pageSize)
    {
        var topic = await SeedTopic();
        using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.GetAsync($"{Route(endpoint, topic.Id)}?pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1L, await Count("topics"));
        Assert.Equal(topic.Title, (await Topics.GetByIdAsync(topic.Id))!.Title);
    }

    [Theory]
    [InlineData("topics")]
    [InlineData("ideas")]
    [InlineData("most-voted")]
    [InlineData("most-active")]
    public async Task Browse_PageZero_Returns400(string endpoint)
    {
        var topic = await SeedTopic();
        using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.GetAsync($"{Route(endpoint, topic.Id)}?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BrowseIdeas_MissingTopic_Returns404()
    {
        using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.GetAsync($"/api/topics/{long.MaxValue}/ideas");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string Route(string endpoint, long topicId) => endpoint switch
    {
        "topics" => "/api/topics",
        "ideas" => $"/api/topics/{topicId}/ideas",
        "most-voted" => "/api/statistics/ideas/most-voted",
        "most-active" => "/api/statistics/topics/most-active",
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
    };
}
