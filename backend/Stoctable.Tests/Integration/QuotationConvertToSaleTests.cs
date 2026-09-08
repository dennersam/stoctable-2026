using Microsoft.EntityFrameworkCore;
using Stoctable.Application.Services.Quotations;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

[Trait("Category", "Integration")]
public class QuotationConvertToSaleTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public QuotationConvertToSaleTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConvertToSale_HappyPath_CreatesSaleAndDecrementsStock()
    {
        var (cashierId, productId, quotationId) = await SeedFinalizedQuotationAsync(
            initialStock: 10, reservedQty: 3, quantityInQuotation: 3);

        var service = BuildService(_fixture.CreateContext());
        var result = await service.ConvertToSaleAsync(quotationId, cashierId);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotEqual(Guid.Empty, result.Data);

        await using var verify = _fixture.CreateContext();
        var product = await verify.Products.AsNoTracking().FirstAsync(p => p.Id == productId);
        Assert.Equal(7m, product.StockQuantity);
        Assert.Equal(0m, product.StockReserved);

        var sale = await verify.Sales.AsNoTracking().Include(s => s.Items)
            .FirstAsync(s => s.Id == result.Data);
        Assert.Equal(SaleStatus.PendingPayment, sale.Status);
        Assert.Single(sale.Items);
        Assert.Equal(3m, sale.Items.First().Quantity);

        var movement = await verify.InventoryMovements.AsNoTracking()
            .Where(m => m.ProductId == productId && m.ReferenceId == sale.Id)
            .FirstAsync();
        Assert.Equal(MovementType.Sale, movement.MovementType);
        Assert.Equal(-3m, movement.Quantity);
        Assert.Equal(10m, movement.QuantityBefore);
        Assert.Equal(7m, movement.QuantityAfter);

        var quotation = await verify.Quotations.AsNoTracking().FirstAsync(q => q.Id == quotationId);
        Assert.Equal(QuotationStatus.Converted, quotation.Status);
        Assert.Equal(sale.Id, quotation.ConvertedToSaleId);

        var reservation = await verify.StockReservations.AsNoTracking()
            .FirstAsync(r => r.QuotationId == quotationId);
        Assert.False(reservation.IsActive);
        Assert.NotNull(reservation.ReleasedAt);
    }

    [Fact]
    public async Task ConvertToSale_StockBelowQuotationQuantity_RollsBackAndReturnsConflict()
    {
        // Estoque cai entre Finalize e Convert (ex: outra venda concorrente)
        var (cashierId, productId, quotationId) = await SeedFinalizedQuotationAsync(
            initialStock: 5, reservedQty: 5, quantityInQuotation: 5);

        // Simula que outra operação consumiu o estoque antes
        await using (var setup = _fixture.CreateContext())
        {
            await setup.Database.ExecuteSqlRawAsync(
                "UPDATE products SET stock_quantity = 2 WHERE id = {0}", productId);
        }

        var service = BuildService(_fixture.CreateContext());
        var result = await service.ConvertToSaleAsync(quotationId, cashierId);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);

        await using var verify = _fixture.CreateContext();

        // Estoque continua intocado (rollback funcionou)
        var product = await verify.Products.AsNoTracking().FirstAsync(p => p.Id == productId);
        Assert.Equal(2m, product.StockQuantity);

        // Quotation permanece Finalized
        var quotation = await verify.Quotations.AsNoTracking().FirstAsync(q => q.Id == quotationId);
        Assert.Equal(QuotationStatus.Finalized, quotation.Status);
        Assert.Null(quotation.ConvertedToSaleId);

        // Nenhuma Sale persistida para esse orçamento
        var saleExists = await verify.Sales.AsNoTracking().AnyAsync(s => s.QuotationId == quotationId);
        Assert.False(saleExists);

        // Reservation continua ativa
        var reservation = await verify.StockReservations.AsNoTracking()
            .FirstAsync(r => r.QuotationId == quotationId);
        Assert.True(reservation.IsActive);
    }

    [Fact]
    public async Task ConvertToSale_TwoConcurrentConversions_OneSucceedsOneFails_NoOversell()
    {
        // Dois orçamentos de 10 unidades cada do mesmo produto que tem só 15 em estoque.
        var cashierId = await GetSeededCashierIdAsync();
        var productId = await SeedProductAsync(initialStock: 15);
        var quotationAId = await SeedFinalizedQuotationOnlyAsync(productId, cashierId, quantity: 10);
        var quotationBId = await SeedFinalizedQuotationOnlyAsync(productId, cashierId, quantity: 10);

        // Cada chamada usa seu próprio DbContext para refletir cenário real (caixas distintos).
        var taskA = Task.Run(async () =>
        {
            var svc = BuildService(_fixture.CreateContext());
            return await svc.ConvertToSaleAsync(quotationAId, cashierId);
        });
        var taskB = Task.Run(async () =>
        {
            var svc = BuildService(_fixture.CreateContext());
            return await svc.ConvertToSaleAsync(quotationBId, cashierId);
        });

        await Task.WhenAll(taskA, taskB);

        var resultA = await taskA;
        var resultB = await taskB;

        // Exatamente um deve ter sucesso, o outro deve ter 409.
        Assert.True(resultA.IsSuccess ^ resultB.IsSuccess,
            $"Esperava 1 sucesso e 1 falha; A={resultA.IsSuccess}, B={resultB.IsSuccess}");

        var failure = resultA.IsSuccess ? resultB : resultA;
        Assert.Equal(409, failure.StatusCode);

        // Estoque nunca pode ficar negativo.
        await using var verify = _fixture.CreateContext();
        var product = await verify.Products.AsNoTracking().FirstAsync(p => p.Id == productId);
        Assert.Equal(5m, product.StockQuantity);
        Assert.True(product.StockQuantity >= 0, "estoque ficou negativo — oversell detectado");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static QuotationService BuildService(StoctableDbContext ctx)
    {
        var seq = new NumberSequenceGenerator(ctx, new BranchContext());
        var productRepo = new ProductRepository(ctx, new BranchContext());
        var quotationRepo = new QuotationRepository(ctx, seq);
        var saleRepo = new SaleRepository(ctx, seq);
        var inventoryRepo = new InventoryRepository(ctx);
        var uow = new UnitOfWork(ctx);
        return new QuotationService(quotationRepo, productRepo, inventoryRepo, saleRepo, uow);
    }

    private async Task<Guid> GetSeededCashierIdAsync()
    {
        await using var ctx = _fixture.CreateContext();
        var existing = await ctx.Users.FirstOrDefaultAsync(u => u.Username == "test_cashier");
        if (existing is not null) return existing.Id;

        var user = new User
        {
            Username = "test_cashier",
            Email = "cashier@test.local",
            FullName = "Test Cashier",
            PasswordHash = "x",
            Role = UserRole.Caixa,
            IsActive = true,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> SeedProductAsync(decimal initialStock)
    {
        await using var ctx = _fixture.CreateContext();
        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}".Substring(0, 20),
            Name = "Test Product",
            SalePrice = 10m,
            CostPrice = 5m,
            StockQuantity = initialStock,
            StockReserved = 0,
            IsActive = true,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();
        return product.Id;
    }

    private async Task<Guid> SeedFinalizedQuotationOnlyAsync(Guid productId, Guid salespersonId, decimal quantity)
    {
        await using var ctx = _fixture.CreateContext();
        var quotation = new Quotation
        {
            QuotationNumber = $"TST{Guid.NewGuid():N}".Substring(0, 14),
            SalespersonId = salespersonId,
            Status = QuotationStatus.Finalized,
            FinalizedAt = DateTimeOffset.UtcNow,
            Subtotal = quantity * 10m,
            TotalAmount = quantity * 10m,
            Items =
            {
                new QuotationItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = 10m,
                    LineTotal = quantity * 10m,
                }
            }
        };
        ctx.Quotations.Add(quotation);

        // Reserva o estoque (como FinalizeAsync faria)
        await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE products SET stock_reserved = stock_reserved + {0} WHERE id = {1}",
            quantity, productId);

        ctx.StockReservations.Add(new StockReservation
        {
            ProductId = productId,
            QuotationId = quotation.Id,
            Quantity = quantity,
            IsActive = true,
        });

        await ctx.SaveChangesAsync();
        return quotation.Id;
    }

    private async Task<(Guid cashierId, Guid productId, Guid quotationId)> SeedFinalizedQuotationAsync(
        decimal initialStock, decimal reservedQty, decimal quantityInQuotation)
    {
        var cashierId = await GetSeededCashierIdAsync();
        var productId = await SeedProductAsync(initialStock);

        // Aplica reserva separadamente para refletir o estado pós-FinalizeAsync
        await using (var ctx = _fixture.CreateContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE products SET stock_reserved = {0} WHERE id = {1}",
                reservedQty, productId);
        }

        var quotationId = await SeedFinalizedQuotationOnlyAsync(productId, cashierId, quantityInQuotation);
        // SeedFinalizedQuotationOnlyAsync adiciona mais reserved — ajusto pra match
        await using (var ctx = _fixture.CreateContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE products SET stock_reserved = {0} WHERE id = {1}",
                reservedQty, productId);
        }

        return (cashierId, productId, quotationId);
    }
}
