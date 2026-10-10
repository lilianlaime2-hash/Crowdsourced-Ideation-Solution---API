namespace CIS.Application.Abstractions.Persistence
{
    public interface IUsersApiClient
    {
        Task<IReadOnlyDictionary<string, string>> GetNamesByIdsAsync(
            IEnumerable<string> userIds,
            CancellationToken cancellationToken = default);
    }
}