using CIS.Domain.Entities;

namespace CIS.Application.Abstractions.Persistence;

public interface IVoteRepository
{
    Task<bool> ExistsAsync(long ideaId, string userId, CancellationToken cancellationToken = default);

    Task<Vote> AddAsync(Vote vote, CancellationToken cancellationToken = default);
}