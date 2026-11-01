namespace Stoctable.Domain.Contracts.Repositories;

public record PaidSaleRow(DateTimeOffset CompletedAt, decimal TotalAmount);

public record OpenQuotationsRow(int Count, decimal Amount);

public record PaymentMethodRow(string Method, decimal Amount, int Count);

public record PendingSaleRow(
    Guid SaleId,
    string SaleNumber,
    string? CustomerName,
    decimal AmountRemaining,
    DateTimeOffset CreatedAt);

public record PendingSalesRow(int Count, decimal AmountRemaining, IReadOnlyList<PendingSaleRow> Oldest);

public record RefundsRow(int Count, decimal Amount);

public record TopProductRow(Guid ProductId, string Sku, string Name, decimal Quantity, decimal Revenue);

public record TopSalespersonRow(string? Name, decimal Revenue, int Count);

public record MarginRow(decimal Revenue, decimal Cost);

public record LowStockRow(Guid ProductId, string Sku, string Name, decimal Quantity, decimal Minimum);

public record StockHealthRow(
    int LowCount,
    int OutCount,
    int DeadCount,
    decimal DeadValue,
    IReadOnlyList<LowStockRow> LowItems);

/// <summary>
/// Consultas agregadas dos painéis. Todos os intervalos são [from, to) em UTC: o
/// corte de dia e de mês é regra de negócio e fica no serviço.
/// </summary>
public interface IDashboardRepository
{
    Task<IReadOnlyList<PaidSaleRow>> GetPaidSalesAsync(DateTimeOffset from, DateTimeOffset to, Guid? salespersonId, CancellationToken ct = default);
    Task<OpenQuotationsRow> GetOpenQuotationsAsync(Guid? salespersonId, CancellationToken ct = default);
    Task<PendingSalesRow> GetPendingSalesAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentMethodRow>> GetReceiptsByMethodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<RefundsRow> GetRefundsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default);
    Task<IReadOnlyList<TopSalespersonRow>> GetTopSalespeopleAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default);
    Task<MarginRow> GetMarginAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<StockHealthRow> GetStockHealthAsync(DateTimeOffset deadStockCutoff, int take, CancellationToken ct = default);
}
