using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Search;

namespace Stoctable.Infrastructure.Repositories;

public class SupplierRepository(StoctableDbContext context) : Repository<Supplier>(context), ISupplierRepository
{
    private static readonly Expression<Func<Supplier, string?>>[] SearchFields =
    [
        s => s.CompanyName,
        s => s.TradeName,
        s => s.Cnpj,
        s => s.Phone,
    ];

    public async Task<Supplier?> GetByCnpjAsync(string cnpj, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(s => s.Cnpj == cnpj, ct);

    public async Task<IEnumerable<Supplier>> SearchAsync(string query, CancellationToken ct = default)
        => await DbSet
            .Where(s => s.IsActive)
            .WhereMatchesAllTokens(query, SearchFields)
            .OrderBy(s => s.CompanyName)
            .Take(50)
            .ToListAsync(ct);

    public async Task<(IEnumerable<Supplier> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable().WhereMatchesAllTokens(search, SearchFields);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.CompanyName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }
}
