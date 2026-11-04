using CIS.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CIS.IntegrationTests.Infrastructure;

internal sealed class CisApiFactory(string mongoConnectionString) : WebApplicationFactory<Program>
{
    // Deliberately test-only key; independent from production and developer secrets.
    internal const string SigningKey = "cis-integration-tests-signing-key-at-least-32-bytes-long";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Persistence:Provider", "MongoDB");
        builder.UseSetting("ConnectionStrings:MongoDatabase", mongoConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("UsersApi:BaseUrl", "http://users-api.test");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.ConfigureServices(services =>
        {
            // Only the independent Users API is replaced. JWT and persistence stay real.
            services.RemoveAll<IUsersApiClient>();
            services.AddSingleton<IUsersApiClient, TestUsersApiClient>();
        });
    }

    private sealed class TestUsersApiClient : IUsersApiClient
    {
        public Task<IReadOnlyDictionary<string, string>> GetNamesByIdsAsync(
            IEnumerable<string> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                userIds.Distinct().ToDictionary(id => id, _ => "Integration author"));
    }
}
