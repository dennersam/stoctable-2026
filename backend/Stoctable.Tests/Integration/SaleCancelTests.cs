using Microsoft.EntityFrameworkCore;
using Stoctable.Application.Services.Sales;
using Stoctable.Communication.Requests.Sales;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

[Trait("Category", "Integration")]
public class SaleCancelTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SaleCancelTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Cancel_PendingPaymentSale_RestoresStockAndCancels()
    {
        var (productId, saleId) = await SeedSaleAsync(initialStock: 10, soldQty: 3, paid: false);

        var service = BuildService(_fixture.CreateContext());
        var result = await service.CancelAsync(saleId, new CancelSaleRequest("Cliente desistiu"));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var stock = await verify.ProductStocks.AsNoTracking().FirstAsync(s => s.ProductId == productId);
        Assert.Equal(10m, stock.Quantity); // 7 + 3 devolvido

        var sale = await verify.Sales.AsNoTracking().FirstAsync(s => s.Id == saleId);
        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.NotNull(sale.CancelledAt);
        Assert.Equal("Cliente desistiu", sale.CancellationReason);

        var movement = await verify.InventoryMovements.AsNoTracking()
            .FirstAsync(m => m.ReferenceId == saleId && m.ReferenceType == "sale_cancellation");
        Assert.Equal(MovementType.AdjustmentIn, movement.MovementType);
        Assert.Equal(3m, movement.Quantity);
        Assert.Equal(7m, movement.QuantityBefore);
        Assert.Equal(10m, movement.QuantityAfter);
    }

    [Fact]
    public async Task Cancel_PaidSale_RefundsPaymentsAndRestoresStock()
    {
        var (productId, saleId) = await SeedSaleAsync(initialStock: 5, soldQty: 2, paid: true);

        var service = BuildService(_fixture.CreateContext());
        var result = await service.CancelAsync(saleId, new CancelSaleRequest("Erro no caixa"));

        Assert.True(result.IsSuccess, result.ErrorMessage);

        await using var verify = _fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments)
            .FirstAsync(s => s.Id == saleId);
        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.All(sale.Payments, p =>
        {
            Assert.Equal(PaymentStatus.Refunded, p.Status);
            Assert.NotNull(p.RefundedAt);
        });

        var stock = await verify.ProductStocks.AsNoTracking().FirstAsync(s => s.ProductId == productId);
        Assert.Equal(5m, stock.Quantity); // 3 + 2 devolvido
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledSale_ReturnsFailure()
    {
        var (_, saleId) = await SeedSaleAsync(initialStock: 5, soldQty: 1, paid: false);

        var service = BuildService(_fixture.CreateContext());
        var first = await service.CancelAsync(saleId, new CancelSaleRequest("Primeira tentativa"));
        Assert.True(first.IsSuccess);

        var second = await service.CancelAsync(saleId, new CancelSaleRequest("Segunda tentativa"));
        Assert.False(second.IsSuccess);
    }

    [Fact]
    public async Task Cancel_WithoutReason_ReturnsFailure()
    {
        var (_, saleId) = await SeedSaleAsync(initialStock: 5, soldQty: 1, paid: false);

        var service = BuildService(_fixture.CreateContext());
        var result = await service.CancelAsync(saleId, new CancelSaleRequest(""));

        Assert.False(result.IsSuccess);
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

    private async Task<(Guid productId, Guid saleId)> SeedSaleAsync(
        decimal initialStock, decimal soldQty, bool paid)
    {
        await using var ctx = _fixture.CreateContext();

        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}".Substring(0, 20),
            Name = "Test Product",
            SalePrice = 10m,
            CostPrice = 5m,
            IsActive = true,
        };
        ctx.Products.Add(product);

        // Saldo da filial ja com a venda baixada.
        ctx.ProductStocks.Add(new ProductStock
        {
            BranchId = BranchContext.LegacySingleBranchId,
            ProductId = product.Id,
            Quantity = initialStock - soldQty,
        });

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
            Status = paid ? SaleStatus.Paid : SaleStatus.PendingPayment,
            Subtotal = soldQty * 10m,
            TotalAmount = soldQty * 10m,
            AmountPaid = paid ? soldQty * 10m : 0,
            CompletedAt = paid ? DateTimeOffset.UtcNow : null,
            Items =
            {
                new SaleItem
                {
                    ProductId = product.Id,
                    Quantity = soldQty,
                    UnitPrice = 10m,
                    LineTotal = soldQty * 10m,
                }
            },
            Payments = paid ? new List<Payment>
            {
                new Payment
                {
                    PaymentMethodId = paymentMethod.Id,
                    Amount = soldQty * 10m,
                    Status = PaymentStatus.Completed,
                    PaidAt = DateTimeOffset.UtcNow,
                }
            } : new List<Payment>()
        };
        ctx.Sales.Add(sale);

        await ctx.SaveChangesAsync();
        return (product.Id, sale.Id);
    }
}
