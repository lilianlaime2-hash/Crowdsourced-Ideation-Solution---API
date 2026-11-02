using CIS.Application.Common.Pagination;

namespace CIS.Application.Hypermedia
{
    public static class PaginationLinkFactory
    {
        public static PaginationLinks Create(int page, int totalPages, Func<int, string?> createPageUrl)
        {
            ArgumentNullException.ThrowIfNull(createPageUrl);

            var self = createPageUrl(page) ?? throw new InvalidOperationException("Unable to generate pagination link.");

            if (totalPages == 0)
            {
                return new PaginationLinks
                {
                    Self = self
                };
            }

            return new PaginationLinks
            {
                Self = self,

                First = createPageUrl(1),

                Previous = page > 1 && page <= totalPages ? createPageUrl(page - 1) : null,

                Next = page < totalPages ? createPageUrl(page + 1) : null,

                Last = createPageUrl(totalPages)
            };
        }
    }
}
