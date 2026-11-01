using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

/// <summary>
/// Toda consulta de item parte de <c>Sales</c>: SaleItem não tem filtro de filial
/// e só é isolado pelo pai. Consultar <c>context.SaleItems</c> direto somaria as
/// vendas de todas as lojas da empresa, sem nenhum erro visível.
/// </summary>
public class DashboardRepository(StoctableDbContext context) : IDashboardRepository
{
    public async Task<IReadOnlyList<PaidSaleRow>> GetPaidSalesAsync(
        DateTimeOffset from, DateTimeOffset to, Guid? salespersonId, CancellationToken ct = default)
    {
        var query = PaidSales(from, to);
        if (salespersonId is not null)
            query = query.Where(s => s.SalespersonId == salespersonId);

        return await query
            .Select(s => new PaidSaleRow(s.CompletedAt!.Value, s.TotalAmount))
            .ToListAsync(ct);
    }

    public async Task<OpenQuotationsRow> GetOpenQuotationsAsync(Guid? salespersonId, CancellationToken ct = default)
    {
        var query = context.Quotations.AsNoTracking()
            .Where(q => q.Status == QuotationStatus.Draft || q.Status == QuotationStatus.Finalized);
        if (salespersonId is not null)
            query = query.Where(q => q.SalespersonId == salespersonId);

        var row = await query
            .GroupBy(_ => 1)
            .Select(g => new OpenQuotationsRow(g.Count(), g.Sum(q => q.TotalAmount)))
            .FirstOrDefaultAsync(ct);

        return row ?? new OpenQuotationsRow(0, 0m);
    }

    public async Task<PendingSalesRow> GetPendingSalesAsync(int take, CancellationToken ct = default)
    {
        var pending = context.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.PendingPayment || s.Status == SaleStatus.PartiallyPaid);

        var totals = await pending
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Remaining = g.Sum(s => s.TotalAmount - s.AmountPaid) })
            .FirstOrDefaultAsync(ct);

        var oldest = await pending
            .OrderBy(s => s.CreatedAt)
            .Take(take)
            .Select(s => new PendingSaleRow(
                s.Id,
                s.SaleNumber,
                s.Customer != null ? s.Customer.FullName : null,
                s.TotalAmount - s.AmountPaid,
                s.CreatedAt))
            .ToListAsync(ct);

        return new PendingSalesRow(totals?.Count ?? 0, totals?.Remaining ?? 0m, oldest);
    }

    public async Task<IReadOnlyList<PaymentMethodRow>> GetReceiptsByMethodAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        => await context.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= from && p.PaidAt < to)
            .Select(p => new { Method = p.PaymentMethod!.Name, p.Amount })
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .OrderByDescending(r => r.Amount)
            .Select(r => new PaymentMethodRow(r.Method, r.Amount, r.Count))
            .ToListAsync(ct);

    public async Task<RefundsRow> GetRefundsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var row = await context.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Refunded && p.RefundedAt >= from && p.RefundedAt < to)
            .GroupBy(_ => 1)
            .Select(g => new RefundsRow(g.Count(), g.Sum(p => p.Amount)))
            .FirstOrDefaultAsync(ct);

        return row ?? new RefundsRow(0, 0m);
    }

    public async Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(
        DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default)
        => await PaidSales(from, to)
            .SelectMany(s => s.Items)
            .Select(i => new { i.ProductId, i.Product!.Sku, i.Product.Name, i.Quantity, i.LineTotal })
            .GroupBy(i => new { i.ProductId, i.Sku, i.Name })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Sku,
                g.Key.Name,
                Quantity = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.LineTotal),
            })
            .OrderByDescending(r => r.Revenue)
            .Take(take)
            .Select(r => new TopProductRow(r.ProductId, r.Sku, r.Name, r.Quantity, r.Revenue))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TopSalespersonRow>> GetTopSalespeopleAsync(
        DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default)
        => await PaidSales(from, to)
            .Select(s => new
            {
                s.SalespersonId,
                Name = s.Salesperson != null ? s.Salesperson.FullName : null,
                s.TotalAmount,
            })
            .GroupBy(s => new { s.SalespersonId, s.Name })
            .Select(g => new { g.Key.Name, Revenue = g.Sum(s => s.TotalAmount), Count = g.Count() })
            .OrderByDescending(r => r.Revenue)
            .Take(take)
            .Select(r => new TopSalespersonRow(r.Name, r.Revenue, r.Count))
            .ToListAsync(ct);

    public async Task<MarginRow> GetMarginAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var row = await PaidSales(from, to)
            .SelectMany(s => s.Items)
            .Where(i => i.Product!.CostPrice > 0)
            .Select(i => new { i.LineTotal, Cost = i.Quantity * i.Product!.CostPrice })
            .GroupBy(_ => 1)
            .Select(g => new MarginRow(g.Sum(i => i.LineTotal), g.Sum(i => i.Cost)))
            .FirstOrDefaultAsync(ct);

        return row ?? new MarginRow(0m, 0m);
    }

    public async Task<StockHealthRow> GetStockHealthAsync(DateTimeOffset deadStockCutoff, int take, CancellationToken ct = default)
    {
        // Mínimo zero é "não configurado", não "repor a partir de zero". Sem essa
        // exclusão, todo produto importado sem saldo apareceria como estoque baixo.
        var low = context.ProductStocks.AsNoTracking()
            .Where(s => s.Minimum > 0 && s.Quantity <= s.Minimum && s.Product!.IsActive);

        var lowCount = await low.CountAsync(ct);
        var outCount = await low.CountAsync(s => s.Quantity <= 0, ct);
        var lowItems = await low
            .OrderBy(s => s.Quantity)
            .ThenBy(s => s.Product!.Name)
            .Take(take)
            .Select(s => new LowStockRow(s.ProductId, s.Product!.Sku, s.Product.Name, s.Quantity, s.Minimum))
            .ToListAsync(ct);

        var recentlySold = context.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Paid && s.CompletedAt >= deadStockCutoff)
            .SelectMany(s => s.Items);

        var dead = await context.ProductStocks.AsNoTracking()
            .Where(s => s.Quantity > 0
                && s.Product!.IsActive
                && !recentlySold.Any(i => i.ProductId == s.ProductId))
            .Select(s => new { Value = s.Quantity * s.Product!.CostPrice })
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Value = g.Sum(s => s.Value) })
            .FirstOrDefaultAsync(ct);

        return new StockHealthRow(lowCount, outCount, dead?.Count ?? 0, dead?.Value ?? 0m, lowItems);
    }

    private IQueryable<Sale> PaidSales(DateTimeOffset from, DateTimeOffset to)
        => context.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Paid && s.CompletedAt >= from && s.CompletedAt < to);
}
