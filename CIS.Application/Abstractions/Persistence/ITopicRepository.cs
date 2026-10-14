using CIS.Application.Common.Pagination;
using CIS.Application.Topics.BrowseTopics;
using CIS.Domain.Entities;

namespace CIS.Application.Abstractions.Persistence
{
    public interface ITopicRepository
    {
        Task<Topic> AddAsync(
            Topic topic,
            CancellationToken cancellationToken = default);
        Task<Topic?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Topic>> GetAllAsync(
            CancellationToken cancellationToken = default);
        Task UpdateAsync(
            Topic topic,
            CancellationToken cancellationToken = default);
        Task<PageResult<Topic>> GetPagedAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<PageResult<Topic>> BrowseAsync(
            TopicBrowseOptions options,
            CancellationToken cancellationToken = default);
    }
}
