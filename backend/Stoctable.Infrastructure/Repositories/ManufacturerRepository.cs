using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class ManufacturerRepository(StoctableDbContext context) : Repository<Manufacturer>(context), IManufacturerRepository
{
    public async Task<IEnumerable<Manufacturer>> GetActiveAsync(CancellationToken ct = default)
        => await DbSet.Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync(ct);

    public async Task<IEnumerable<Manufacturer>> SearchAsync(string query, CancellationToken ct = default)
    {
        var lower = query.ToLower();
        return await DbSet
            .Where(m => m.IsActive && m.Name.ToLower().Contains(lower))
            .OrderBy(m => m.Name)
            .Take(30)
            .ToListAsync(ct);
    }

    public async Task<(IEnumerable<Manufacturer> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(m => m.Name.ToLower().Contains(lower));
        }
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }
}
