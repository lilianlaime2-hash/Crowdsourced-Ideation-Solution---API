using CIS.Application.Common.Pagination;
using CIS.Application.Ideas.BrowseIdeas;
using CIS.Application.Ideas.CreateIdea;

namespace CIS.Application.Ideas.GetIdeas;

public interface IGetIdeasService
{
    Task<IReadOnlyList<CreateIdeaResponse>?> GetByTopicIdAsync(
        long topicId,
        CancellationToken cancellationToken = default);
    Task<PagedResponse<CreateIdeaResponse>?> GetPagedByTopicIdAsync(
        long topicId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<PagedResponse<CreateIdeaResponse>?> BrowseAsync(
        long topicId,
        IdeaBrowseOptions options,
        CancellationToken cancellationToken = default);
}