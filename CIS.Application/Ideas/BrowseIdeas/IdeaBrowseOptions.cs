using CIS.Application.Common.Pagination;
using CIS.Application.Common.Sorting;
using CIS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Ideas.BrowseIdeas
{
    public sealed class IdeaBrowseOptions
    {
        public int Page { get; init; } = PaginationRules.DefaultPage;

        public int PageSize { get; init; } = PaginationRules.DefaultPageSize;

        public IdeaStatus? Status { get; init; }

        public string? UserId { get; init; }

        public string? Search { get; init; }

        public IdeaSortField SortBy { get; init; } = IdeaSortField.CreatedAt;

        public SortDirection SortDirection { get; init; } = SortDirection.Desc;
    }
}
