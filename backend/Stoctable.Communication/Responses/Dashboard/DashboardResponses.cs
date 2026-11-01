namespace Stoctable.Communication.Responses.Dashboard;

public record SalesSummaryResponse(decimal Revenue, int Count, decimal AverageTicket);

public record DailySalesResponse(DateOnly Date, decimal Revenue, int Count);

public record OpenQuotationsResponse(int Count, decimal Amount);

public record PaymentMethodTotalResponse(string Method, decimal Amount, int Count);

public record PendingSaleResponse(
    Guid SaleId,
    string SaleNumber,
    string? CustomerName,
    decimal AmountRemaining,
    DateTimeOffset CreatedAt);

public record PendingSalesResponse(int Count, decimal AmountRemaining, List<PendingSaleResponse> Oldest);

public record RefundsResponse(int Count, decimal Amount);

public record TopProductResponse(Guid ProductId, string Sku, string Name, decimal Quantity, decimal Revenue);

public record TopSalespersonResponse(string Name, decimal Revenue, int Count);

/// <summary>
/// Margem aproximada: usa o custo ATUAL do produto, porque a venda não guarda o
/// custo da época. Itens sem custo cadastrado ficam fora da conta.
/// </summary>
public record MarginResponse(decimal Revenue, decimal Profit, decimal Percent);

public record LowStockItemResponse(Guid ProductId, string Sku, string Name, decimal Quantity, decimal Minimum);

public record StockHealthResponse(
    int LowCount,
    int OutCount,
    int DeadCount,
    decimal DeadValue,
    List<LowStockItemResponse> LowItems);

public record SalespersonDashboardResponse(
    SalesSummaryResponse Today,
    SalesSummaryResponse Month,
    SalesSummaryResponse PreviousMonthToDate,
    List<DailySalesResponse> DailySales,
    OpenQuotationsResponse OpenQuotations);

public record CashierDashboardResponse(
    decimal ReceivedToday,
    int PaymentsToday,
    List<PaymentMethodTotalResponse> ByMethod,
    PendingSalesResponse PendingSales,
    RefundsResponse RefundsToday);

public record AdminDashboardResponse(
    SalesSummaryResponse Today,
    SalesSummaryResponse Month,
    SalesSummaryResponse PreviousMonthToDate,
    List<DailySalesResponse> DailySales,
    MarginResponse Margin,
    List<TopProductResponse> TopProducts,
    List<TopSalespersonResponse> TopSalespeople,
    List<PaymentMethodTotalResponse> ByMethod,
    OpenQuotationsResponse OpenQuotations,
    PendingSalesResponse PendingSales,
    StockHealthResponse Stock);
