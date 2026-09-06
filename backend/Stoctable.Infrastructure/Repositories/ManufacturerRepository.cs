using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Search;

namespace Stoctable.Infrastructure.Repositories;

public class ManufacturerRepository(StoctableDbContext context) : Repository<Manufacturer>(context), IManufacturerRepository
{
    private static readonly Expression<Func<Manufacturer, string?>>[] SearchFields =
    [
        m => m.Name,
    ];

    public async Task<IEnumerable<Manufacturer>> GetActiveAsync(CancellationToken ct = default)
        => await DbSet.Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync(ct);

    public async Task<IEnumerable<Manufacturer>> SearchAsync(string query, CancellationToken ct = default)
        => await DbSet
            .Where(m => m.IsActive)
            .WhereMatchesAllTokens(query, SearchFields)
            .OrderBy(m => m.Name)
            .Take(30)
            .ToListAsync(ct);

    public async Task<(IEnumerable<Manufacturer> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable().WhereMatchesAllTokens(search, SearchFields);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }
}
