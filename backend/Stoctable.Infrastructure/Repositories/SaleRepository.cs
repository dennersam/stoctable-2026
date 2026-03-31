using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class SaleRepository(StoctableDbContext context) : Repository<Sale>(context), ISaleRepository
{
    public async Task<Sale?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Customer)
            .Include(s => s.Salesperson)
            .Include(s => s.Cashier)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Payments).ThenInclude(p => p.PaymentMethod)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IEnumerable<Sale>> GetByStatusAsync(SaleStatus status, CancellationToken ct = default)
        => await DbSet
            .Where(s => s.Status == status)
            .Include(s => s.Customer)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<string> GenerateNextNumberAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var prefix = $"VDA{today:yyyyMM}";
        var count = await DbSet.CountAsync(s => s.SaleNumber.StartsWith(prefix), ct);
        return $"{prefix}{(count + 1):D4}";
    }
}
