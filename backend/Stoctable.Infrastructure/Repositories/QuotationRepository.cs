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

    // Override: entity is already loaded and tracked via GetWithItemsAsync.
    // EF's snapshot-based change tracking handles everything automatically:
    //   - Quotation scalar changes (RecalculateTotals) → detected as Modified → UPDATE
    //   - New QuotationItems added to the collection   → detected as Added   → INSERT
    //   - Unchanged existing items                     → no SQL generated
    // Calling DbSet.Update or forcing State = Modified traverses the object graph
    // and can change Added dependents to Modified, causing 0-row UPDATE exceptions.
    public override async Task UpdateAsync(Quotation entity, CancellationToken ct = default)
    {
        // EF Core 9 regression: new entities whose GUID PK is pre-set via BaseEntity
        // (Id = Guid.NewGuid()) are treated as "existing entities being re-attached"
        // when added to a tracked collection, resulting in Modified state instead of
        // Added. This causes an UPDATE that affects 0 rows → DbUpdateConcurrencyException.
        // Fix: any Modified entry where OriginalValue == CurrentValue for ALL properties
        // was never persisted, so re-mark it as Added to generate INSERT instead.
        foreach (var entry in Context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified
                     && e.Entity is not AuditLog
                     && e.Properties.All(p => Equals(p.OriginalValue, p.CurrentValue)))
            .ToList())
        {
            entry.State = EntityState.Added;
        }

        await Context.SaveChangesAsync(ct);
    }
}
