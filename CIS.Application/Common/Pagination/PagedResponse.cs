using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace CIS.Application.Common.Pagination
{
    public sealed class PagedResponse<T>
    {
        public IReadOnlyList<T> Items { get; init; } = [];

        public int Page { get; init; }

        public int PageSize { get; init; }

        public long TotalItems { get; init; }

        public int TotalPages { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PaginationLinks? Links { get; init; }

        public static PagedResponse<T> Create(
            IReadOnlyList<T> items,
            int page,
            int pageSize,
            long totalItems)
        {
            return new PagedResponse<T>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalItems == 0
                    ? 0
                    : (int)Math.Ceiling(totalItems / (double)pageSize)
            };
        }

        public PagedResponse<T> WithLinks(PaginationLinks links)
        {
            ArgumentNullException.ThrowIfNull(links);

            return new PagedResponse<T>
            {
                Items = Items,
                Page = Page,
                PageSize = PageSize,
                TotalItems = TotalItems,
                TotalPages = TotalPages,
                Links = links
            };
        }
    }
}
