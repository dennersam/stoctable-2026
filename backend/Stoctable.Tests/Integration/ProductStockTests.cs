using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Estoque por filial, agora que <c>product_stocks</c> é a fonte da verdade.
///
/// Sucedeu ProductStockMirrorTests, que reconciliava o dual-write com as colunas
/// de <c>products</c>. Aquela reconciliação deixou de fazer sentido quando a
/// autoridade virou — o que sobrevive aqui são os casos de comportamento, que
/// continuam valendo: guarda de saldo, liberação idempotente e isolamento entre
/// lojas.
/// </summary>
[Trait("Category", "Integration")]
public class ProductStockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Decrement_ReducesQuantity_AndReportsTheNewBalance()
    {
        var productId = await SeedProductAsync(stock: 10m);

        await using (var ctx = fixture.CreateContext())
        {
            var result = await NewRepo(ctx).TryDecrementAsync(productId, 3m);
            Assert.True(result.Success);
            // O saldo vem do RETURNING do próprio UPDATE, não de uma releitura.
            Assert.Equal(7m, result.QuantityAfter);
        }

        await AssertStockAsync(productId, quantity: 7m, reserved: 0m);
    }

    [Fact]
    public async Task Increment_AddsQuantity()
    {
        var productId = await SeedProductAsync(stock: 5m);

        await using (var ctx = fixture.CreateContext())
        {
            var result = await NewRepo(ctx).IncrementAsync(productId, 4m);
            Assert.True(result.Success);
            Assert.Equal(9m, result.QuantityAfter);
        }

        await AssertStockAsync(productId, quantity: 9m, reserved: 0m);
    }

    [Fact]
    public async Task ReserveAndRelease_MoveTheReservedColumn()
    {
        var productId = await SeedProductAsync(stock: 10m);

        await using (var ctx = fixture.CreateContext())
            Assert.True((await NewRepo(ctx).TryReserveAsync(productId, 4m)).Success);

        await AssertStockAsync(productId, quantity: 10m, reserved: 4m);

        await using (var ctx = fixture.CreateContext())
            await NewRepo(ctx).ReleaseReservedAsync(productId, 4m);

        await AssertStockAsync(productId, quantity: 10m, reserved: 0m);
    }

    [Fact]
    public async Task Reserve_BeyondAvailable_IsRefused()
    {
        // A guarda é sobre o DISPONÍVEL, não sobre a quantidade física: 10 em
        // estoque com 8 reservados deixa 2 para comprometer, não 10.
        var productId = await SeedProductAsync(stock: 10m);

        await using (var ctx = fixture.CreateContext())
            Assert.True((await NewRepo(ctx).TryReserveAsync(productId, 8m)).Success);

        await using (var ctx = fixture.CreateContext())
            Assert.False((await NewRepo(ctx).TryReserveAsync(productId, 5m)).Success);

        await AssertStockAsync(productId, quantity: 10m, reserved: 8m);
    }

    [Fact]
    public async Task Release_BeyondReserved_NeverGoesNegative()
    {
        var productId = await SeedProductAsync(stock: 10m);

        await using (var ctx = fixture.CreateContext())
            await NewRepo(ctx).TryReserveAsync(productId, 2m);

        // Liberar duas vezes a mesma reserva acontece (cancelamento seguido de
        // expiração) e não pode produzir reserva negativa.
        await using (var ctx = fixture.CreateContext())
            await NewRepo(ctx).ReleaseReservedAsync(productId, 5m);

        await AssertStockAsync(productId, quantity: 10m, reserved: 0m);
    }

    [Fact]
    public async Task Decrement_WithoutEnoughStock_ChangesNothing()
    {
        var productId = await SeedProductAsync(stock: 2m);

        await using (var ctx = fixture.CreateContext())
        {
            var result = await NewRepo(ctx).TryDecrementAsync(productId, 5m);
            Assert.False(result.Success);
            // Mesmo na recusa o saldo atual volta, para a mensagem de erro.
            Assert.Equal(2m, result.QuantityAfter);
        }

        await AssertStockAsync(productId, quantity: 2m, reserved: 0m);
    }

    [Fact]
    public async Task FirstOperation_CreatesRowFromZero()
    {
        // Produto nunca movimentado nesta filial não tem linha. A primeira
        // operação cria uma zerada e aplica o delta sobre ela — nunca adota
        // saldo de lugar nenhum, porque o estoque das outras lojas não é seu.
        var productId = await SeedProductAsync(stock: null);

        await using (var ctx = fixture.CreateContext())
        {
            Assert.False((await NewRepo(ctx).TryDecrementAsync(productId, 1m)).Success);
            Assert.True((await NewRepo(ctx).IncrementAsync(productId, 6m)).Success);
        }

        await AssertStockAsync(productId, quantity: 6m, reserved: 0m);
    }

    [Fact]
    public async Task StockIsScopedByBranch_NotShared()
    {
        var productId = await SeedProductAsync(stock: null);

        var penha = new BranchContext { BranchId = Guid.NewGuid() };
        var villa = new BranchContext { BranchId = Guid.NewGuid() };

        await using (var ctx = fixture.CreateContext(penha))
            await new ProductStockRepository(ctx, penha).IncrementAsync(productId, 3m);

        await using (var ctx = fixture.CreateContext(villa))
            await new ProductStockRepository(ctx, villa).IncrementAsync(productId, 5m);

        // IgnoreQueryFilters porque o objetivo aqui é justamente ver as duas
        // filiais de uma vez; no uso normal o filtro global esconde a outra.
        await using var verify = fixture.CreateContext();
        var rows = await verify.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.ProductId == productId).ToListAsync();

        // Duas linhas distintas: o catálogo é da empresa, o estoque é da loja.
        Assert.Equal(2, rows.Count);
        Assert.Equal(3m, rows.Single(r => r.BranchId == penha.BranchId).Quantity);
        Assert.Equal(5m, rows.Single(r => r.BranchId == villa.BranchId).Quantity);
    }

    [Fact]
    public async Task Minimum_IsPerBranch()
    {
        var productId = await SeedProductAsync(stock: null);

        var mega = new BranchContext { BranchId = Guid.NewGuid() };
        var penha = new BranchContext { BranchId = Guid.NewGuid() };

        await using (var ctx = fixture.CreateContext(mega))
            await new ProductStockRepository(ctx, mega).SetMinimumAsync(productId, 20m);

        await using (var ctx = fixture.CreateContext(penha))
            await new ProductStockRepository(ctx, penha).SetMinimumAsync(productId, 2m);

        // A loja grande repõe a partir de 20, a pequena a partir de 2 — mesmo
        // produto, mínimos diferentes.
        await using var verify = fixture.CreateContext();
        var rows = await verify.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.ProductId == productId).ToListAsync();

        Assert.Equal(20m, rows.Single(r => r.BranchId == mega.BranchId).Minimum);
        Assert.Equal(2m, rows.Single(r => r.BranchId == penha.BranchId).Minimum);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static IProductStockRepository NewRepo(StoctableDbContext ctx)
        => new ProductStockRepository(ctx, new BranchContext());

    private async Task AssertStockAsync(Guid productId, decimal quantity, decimal reserved)
    {
        await using var verify = fixture.CreateContext();

        var stock = await verify.ProductStocks.AsNoTracking()
            .SingleAsync(s => s.ProductId == productId && s.BranchId == BranchContext.LegacySingleBranchId);

        Assert.Equal(quantity, stock.Quantity);
        Assert.Equal(reserved, stock.Reserved);
        Assert.Equal(quantity - reserved, stock.Available);
    }

    /// <summary>
    /// Cria o produto e, quando <paramref name="stock"/> é informado, a linha de
    /// estoque da filial legada. Passar null semeia só o catálogo — é assim que
    /// se testa o produto que ainda não chegou nesta loja.
    /// </summary>
    private async Task<Guid> SeedProductAsync(decimal? stock)
    {
        await using var ctx = fixture.CreateContext();

        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}"[..20],
            Name = "Produto de Teste",
            SalePrice = 10m,
            CostPrice = 5m,
            IsActive = true,
        };
        ctx.Products.Add(product);

        if (stock is not null)
            ctx.ProductStocks.Add(new ProductStock
            {
                BranchId = BranchContext.LegacySingleBranchId,
                ProductId = product.Id,
                Quantity = stock.Value,
            });

        await ctx.SaveChangesAsync();
        return product.Id;
    }
}
