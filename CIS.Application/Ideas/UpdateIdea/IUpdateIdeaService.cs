using CIS.Application.Ideas.CreateIdea;

namespace CIS.Application.Ideas.UpdateIdea;

public interface IUpdateIdeaService
{
    Task<CreateIdeaResponse?> ExecuteAsync(
        long id,
        CreateIdeaRequest request,
        CancellationToken cancellationToken = default);
}