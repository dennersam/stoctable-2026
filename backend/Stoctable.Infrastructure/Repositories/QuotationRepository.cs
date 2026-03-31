using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class QuotationRepository(StoctableDbContext context) : Repository<Quotation>(context), IQuotationRepository
{
    public async Task<Quotation?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(q => q.Customer)
            .Include(q => q.Salesperson)
            .Include(q => q.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IEnumerable<Quotation>> GetByStatusAsync(QuotationStatus status, CancellationToken ct = default)
        => await DbSet
            .Where(q => q.Status == status)
            .Include(q => q.Customer)
            .Include(q => q.Salesperson)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

    public async Task<string> GenerateNextNumberAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var prefix = $"ORC{today:yyyyMM}";
        var count = await DbSet.CountAsync(q => q.QuotationNumber.StartsWith(prefix), ct);
        return $"{prefix}{(count + 1):D4}";
    }
}
