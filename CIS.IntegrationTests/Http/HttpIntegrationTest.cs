using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CIS.Domain.Entities;
using CIS.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CIS.IntegrationTests.Http;

public abstract class HttpIntegrationTest(MongoDbFixture fixture) : MongoIntegrationTest(fixture)
{
    private protected CisApiFactory Factory() => new(ConnectionString);

    protected static HttpClient Client(WebApplicationFactory<Program> factory, string? userId = TestData.UserId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        if (userId is not null)
        {
            var token = new JwtSecurityToken(
                claims: [new Claim(JwtRegisteredClaimNames.Sub, userId)],
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CisApiFactory.SigningKey)),
                    SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }

    protected Task<Topic> SeedTopic(string title = "HTTP topic") => Topics.AddAsync(TestData.Topic(title));
    protected Task<Idea> SeedIdea(long topicId, string title = "HTTP idea") => Ideas.AddAsync(TestData.Idea(topicId, title));
    protected Task<long> Count(string collection) => Documents(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
    protected static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
