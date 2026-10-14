using CIS.Application.Common.Pagination;

namespace CIS.Application.Topics.GetTopic
{
    public interface IGetTopicService
    {
        Task<TopicResponse?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TopicResponse>> GetAllAsync(
            CancellationToken cancellationToken = default);
        Task<PagedResponse<TopicResponse>> GetPagedAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}