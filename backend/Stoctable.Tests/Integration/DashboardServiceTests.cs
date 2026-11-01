using Stoctable.Application.Services.Dashboard;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Cada teste usa uma filial própria: o filtro global isola as linhas, então um
/// não enxerga as vendas do outro mesmo dividindo o banco.
///
/// O relógio é fixo em 15/10/2026 12:00 de São Paulo. Os cortes de dia e de mês
/// são o que mais erra em silêncio aqui: o servidor roda em UTC, e uma venda das
/// 23h30 de ontem cairia em "hoje" sem nenhum erro visível.
/// </summary>
[Trait("Category", "Integration")]
public class DashboardServiceTests : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.FromHours(-3));

    private readonly PostgresFixture _fixture;
    private readonly BranchContext _filial = new() { BranchId = Guid.NewGuid() };

    public DashboardServiceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ─── Vendedor ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Salesperson_TodayFollowsSaoPauloDay_NotUtc()
    {
        var vendedor = await SeedUserAsync();
        // 02:30 UTC de hoje ainda é 23:30 de ontem em São Paulo.
        await SeedPaidSaleAsync(100m, new DateTimeOffset(2026, 10, 15, 2, 30, 0, TimeSpan.Zero), vendedor);
        // 03:30 UTC já é 00:30 de hoje.
        await SeedPaidSaleAsync(40m, new DateTimeOffset(2026, 10, 15, 3, 30, 0, TimeSpan.Zero), vendedor);

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(40m, dados.Today.Revenue);
        Assert.Equal(1, dados.Today.Count);
        Assert.Equal(140m, dados.Month.Revenue);
    }

    [Fact]
    public async Task Salesperson_MonthFollowsSaoPauloMonth_NotUtc()
    {
        var vendedor = await SeedUserAsync();
        // 02:00 UTC de 01/10 ainda é 30/09 em São Paulo: pertence a setembro.
        await SeedPaidSaleAsync(70m, new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero), vendedor);
        // O mesmo corte no início de setembro: 02:00 UTC ainda é agosto, 03:30 não.
        await SeedPaidSaleAsync(9m, new DateTimeOffset(2026, 9, 1, 2, 0, 0, TimeSpan.Zero), vendedor);
        await SeedPaidSaleAsync(5m, new DateTimeOffset(2026, 9, 1, 3, 30, 0, TimeSpan.Zero), vendedor);

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(0m, dados.Month.Revenue);
        Assert.Equal(5m, dados.PreviousMonthToDate.Revenue);
    }

    [Fact]
    public async Task Salesperson_SeesOnlyOwnSales()
    {
        var eu = await SeedUserAsync();
        var outro = await SeedUserAsync();
        await SeedPaidSaleAsync(100m, Agora.AddHours(-1), eu);
        await SeedPaidSaleAsync(900m, Agora.AddHours(-1), outro);

        var dados = (await Service().GetSalespersonAsync(eu)).Data!;

        Assert.Equal(100m, dados.Today.Revenue);
        Assert.Equal(100m, dados.Month.Revenue);
    }

    [Fact]
    public async Task Salesperson_CountsOnlyPaidSales()
    {
        var vendedor = await SeedUserAsync();
        await SeedPaidSaleAsync(100m, Agora.AddHours(-1), vendedor);
        await SeedSaleAsync(SaleStatus.PendingPayment, 50m, 0m, vendedor);
        await SeedSaleAsync(SaleStatus.PartiallyPaid, 50m, 10m, vendedor);
        await SeedSaleAsync(SaleStatus.Cancelled, 50m, 0m, vendedor, completedAt: Agora.AddHours(-1));

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(100m, dados.Month.Revenue);
        Assert.Equal(1, dados.Month.Count);
    }

    [Fact]
    public async Task Salesperson_AverageTicketDividesRevenueByCount()
    {
        var vendedor = await SeedUserAsync();
        await SeedPaidSaleAsync(100m, Agora.AddHours(-1), vendedor);
        await SeedPaidSaleAsync(50m, Agora.AddDays(-2), vendedor);

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(75m, dados.Month.AverageTicket);
    }

    [Fact]
    public async Task Salesperson_WithoutSales_AverageTicketIsZero()
    {
        var vendedor = await SeedUserAsync();

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(0m, dados.Month.AverageTicket);
    }

    [Fact]
    public async Task Salesperson_PreviousMonthStopsAtSameElapsedTime()
    {
        var vendedor = await SeedUserAsync();
        // 10/09 está dentro dos primeiros 14,5 dias do mês; 20/09 não. Comparar
        // com o mês anterior inteiro faria todo mês em andamento parecer queda.
        await SeedPaidSaleAsync(30m, new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(-3)), vendedor);
        await SeedPaidSaleAsync(500m, new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(-3)), vendedor);

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(30m, dados.PreviousMonthToDate.Revenue);
    }

    [Fact]
    public async Task Salesperson_DailySeriesHasOnePointPerDayUntilToday()
    {
        var vendedor = await SeedUserAsync();
        await SeedPaidSaleAsync(10m, new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.FromHours(-3)), vendedor);
        await SeedPaidSaleAsync(20m, new DateTimeOffset(2026, 10, 3, 22, 0, 0, TimeSpan.FromHours(-3)), vendedor);

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(15, dados.DailySales.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), dados.DailySales[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 15), dados.DailySales[^1].Date);

        // 22h de São Paulo em 03/10 já é 04/10 em UTC; agrupar em UTC separaria
        // as duas vendas em dias diferentes.
        var dia3 = dados.DailySales.Single(d => d.Date == new DateOnly(2026, 10, 3));
        Assert.Equal(30m, dia3.Revenue);
        Assert.Equal(2, dia3.Count);
        Assert.Equal(0m, dados.DailySales.Single(d => d.Date == new DateOnly(2026, 10, 4)).Revenue);
    }

    [Fact]
    public async Task Salesperson_OpenQuotationsAreDraftAndFinalizedOnly()
    {
        var vendedor = await SeedUserAsync();
        var outro = await SeedUserAsync();
        await SeedQuotationAsync(QuotationStatus.Draft, 10m, vendedor);
        await SeedQuotationAsync(QuotationStatus.Finalized, 20m, vendedor);
        await SeedQuotationAsync(QuotationStatus.Converted, 40m, vendedor);
        await SeedQuotationAsync(QuotationStatus.Cancelled, 80m, vendedor);
        await SeedQuotationAsync(QuotationStatus.Draft, 160m, outro);

        var dados = (await Service().GetSalespersonAsync(vendedor)).Data!;

        Assert.Equal(2, dados.OpenQuotations.Count);
        Assert.Equal(30m, dados.OpenQuotations.Amount);
    }

    // ─── Caixa ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cashier_GroupsTodaysReceiptsByPaymentMethod()
    {
        var pix = await SeedPaymentMethodAsync("PIX");
        var dinheiro = await SeedPaymentMethodAsync("Dinheiro");
        var venda = await SeedPaidSaleAsync(180m, Agora.AddHours(-1));

        await SeedPaymentAsync(venda, pix, 100m, Agora.AddHours(-1));
        await SeedPaymentAsync(venda, pix, 50m, Agora.AddHours(-2));
        await SeedPaymentAsync(venda, dinheiro, 30m, Agora.AddHours(-3));
        // 23:30 de ontem em São Paulo: não é recebimento de hoje.
        await SeedPaymentAsync(venda, dinheiro, 999m, new DateTimeOffset(2026, 10, 15, 2, 30, 0, TimeSpan.Zero));

        var dados = (await Service().GetCashierAsync()).Data!;

        Assert.Equal(180m, dados.ReceivedToday);
        Assert.Equal(3, dados.PaymentsToday);
        Assert.Equal(2, dados.ByMethod.Count);
        Assert.Equal(150m, dados.ByMethod[0].Amount);
        Assert.Equal(2, dados.ByMethod[0].Count);
        Assert.Contains("PIX", dados.ByMethod[0].Method);
    }

    [Fact]
    public async Task Cashier_RefundedPaymentIsNotReceiptButCountsAsRefund()
    {
        var pix = await SeedPaymentMethodAsync("PIX");
        var venda = await SeedPaidSaleAsync(80m, Agora.AddHours(-3));
        await SeedPaymentAsync(venda, pix, 80m, Agora.AddHours(-3),
            PaymentStatus.Refunded, refundedAt: Agora.AddHours(-1));

        var dados = (await Service().GetCashierAsync()).Data!;

        Assert.Equal(0m, dados.ReceivedToday);
        Assert.Equal(1, dados.RefundsToday.Count);
        Assert.Equal(80m, dados.RefundsToday.Amount);
    }

    [Fact]
    public async Task Cashier_PendingQueueShowsRemainingAmountOldestFirst()
    {
        var nova = await SeedSaleAsync(SaleStatus.PendingPayment, 100m, 0m, createdAt: Agora.AddHours(-1));
        var antiga = await SeedSaleAsync(SaleStatus.PartiallyPaid, 100m, 60m, createdAt: Agora.AddHours(-5));
        await SeedSaleAsync(SaleStatus.Paid, 100m, 100m, completedAt: Agora);
        await SeedSaleAsync(SaleStatus.Cancelled, 100m, 0m);

        var dados = (await Service().GetCashierAsync()).Data!;

        Assert.Equal(2, dados.PendingSales.Count);
        Assert.Equal(140m, dados.PendingSales.AmountRemaining);
        Assert.Equal(antiga, dados.PendingSales.Oldest[0].SaleId);
        Assert.Equal(40m, dados.PendingSales.Oldest[0].AmountRemaining);
        Assert.Equal(nova, dados.PendingSales.Oldest[1].SaleId);
    }

    // ─── Administrador ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_TopProductsRankByRevenue()
    {
        var parafuso = await SeedProductAsync("Parafuso", custo: 1m);
        var pneu = await SeedProductAsync("Pneu", custo: 100m);
        await SeedPaidSaleAsync(50m, Agora.AddHours(-1), items: [(parafuso, 50m, 50m)]);
        await SeedPaidSaleAsync(400m, Agora.AddHours(-1), items: [(pneu, 2m, 400m)]);

        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal(pneu, dados.TopProducts[0].ProductId);
        Assert.Equal(400m, dados.TopProducts[0].Revenue);
        Assert.Equal(2m, dados.TopProducts[0].Quantity);
        Assert.Equal(parafuso, dados.TopProducts[1].ProductId);
    }

    /// <summary>
    /// SaleItem não tem filtro de filial: só é isolado quando a consulta parte
    /// de Sales. Consultar context.SaleItems direto somaria todas as lojas.
    /// </summary>
    [Fact]
    public async Task Admin_TopProductsIgnoreOtherBranches()
    {
        var produto = await SeedProductAsync("Disputado", custo: 10m);
        var outraFilial = new BranchContext { BranchId = Guid.NewGuid() };
        await SeedPaidSaleAsync(100m, Agora.AddHours(-1), items: [(produto, 1m, 100m)]);
        await SeedPaidSaleAsync(900m, Agora.AddHours(-1), items: [(produto, 9m, 900m)], filial: outraFilial);

        var dados = (await Service().GetAdminAsync()).Data!;

        var linha = Assert.Single(dados.TopProducts);
        Assert.Equal(100m, linha.Revenue);
        Assert.Equal(1m, linha.Quantity);
        Assert.Equal(100m, dados.Month.Revenue);
    }

    [Fact]
    public async Task Admin_TopProductsIgnoreUnpaidSalesAndPreviousMonths()
    {
        var produto = await SeedProductAsync("Item", custo: 10m);
        await SeedPaidSaleAsync(10m, Agora.AddHours(-1), items: [(produto, 1m, 10m)]);
        await SeedPaidSaleAsync(500m, Agora.AddMonths(-1), items: [(produto, 5m, 500m)]);
        await SeedSaleAsync(SaleStatus.Cancelled, 300m, 0m, completedAt: Agora.AddHours(-1), items: [(produto, 3m, 300m)]);

        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal(10m, Assert.Single(dados.TopProducts).Revenue);
    }

    [Fact]
    public async Task Admin_TopSalespeopleRankByRevenue_AndNameMissingSeller()
    {
        var ana = await SeedUserAsync("Pessoa A");
        var bia = await SeedUserAsync("Pessoa B");
        await SeedPaidSaleAsync(100m, Agora.AddHours(-1), ana);
        await SeedPaidSaleAsync(300m, Agora.AddHours(-1), bia);
        await SeedPaidSaleAsync(50m, Agora.AddHours(-1));

        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal("Pessoa B", dados.TopSalespeople[0].Name);
        Assert.Equal(300m, dados.TopSalespeople[0].Revenue);
        Assert.Equal("Pessoa A", dados.TopSalespeople[1].Name);
        Assert.Equal("Sem vendedor", dados.TopSalespeople[2].Name);
    }

    /// <summary>
    /// Produto sem custo cadastrado daria margem de 100% e inflaria o número
    /// inteiro, então fica fora da conta nos dois lados da divisão.
    /// </summary>
    [Fact]
    public async Task Admin_MarginUsesCurrentCost_AndSkipsItemsWithoutCost()
    {
        var comCusto = await SeedProductAsync("Com custo", custo: 60m);
        var semCusto = await SeedProductAsync("Sem custo", custo: 0m);
        await SeedPaidSaleAsync(1200m, Agora.AddHours(-1),
            items: [(comCusto, 2m, 200m), (semCusto, 1m, 1000m)]);

        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal(200m, dados.Margin.Revenue);
        Assert.Equal(80m, dados.Margin.Profit);
        Assert.Equal(40m, dados.Margin.Percent);
    }

    [Fact]
    public async Task Admin_WithoutSales_MarginPercentIsZero()
    {
        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal(0m, dados.Margin.Percent);
    }

    [Fact]
    public async Task Admin_LowStockRequiresMinimumConfigured()
    {
        var baixo = await SeedProductAsync("Baixo", custo: 1m);
        var zerado = await SeedProductAsync("Zerado", custo: 1m);
        var folgado = await SeedProductAsync("Folgado", custo: 1m);
        var semMinimo = await SeedProductAsync("Sem mínimo", custo: 1m);
        var inativo = await SeedProductAsync("Inativo", custo: 1m, ativo: false);
        await SeedStockAsync(baixo, quantidade: 2m, minimo: 5m);
        await SeedStockAsync(zerado, quantidade: 0m, minimo: 3m);
        await SeedStockAsync(folgado, quantidade: 9m, minimo: 5m);
        await SeedStockAsync(semMinimo, quantidade: 0m, minimo: 0m);
        await SeedStockAsync(inativo, quantidade: 0m, minimo: 5m);

        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal(2, dados.Stock.LowCount);
        Assert.Equal(1, dados.Stock.OutCount);
        Assert.Equal(zerado, dados.Stock.LowItems[0].ProductId);
        Assert.Equal(baixo, dados.Stock.LowItems[1].ProductId);
    }

    [Fact]
    public async Task Admin_DeadStockIsPositiveStockWithoutRecentPaidSale()
    {
        var parado = await SeedProductAsync("Parado", custo: 10m);
        var vendido = await SeedProductAsync("Vendido", custo: 10m);
        var vendidoHaMuito = await SeedProductAsync("Vendido há muito", custo: 5m);
        var cancelado = await SeedProductAsync("Só venda cancelada", custo: 1m);
        var semSaldo = await SeedProductAsync("Sem saldo", custo: 10m);
        await SeedStockAsync(parado, quantidade: 3m, minimo: 0m);
        await SeedStockAsync(vendido, quantidade: 3m, minimo: 0m);
        await SeedStockAsync(vendidoHaMuito, quantidade: 2m, minimo: 0m);
        await SeedStockAsync(cancelado, quantidade: 4m, minimo: 0m);
        await SeedStockAsync(semSaldo, quantidade: 0m, minimo: 0m);
        await SeedPaidSaleAsync(10m, Agora.AddDays(-30), items: [(vendido, 1m, 10m)]);
        await SeedPaidSaleAsync(10m, Agora.AddDays(-200), items: [(vendidoHaMuito, 1m, 10m)]);
        await SeedSaleAsync(SaleStatus.Cancelled, 10m, 0m, completedAt: Agora.AddDays(-5), items: [(cancelado, 1m, 10m)]);

        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal(3, dados.Stock.DeadCount);
        Assert.Equal(44m, dados.Stock.DeadValue); // 3×10 + 2×5 + 4×1
    }

    /// <summary>
    /// Uma venda vendida em outra loja não tira o produto de "parado" aqui: o
    /// estoque parado é o desta filial, e é a venda desta filial que o gira.
    /// </summary>
    [Fact]
    public async Task Admin_DeadStockIgnoresSalesFromOtherBranches()
    {
        var produto = await SeedProductAsync("Só gira lá", custo: 10m);
        await SeedStockAsync(produto, quantidade: 1m, minimo: 0m);
        await SeedPaidSaleAsync(10m, Agora.AddDays(-1), items: [(produto, 1m, 10m)],
            filial: new BranchContext { BranchId = Guid.NewGuid() });

        var dados = (await Service().GetAdminAsync()).Data!;

        Assert.Equal(1, dados.Stock.DeadCount);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private DashboardService Service()
        => new(new DashboardRepository(_fixture.CreateContext(_filial)), new FixedClock(Agora));

    private async Task<Guid> SeedUserAsync(string nome = "Vendedor")
    {
        await using var ctx = _fixture.CreateContext(_filial);
        var user = new User
        {
            Username = $"u{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@teste.local",
            FullName = nome,
            PasswordHash = "x",
            Role = UserRole.Atendente,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> SeedProductAsync(string nome, decimal custo, bool ativo = true)
    {
        await using var ctx = _fixture.CreateContext(_filial);
        var product = new Product
        {
            Sku = Guid.NewGuid().ToString("N")[..12],
            Name = nome,
            CostPrice = custo,
            SalePrice = custo * 2,
            IsActive = ativo,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();
        return product.Id;
    }

    private async Task SeedStockAsync(Guid productId, decimal quantidade, decimal minimo)
    {
        await using var ctx = _fixture.CreateContext(_filial);
        ctx.ProductStocks.Add(new ProductStock
        {
            ProductId = productId,
            Quantity = quantidade,
            Minimum = minimo,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task<Guid> SeedPaymentMethodAsync(string nome)
    {
        await using var ctx = _fixture.CreateContext(_filial);
        var method = new PaymentMethod
        {
            Name = $"{nome} {Guid.NewGuid():N}",
            IsActive = true,
            MaxInstallments = 1,
        };
        ctx.PaymentMethods.Add(method);
        await ctx.SaveChangesAsync();
        return method.Id;
    }

    private async Task SeedPaymentAsync(
        Guid saleId, Guid methodId, decimal valor, DateTimeOffset paidAt,
        PaymentStatus status = PaymentStatus.Completed, DateTimeOffset? refundedAt = null)
    {
        await using var ctx = _fixture.CreateContext(_filial);
        ctx.Payments.Add(new Payment
        {
            SaleId = saleId,
            PaymentMethodId = methodId,
            Amount = valor,
            Status = status,
            PaidAt = paidAt.ToUniversalTime(),
            RefundedAt = refundedAt?.ToUniversalTime(),
        });
        await ctx.SaveChangesAsync();
    }

    private Task<Guid> SeedPaidSaleAsync(
        decimal total, DateTimeOffset completedAt, Guid? vendedor = null,
        (Guid Product, decimal Quantity, decimal LineTotal)[]? items = null, BranchContext? filial = null)
        => SeedSaleAsync(SaleStatus.Paid, total, total, vendedor, completedAt, items: items, filial: filial);

    private async Task<Guid> SeedSaleAsync(
        SaleStatus status, decimal total, decimal pago, Guid? vendedor = null,
        DateTimeOffset? completedAt = null, DateTimeOffset? createdAt = null,
        (Guid Product, decimal Quantity, decimal LineTotal)[]? items = null, BranchContext? filial = null)
    {
        await using var ctx = _fixture.CreateContext(filial ?? _filial);
        var sale = new Sale
        {
            SaleNumber = $"TST{Guid.NewGuid():N}"[..14],
            SalespersonId = vendedor,
            Status = status,
            Subtotal = total,
            TotalAmount = total,
            AmountPaid = pago,
            CompletedAt = completedAt?.ToUniversalTime(),
            CreatedAt = (createdAt ?? completedAt ?? Agora).ToUniversalTime(),
        };
        foreach (var (product, quantity, lineTotal) in items ?? [])
        {
            sale.Items.Add(new SaleItem
            {
                ProductId = product,
                Quantity = quantity,
                UnitPrice = quantity == 0 ? 0 : lineTotal / quantity,
                LineTotal = lineTotal,
            });
        }
        ctx.Sales.Add(sale);
        await ctx.SaveChangesAsync();
        return sale.Id;
    }

    private async Task SeedQuotationAsync(QuotationStatus status, decimal total, Guid vendedor)
    {
        await using var ctx = _fixture.CreateContext(_filial);
        ctx.Quotations.Add(new Quotation
        {
            QuotationNumber = $"ORC{Guid.NewGuid():N}"[..14],
            SalespersonId = vendedor,
            Status = status,
            Subtotal = total,
            TotalAmount = total,
        });
        await ctx.SaveChangesAsync();
    }

    private sealed class FixedClock(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora.ToUniversalTime();
    }
}
