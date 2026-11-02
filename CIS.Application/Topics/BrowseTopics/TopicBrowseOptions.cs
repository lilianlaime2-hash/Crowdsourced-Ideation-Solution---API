using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Domain.Enums;

namespace CIS.Application.Topics.BrowseTopics
{
    public sealed class TopicBrowseOptions
    {
        public int Page { get; init; } = PaginationRules.DefaultPage;

        public int PageSize { get; init; } = PaginationRules.DefaultPageSize;

        public TopicStatus? Status { get; init; }

        public string? UserId { get; init; }

        public string? Tag { get; init; }

        public string? Search { get; init; }

        public TopicSortField SortBy { get; init; } = TopicSortField.CreatedAt;

        public SortDirection SortDirection { get; init; } = SortDirection.Desc;
    }
}
