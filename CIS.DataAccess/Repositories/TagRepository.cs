using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.Context;
using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIS.DataAccess.Repositories
{
    public sealed class TagRepository : ITagRepository
    {
        private readonly CisDbContext _context;

        public TagRepository(CisDbContext context)
        {
            _context = context;
        }

        public async Task<Tag?> GetByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken = default)
        {
            return await _context.Tags
                .FirstOrDefaultAsync(
                    tag => tag.NormalizedName == normalizedName,
                    cancellationToken);
        }
    }
}
