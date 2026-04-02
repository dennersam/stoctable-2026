using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class SupplierRepository(StoctableDbContext context) : Repository<Supplier>(context), ISupplierRepository
{
    public async Task<Supplier?> GetByCnpjAsync(string cnpj, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(s => s.Cnpj == cnpj, ct);

    public async Task<IEnumerable<Supplier>> SearchAsync(string query, CancellationToken ct = default)
    {
        var lower = query.ToLower();
        return await DbSet
            .Where(s => s.IsActive && (
                s.CompanyName.ToLower().Contains(lower) ||
                (s.TradeName != null && s.TradeName.ToLower().Contains(lower)) ||
                (s.Cnpj != null && s.Cnpj.Contains(lower))))
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<(IEnumerable<Supplier> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(s =>
                s.CompanyName.ToLower().Contains(lower) ||
                (s.TradeName != null && s.TradeName.ToLower().Contains(lower)) ||
                (s.Cnpj != null && s.Cnpj.Contains(lower)) ||
                (s.Phone != null && s.Phone.Contains(lower)));
        }
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.CompanyName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }
}
