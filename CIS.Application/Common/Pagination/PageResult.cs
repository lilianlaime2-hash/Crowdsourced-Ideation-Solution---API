using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Common.Pagination
{
    public sealed record PageResult<T>(IReadOnlyList<T> Items, long TotalItems);
}
