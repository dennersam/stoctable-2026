using Stoctable.Application.Results;
using Stoctable.Communication.Responses.Dashboard;
using Stoctable.Domain.Contracts.Repositories;

namespace Stoctable.Application.Services.Dashboard;

public class DashboardService(IDashboardRepository repository, TimeProvider clock)
{
    private const int TopLimit = 10;
    private const int PendingLimit = 5;
    private const int LowStockLimit = 10;
    private const int DeadStockDays = 180;
    private const string MissingSalesperson = "Sem vendedor";

    // O servidor roda em UTC, mas "hoje" e "este mês" são os da loja.
    private static readonly TimeZoneInfo StoreTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<Result<SalespersonDashboardResponse>> GetSalespersonAsync(Guid salespersonId, CancellationToken ct = default)
    {
        var period = CurrentPeriod();
        var sales = await repository.GetPaidSalesAsync(period.PreviousMonthStart, period.TomorrowStart, salespersonId, ct);
        var quotations = await repository.GetOpenQuotationsAsync(salespersonId, ct);

        return Result<SalespersonDashboardResponse>.Success(new SalespersonDashboardResponse(
            Today: Summarize(sales, period.TodayStart, period.TomorrowStart),
            Month: Summarize(sales, period.MonthStart, period.TomorrowStart),
            PreviousMonthToDate: Summarize(sales, period.PreviousMonthStart, period.PreviousMonthToDateEnd),
            DailySales: DailySeries(sales, period),
            OpenQuotations: new OpenQuotationsResponse(quotations.Count, quotations.Amount)));
    }

    public async Task<Result<CashierDashboardResponse>> GetCashierAsync(CancellationToken ct = default)
    {
        var period = CurrentPeriod();
        var receipts = await repository.GetReceiptsByMethodAsync(period.TodayStart, period.TomorrowStart, ct);
        var pending = await repository.GetPendingSalesAsync(PendingLimit, ct);
        var refunds = await repository.GetRefundsAsync(period.TodayStart, period.TomorrowStart, ct);

        return Result<CashierDashboardResponse>.Success(new CashierDashboardResponse(
            ReceivedToday: receipts.Sum(r => r.Amount),
            PaymentsToday: receipts.Sum(r => r.Count),
            ByMethod: receipts.Select(ToResponse).ToList(),
            PendingSales: ToResponse(pending),
            RefundsToday: new RefundsResponse(refunds.Count, refunds.Amount)));
    }

    public async Task<Result<AdminDashboardResponse>> GetAdminAsync(CancellationToken ct = default)
    {
        var period = CurrentPeriod();
        var sales = await repository.GetPaidSalesAsync(period.PreviousMonthStart, period.TomorrowStart, null, ct);
        var margin = await repository.GetMarginAsync(period.MonthStart, period.TomorrowStart, ct);
        var topProducts = await repository.GetTopProductsAsync(period.MonthStart, period.TomorrowStart, TopLimit, ct);
        var topSalespeople = await repository.GetTopSalespeopleAsync(period.MonthStart, period.TomorrowStart, TopLimit, ct);
        var receipts = await repository.GetReceiptsByMethodAsync(period.MonthStart, period.TomorrowStart, ct);
        var quotations = await repository.GetOpenQuotationsAsync(null, ct);
        var pending = await repository.GetPendingSalesAsync(PendingLimit, ct);
        var stock = await repository.GetStockHealthAsync(clock.GetUtcNow().AddDays(-DeadStockDays), LowStockLimit, ct);

        return Result<AdminDashboardResponse>.Success(new AdminDashboardResponse(
            Today: Summarize(sales, period.TodayStart, period.TomorrowStart),
            Month: Summarize(sales, period.MonthStart, period.TomorrowStart),
            PreviousMonthToDate: Summarize(sales, period.PreviousMonthStart, period.PreviousMonthToDateEnd),
            DailySales: DailySeries(sales, period),
            Margin: ToResponse(margin),
            TopProducts: topProducts
                .Select(p => new TopProductResponse(p.ProductId, p.Sku, p.Name, p.Quantity, p.Revenue))
                .ToList(),
            TopSalespeople: topSalespeople
                .Select(s => new TopSalespersonResponse(s.Name ?? MissingSalesperson, s.Revenue, s.Count))
                .ToList(),
            ByMethod: receipts.Select(ToResponse).ToList(),
            OpenQuotations: new OpenQuotationsResponse(quotations.Count, quotations.Amount),
            PendingSales: ToResponse(pending),
            Stock: new StockHealthResponse(
                stock.LowCount,
                stock.OutCount,
                stock.DeadCount,
                stock.DeadValue,
                stock.LowItems
                    .Select(i => new LowStockItemResponse(i.ProductId, i.Sku, i.Name, i.Quantity, i.Minimum))
                    .ToList())));
    }

    private Period CurrentPeriod()
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, StoreTimeZone).DateTime);
        var monthStart = StartOfDay(new DateOnly(today.Year, today.Month, 1));
        var previousMonthStart = StartOfDay(new DateOnly(today.Year, today.Month, 1).AddMonths(-1));

        // Compara com o mesmo tempo decorrido do mês anterior. Contra o mês
        // inteiro, todo mês em andamento pareceria queda.
        var previousMonthToDateEnd = previousMonthStart + (now - monthStart);
        if (previousMonthToDateEnd > monthStart)
            previousMonthToDateEnd = monthStart;

        return new Period(
            Today: today,
            TodayStart: StartOfDay(today),
            TomorrowStart: StartOfDay(today.AddDays(1)),
            FirstDayOfMonth: new DateOnly(today.Year, today.Month, 1),
            MonthStart: monthStart,
            PreviousMonthStart: previousMonthStart,
            PreviousMonthToDateEnd: previousMonthToDateEnd);
    }

    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, StoreTimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static SalesSummaryResponse Summarize(IEnumerable<PaidSaleRow> sales, DateTimeOffset from, DateTimeOffset to)
    {
        var inRange = sales.Where(s => s.CompletedAt >= from && s.CompletedAt < to).ToList();
        var revenue = inRange.Sum(s => s.TotalAmount);
        var ticket = inRange.Count == 0 ? 0m : Math.Round(revenue / inRange.Count, 2);
        return new SalesSummaryResponse(revenue, inRange.Count, ticket);
    }

    private static List<DailySalesResponse> DailySeries(IEnumerable<PaidSaleRow> sales, Period period)
    {
        var byDay = sales
            .Where(s => s.CompletedAt >= period.MonthStart && s.CompletedAt < period.TomorrowStart)
            .GroupBy(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.CompletedAt, StoreTimeZone).DateTime))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(s => s.TotalAmount), Count: g.Count()));

        var days = period.Today.DayNumber - period.FirstDayOfMonth.DayNumber + 1;
        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = period.FirstDayOfMonth.AddDays(offset);
                var (revenue, count) = byDay.GetValueOrDefault(day);
                return new DailySalesResponse(day, revenue, count);
            })
            .ToList();
    }

    private static MarginResponse ToResponse(MarginRow margin)
    {
        var profit = margin.Revenue - margin.Cost;
        var percent = margin.Revenue == 0 ? 0m : Math.Round(profit / margin.Revenue * 100, 1);
        return new MarginResponse(margin.Revenue, profit, percent);
    }

    private static PaymentMethodTotalResponse ToResponse(PaymentMethodRow row)
        => new(row.Method, row.Amount, row.Count);

    private static PendingSalesResponse ToResponse(PendingSalesRow pending)
        => new(
            pending.Count,
            pending.AmountRemaining,
            pending.Oldest
                .Select(s => new PendingSaleResponse(s.SaleId, s.SaleNumber, s.CustomerName, s.AmountRemaining, s.CreatedAt))
                .ToList());

    private readonly record struct Period(
        DateOnly Today,
        DateTimeOffset TodayStart,
        DateTimeOffset TomorrowStart,
        DateOnly FirstDayOfMonth,
        DateTimeOffset MonthStart,
        DateTimeOffset PreviousMonthStart,
        DateTimeOffset PreviousMonthToDateEnd);
}
