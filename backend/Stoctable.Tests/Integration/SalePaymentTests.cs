using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Stoctable.Application.Services.Sales;
using Stoctable.Communication.Requests.Sales;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;
using Stoctable.Infrastructure;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Cobre o caminho do caixa: POST /api/sales/{id}/payments → ProcessPaymentAsync.
/// O pagamento cria Payments novos dentro de uma Sale já rastreada, que é onde a
/// regressão do EF Core 9 (PK Guid pré-preenchida → Modified em vez de Added)
/// aparecia como DbUpdateConcurrencyException / 500.
/// </summary>
[Trait("Category", "Integration")]
public class SalePaymentTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SalePaymentTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessPayment_FullAmount_InsertsPaymentAndMarksPaid()
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);

        var service = BuildService(_fixture.CreateContext());
        var result = await service.ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 100m)]));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments)
            .FirstAsync(s => s.Id == saleId);

        Assert.Equal(SaleStatus.Paid, sale.Status);
        Assert.Equal(100m, sale.AmountPaid);
        Assert.NotNull(sale.CompletedAt);

        var payment = Assert.Single(sale.Payments);
        Assert.Equal(100m, payment.Amount);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.NotNull(payment.PaidAt);
    }

    [Fact]
    public async Task ProcessPayment_SplitAcrossTwoEntries_InsertsBoth()
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);

        var service = BuildService(_fixture.CreateContext());
        var result = await service.ProcessPaymentAsync(saleId, new ProcessPaymentRequest([
            new PaymentEntryRequest(methodId, 60m),
            new PaymentEntryRequest(methodId, 40m, Installments: 2)
        ]));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments)
            .FirstAsync(s => s.Id == saleId);

        Assert.Equal(2, sale.Payments.Count);
        Assert.Equal(100m, sale.Payments.Sum(p => p.Amount));
        Assert.Equal(SaleStatus.Paid, sale.Status);
    }

    [Fact]
    public async Task ProcessPayment_PartialThenRemainder_KeepsBothPayments()
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);

        var partial = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 30m)]));
        Assert.True(partial.IsSuccess, partial.ErrorMessage);

        await using (var mid = _fixture.CreateContext())
        {
            var sale = await mid.Sales.AsNoTracking().FirstAsync(s => s.Id == saleId);
            Assert.Equal(SaleStatus.PartiallyPaid, sale.Status);
            Assert.Equal(30m, sale.AmountPaid);
        }

        // Segunda chamada: a venda já tem um Payment persistido e ganha outro novo —
        // o UPDATE legítimo da Sale e o INSERT do Payment convivem no mesmo SaveChanges.
        var rest = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 70m)]));
        Assert.True(rest.IsSuccess, rest.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var final = await verify.Sales.AsNoTracking().Include(s => s.Payments)
            .FirstAsync(s => s.Id == saleId);

        Assert.Equal(2, final.Payments.Count);
        Assert.Equal(100m, final.AmountPaid);
        Assert.Equal(SaleStatus.Paid, final.Status);
    }

    [Fact]
    public async Task ProcessPayment_AboveTotal_ReturnsFailure()
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);

        var service = BuildService(_fixture.CreateContext());
        var result = await service.ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 150m)]));

        Assert.False(result.IsSuccess);

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Payments.AsNoTracking().AnyAsync(p => p.SaleId == saleId));
    }

    [Fact]
    public async Task ProcessPayment_OnPaidSale_ReturnsFailure()
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);

        var first = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 100m)]));
        Assert.True(first.IsSuccess, first.ErrorMessage);

        var second = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 100m)]));
        Assert.False(second.IsSuccess);
    }

    [Fact]
    public async Task ProcessPayment_OnCancelledSale_ReturnsFailureAndChangesNothing()
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);
        var cancel = await BuildService(_fixture.CreateContext())
            .CancelAsync(saleId, new CancelSaleRequest("Cliente desistiu"));
        Assert.True(cancel.IsSuccess, cancel.ErrorMessage);

        var result = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 100m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.Sale.AlreadyCancelled, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments)
            .FirstAsync(s => s.Id == saleId);
        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal(0m, sale.AmountPaid);
        Assert.Empty(sale.Payments);
    }

    [Fact]
    public async Task ProcessPayment_AboveRemainingAfterPartial_ReturnsFailureAndChangesNothing()
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);
        var partial = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 60m)]));
        Assert.True(partial.IsSuccess, partial.ErrorMessage);

        // 60 cabe no total da venda, mas não nos 40 que faltam.
        var result = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 60m)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.Sale.PaymentExceedsRemaining, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments)
            .FirstAsync(s => s.Id == saleId);
        Assert.Equal(60m, sale.AmountPaid);
        Assert.Equal(SaleStatus.PartiallyPaid, sale.Status);
        Assert.Single(sale.Payments);
    }

    /// <summary>
    /// A tela calcula o saldo em ponto flutuante: 1.1 - 0.2 vira 0.9000000000000001
    /// no JavaScript. Esse resíduo não pode impedir o caixa de quitar a venda, nem
    /// deixá-la parcialmente paga por uma fração de centavo.
    /// </summary>
    [Theory]
    [InlineData("0.9000000000000001")]
    [InlineData("0.8999999999999999")]
    public async Task ProcessPayment_RemainderWithFloatingPointResidue_SettlesTheSale(string restante)
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 1.10m);
        var partial = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, 0.20m)]));
        Assert.True(partial.IsSuccess, partial.ErrorMessage);

        var result = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([
                new PaymentEntryRequest(methodId, decimal.Parse(restante, CultureInfo.InvariantCulture))
            ]));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().FirstAsync(s => s.Id == saleId);
        Assert.Equal(SaleStatus.Paid, sale.Status);
        Assert.Equal(1.10m, sale.AmountPaid);
    }

    /// <summary>
    /// Sem esta regra, [-50, 150] numa venda de 100 soma exatamente 100, passa pela
    /// checagem de saldo e grava um recebimento negativo no caixa.
    /// </summary>
    [Theory]
    [InlineData("-50", "150")]
    [InlineData("-10", "10")]
    [InlineData("0", "100")]
    [InlineData("0.004", "100")] // vira zero ao arredondar para centavos
    public async Task ProcessPayment_WithNonPositiveEntry_ReturnsFailureAndChangesNothing(string primeiro, string segundo)
    {
        var (saleId, methodId) = await SeedPendingSaleAsync(total: 100m);

        var result = await BuildService(_fixture.CreateContext()).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([
                new PaymentEntryRequest(methodId, decimal.Parse(primeiro, CultureInfo.InvariantCulture)),
                new PaymentEntryRequest(methodId, decimal.Parse(segundo, CultureInfo.InvariantCulture)),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.Sale.PaymentAmountMustBePositive, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments)
            .FirstAsync(s => s.Id == saleId);
        Assert.Equal(0m, sale.AmountPaid);
        Assert.Empty(sale.Payments);
    }

    [Fact]
    public async Task ProcessPayment_WithoutEntries_ReturnsFailure()
    {
        var (saleId, _) = await SeedPendingSaleAsync(total: 100m);

        var result = await BuildService(_fixture.CreateContext())
            .ProcessPaymentAsync(saleId, new ProcessPaymentRequest([]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.Sale.PaymentRequired, result.ErrorMessage);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static SaleService BuildService(StoctableDbContext ctx)
    {
        var seq = new NumberSequenceGenerator(ctx, new BranchContext());
        var productRepo = new ProductRepository(ctx);
        var stockRepo = new ProductStockRepository(ctx, new BranchContext());
        var saleRepo = new SaleRepository(ctx, seq);
        var inventoryRepo = new InventoryRepository(ctx);
        var uow = new UnitOfWork(ctx);
        return new SaleService(saleRepo, productRepo, stockRepo, inventoryRepo, uow);
    }

    private async Task<(Guid saleId, Guid paymentMethodId)> SeedPendingSaleAsync(decimal total)
    {
        await using var ctx = _fixture.CreateContext();

        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}".Substring(0, 20),
            Name = "Test Product",
            SalePrice = total,
            CostPrice = total / 2,
            StockQuantity = 10m,
            StockReserved = 0,
            IsActive = true,
        };
        ctx.Products.Add(product);

        var paymentMethod = await ctx.PaymentMethods.FirstOrDefaultAsync();
        if (paymentMethod is null)
        {
            paymentMethod = new PaymentMethod { Name = "Dinheiro", IsActive = true, MaxInstallments = 1 };
            ctx.PaymentMethods.Add(paymentMethod);
            await ctx.SaveChangesAsync();
        }

        var sale = new Sale
        {
            SaleNumber = $"TST{Guid.NewGuid():N}".Substring(0, 14),
            Status = SaleStatus.PendingPayment,
            Subtotal = total,
            TotalAmount = total,
            AmountPaid = 0m,
            Items =
            {
                new SaleItem
                {
                    ProductId = product.Id,
                    Quantity = 1m,
                    UnitPrice = total,
                    LineTotal = total,
                }
            }
        };
        ctx.Sales.Add(sale);

        await ctx.SaveChangesAsync();
        return (sale.Id, paymentMethod.Id);
    }
}
