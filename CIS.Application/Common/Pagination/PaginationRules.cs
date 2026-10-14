namespace CIS.Application.Common.Pagination
{
    public static class PaginationRules
    {
        public const int DefaultPage = 1;
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 100;
        public const int MaxPage = 1_000_000;

        public static void Validate(int page, int pageSize)
        {
            if (page < 1 || page > MaxPage)
            {
                throw new ArgumentException($"Page must be between 1 and {MaxPage}.", nameof(page));
            }

            if (pageSize < 1 || pageSize > MaxPageSize)
            {
                throw new ArgumentException($"Page size must be between 1 and {MaxPageSize}.", nameof(pageSize));
            }
        }

        public static int CalculateSkip(int page, int pageSize)
        {
            Validate(page, pageSize);
            return (page - 1) * pageSize;
        }
    }
}
