using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CIS.Application.Common.Pagination;
using CIS.Application.Topics;
using CIS.Application.Topics.CreateTopic;
using CIS.Domain.Enums;
using CIS.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.IntegrationTests;

[Collection(MongoDbCollection.Name)]
public sealed class CisApiHttpIntegrationTests(MongoDbFixture fixture) : MongoIntegrationTest(fixture)
{
    [Fact]
    public async Task ProtectedEndpoints_MissingOrInvalidJwt_ReturnUnauthorized()
    {
        using var factory = new CisApiFactory(ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        string?[] tokens =
        [
            null,
            "not-a-jwt",
            CreateToken(signingKey: "different-integration-signing-key-of-at-least-32-bytes"),
            CreateToken(expires: DateTime.UtcNow.AddMinutes(-5)),
            CreateToken(subject: "not-a-uuid"),
            CreateToken(algorithm: SecurityAlgorithms.HmacSha384)
        ];
        string[] routes =
        [
            "/api/topics", "/api/topics/1", "/api/topics/1/ideas",
            "/api/statistics/ideas/most-voted", "/api/statistics/topics/most-active"
        ];

        foreach (var token in tokens)
        {
            foreach (var route in routes)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, route);
                if (token is not null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            using var create = new HttpRequestMessage(HttpMethod.Post, "/api/topics")
            {
                Content = JsonContent.Create(new CreateTopicRequest { Title = "Rejected", Description = "Must not persist." })
            };
            if (token is not null)
                create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var createResponse = await client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
        }
        Assert.Equal(0L, await Documents("topics").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task Topics_ValidJwtAnd50ConcurrentCreates_PersistsUniqueResourcesAndAllowsReading()
    {
        using var factory = new CisApiFactory(ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        var apiDatabase = factory.Services.GetRequiredService<IMongoDatabase>();
        Assert.Equal(Database.DatabaseNamespace.DatabaseName, apiDatabase.DatabaseNamespace.DatabaseName);
        var topics = await ConcurrentRequests.RunAsync(50, async index =>
        {
            using var response = await client.PostAsJsonAsync("/api/topics", new CreateTopicRequest
            {
                Title = $"Concurrent topic {index:D2}",
                Description = "Created through HTTP and persisted in MongoDB."
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var topic = await response.Content.ReadFromJsonAsync<TopicResponse>();
            Assert.NotNull(topic);
            Assert.Equal($"Concurrent topic {index:D2}", topic.Title);
            Assert.Equal(TopicStatus.Active, topic.Status);
            Assert.Equal("Integration author", topic.AuthorName);
            return topic;
        });

        Assert.Equal(50, topics.Select(t => t.Id).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 50).Select(id => (long)id), topics.Select(t => t.Id).Order());
        var stored = await Documents("topics").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(50, stored.Count);
        Assert.All(stored, document => Assert.Equal(TestData.UserId, document["userId"].AsString));
        Assert.Equal(topics.Select(t => t.Id).Order(), stored.Select(d => d["_id"].ToInt64()).Order());

        var reads = await ConcurrentRequests.RunAsync(50, async index =>
        {
            using var response = await client.GetAsync($"/api/topics/{topics[index].Id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var topic = await response.Content.ReadFromJsonAsync<TopicResponse>();
            Assert.NotNull(topic);
            Assert.Equal(topics[index].Id, topic.Id);
            Assert.Equal(topics[index].Title, topic.Title);
            Assert.Equal("Created through HTTP and persisted in MongoDB.", topic.Description);
            return topic.Id;
        });
        Assert.Equal(50, reads.Distinct().Count());
        using var listResponse = await client.GetAsync("/api/topics?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<TopicResponse>>();
        Assert.NotNull(page);
        Assert.Equal(50L, page.TotalItems);
        Assert.Equal(50, page.Items.Count);
        using var missingResponse = await client.GetAsync($"/api/topics/{long.MaxValue}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    private static string CreateToken(
        string subject = TestData.UserId, string signingKey = CisApiFactory.SigningKey,
        DateTime? expires = null, string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), algorithm);
        var token = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.Sub, subject)],
            notBefore: DateTime.UtcNow.AddHours(-1),
            expires: expires ?? DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
