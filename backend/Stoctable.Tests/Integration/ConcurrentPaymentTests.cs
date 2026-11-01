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

/// <summary>
/// Concorrência real na mesma venda. As checagens de saldo e de status passam em
/// qualquer teste sequencial mesmo sem bloqueio: só falham quando duas transações
/// leem o mesmo estado ao mesmo tempo. Como DbContext não é thread-safe, cada task
/// cria o seu.
/// </summary>
[Trait("Category", "Integration")]
public class ConcurrentPaymentTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ParallelPartialPayments_NeverExceedTheTotal()
    {
        var (saleId, methodId, _) = await SeedSaleAsync(total: 100m);

        // Dez tentativas de 30 numa venda de 100: exatamente três cabem.
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PayAsync(saleId, methodId, 30m)));

        Assert.Equal(3, results.Count(ok => ok));

        await using var verify = fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments).FirstAsync(s => s.Id == saleId);
        Assert.Equal(90m, sale.AmountPaid);
        Assert.Equal(3, sale.Payments.Count);
        Assert.Equal(SaleStatus.PartiallyPaid, sale.Status);
    }

    [Fact]
    public async Task ParallelFullPayments_ChargeOnlyOnce()
    {
        var (saleId, methodId, _) = await SeedSaleAsync(total: 100m);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PayAsync(saleId, methodId, 100m)));

        Assert.Equal(1, results.Count(ok => ok));

        await using var verify = fixture.CreateContext();
        var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments).FirstAsync(s => s.Id == saleId);
        Assert.Equal(100m, sale.AmountPaid);
        Assert.Single(sale.Payments);
        Assert.Equal(SaleStatus.Paid, sale.Status);
    }

    [Fact]
    public async Task ParallelCancels_ReturnStockOnlyOnce()
    {
        var (saleId, _, productId) = await SeedSaleAsync(total: 100m, stockAfterSale: 7m, soldQuantity: 3m);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => CancelAsync(saleId)));

        Assert.Equal(1, results.Count(ok => ok));

        await using var verify = fixture.CreateContext();
        var stock = await verify.ProductStocks.AsNoTracking().FirstAsync(s => s.ProductId == productId);
        Assert.Equal(10m, stock.Quantity);
    }

    /// <summary>
    /// Qualquer que seja a ordem, a venda termina cancelada e nenhum pagamento
    /// fica sem estorno. Sem bloqueio, o cancelamento estorna só os pagamentos que
    /// leu, e o que entrou no intervalo fica como recebido numa venda cancelada.
    /// </summary>
    [Fact]
    public async Task PaymentRacingWithCancel_NeverLeavesAnUnrefundedPayment()
    {
        for (var round = 0; round < 25; round++)
        {
            var (saleId, methodId, _) = await SeedSaleAsync(total: 100m);

            await Task.WhenAll(PayAsync(saleId, methodId, 40m), CancelAsync(saleId));

            await using var verify = fixture.CreateContext();
            var sale = await verify.Sales.AsNoTracking().Include(s => s.Payments).FirstAsync(s => s.Id == saleId);
            Assert.Equal(SaleStatus.Cancelled, sale.Status);
            Assert.All(sale.Payments, p => Assert.Equal(PaymentStatus.Refunded, p.Status));
        }
    }

    private async Task<bool> PayAsync(Guid saleId, Guid methodId, decimal amount)
    {
        await using var ctx = fixture.CreateContext();
        var result = await BuildService(ctx).ProcessPaymentAsync(
            saleId, new ProcessPaymentRequest([new PaymentEntryRequest(methodId, amount)]));
        return result.IsSuccess;
    }

    private async Task<bool> CancelAsync(Guid saleId)
    {
        await using var ctx = fixture.CreateContext();
        var result = await BuildService(ctx).CancelAsync(saleId, new CancelSaleRequest("Teste de concorrência"));
        return result.IsSuccess;
    }

    private static SaleService BuildService(StoctableDbContext ctx)
    {
        var seq = new NumberSequenceGenerator(ctx, new BranchContext());
        return new SaleService(
            new SaleRepository(ctx, seq),
            new ProductRepository(ctx),
            new ProductStockRepository(ctx, new BranchContext()),
            new InventoryRepository(ctx),
            new UnitOfWork(ctx));
    }

    private async Task<(Guid SaleId, Guid MethodId, Guid ProductId)> SeedSaleAsync(
        decimal total, decimal stockAfterSale = 10m, decimal soldQuantity = 1m)
    {
        await using var ctx = fixture.CreateContext();

        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}"[..20],
            Name = "Produto",
            SalePrice = total,
            CostPrice = total / 2,
            IsActive = true,
        };
        ctx.Products.Add(product);
        ctx.ProductStocks.Add(new ProductStock { ProductId = product.Id, Quantity = stockAfterSale });

        var method = new PaymentMethod { Name = $"Forma {Guid.NewGuid():N}", IsActive = true, MaxInstallments = 1 };
        ctx.PaymentMethods.Add(method);

        var sale = new Sale
        {
            SaleNumber = $"TST{Guid.NewGuid():N}"[..14],
            Status = SaleStatus.PendingPayment,
            Subtotal = total,
            TotalAmount = total,
            Items =
            {
                new SaleItem
                {
                    ProductId = product.Id,
                    Quantity = soldQuantity,
                    UnitPrice = total,
                    LineTotal = total,
                },
            },
        };
        ctx.Sales.Add(sale);

        await ctx.SaveChangesAsync();
        return (sale.Id, method.Id, product.Id);
    }
}
