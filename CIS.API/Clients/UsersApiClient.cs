using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CIS.Application.Abstractions.Persistence;

namespace CIS_WebAPI.Clients
{
    public sealed class UsersApiClient : IUsersApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UsersApiClient> _logger;

        public UsersApiClient(HttpClient httpClient, ILogger<UsersApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        public async Task<IReadOnlyDictionary<string, string>> GetNamesByIdsAsync(
            IEnumerable<string> userIds,
            CancellationToken cancellationToken = default)
        {
            var ids = userIds.Distinct().ToList();

            var results = await Task.WhenAll(
                ids.Select(id => GetNameAsync(id, cancellationToken)));

            return results
                .Where(r => r.Name is not null)
                .ToDictionary(r => r.Id, r => r.Name!);
        }
        private async Task<(string Id, string? Name)> GetNameAsync(
            string id,
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    $"users/{Uri.EscapeDataString(id)}",
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (id, null);
                }

                response.EnsureSuccessStatusCode();

                var user = await response.Content.ReadFromJsonAsync<UsersApiUser>(
                    cancellationToken);

                return (id, user?.Name);
            }
            catch (Exception ex) when (
                !cancellationToken.IsCancellationRequested &&
                ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "Could not resolve the name of user {UserId}.", id);
                return (id, null);
            }
        }
        private sealed class UsersApiUser
        {
            public string? Name { get; init; }
        }
    }
}