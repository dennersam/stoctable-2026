using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class SaleRepository(StoctableDbContext context, NumberSequenceGenerator sequenceGenerator)
    : Repository<Sale>(context), ISaleRepository
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
        var prefix = $"VDA{DateTime.UtcNow:yyyyMM}";
        var next = await sequenceGenerator.NextAsync(prefix, ct);
        return $"{prefix}{next:D4}";
    }

    // Mesmo tratamento de QuotationRepository.UpdateAsync, aqui pelos Payments.
    //
    // EF Core 9: entidades novas cuja PK Guid já vem preenchida por BaseEntity
    // (Id = Guid.NewGuid()) entram como Modified — e não Added — ao serem
    // adicionadas a uma coleção rastreada, como em sale.Payments.Add(...) no
    // ProcessPaymentAsync. O SaveChanges emite UPDATE payments WHERE id = ...
    // numa linha que ainda não existe, afeta 0 linhas e estoura
    // DbUpdateConcurrencyException ao confirmar o pagamento no caixa.
    //
    // Um Modified em que TODAS as propriedades têm OriginalValue == CurrentValue
    // nunca veio do banco: uma entidade de fato alterada teria ao menos uma
    // diferença. Reclassificar esses casos como Added gera o INSERT correto e
    // deixa intactos os UPDATEs legítimos (ex.: o estorno em CancelAsync, que
    // muda Payment.Status e RefundedAt).
    public override async Task UpdateAsync(Sale entity, CancellationToken ct = default)
    {
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
