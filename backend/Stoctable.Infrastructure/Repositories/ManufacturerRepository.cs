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
}
