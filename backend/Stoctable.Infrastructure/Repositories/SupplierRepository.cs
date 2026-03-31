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
}
