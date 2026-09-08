using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Fase 2 do plano de SaaS: o estoque saiu da linha do produto para
/// <c>product_stocks</c>, que é por filial.
///
/// Enquanto a transição dura, as duas fontes são escritas em paralelo e
/// <c>products</c> continua autoritativa. Estes testes são a reconciliação
/// automatizada: se algum caminho de escrita de estoque esquecer de espelhar,
/// as duas divergem e o teste quebra — que é exatamente o defeito que a
/// reconciliação manual em produção procuraria durante uma semana.
/// </summary>
[Trait("Category", "Integration")]
public class ProductStockMirrorTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public ProductStockMirrorTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Decrement_MirrorsIntoProductStocks()
    {
        var productId = await SeedProductAsync(stock: 10m);

        await using (var ctx = _fixture.CreateContext())
        {
            var ok = await NewRepo(ctx).TryDecrementStockAsync(productId, 3m);
            Assert.True(ok);
        }

        await AssertInSyncAsync(productId, expectedQuantity: 7m, expectedReserved: 0m);
    }

    [Fact]
    public async Task Increment_MirrorsIntoProductStocks()
    {
        var productId = await SeedProductAsync(stock: 5m);

        await using (var ctx = _fixture.CreateContext())
        {
            Assert.True(await NewRepo(ctx).IncrementStockAsync(productId, 4m));
        }

        await AssertInSyncAsync(productId, expectedQuantity: 9m, expectedReserved: 0m);
    }

    [Fact]
    public async Task ReserveAndRelease_MirrorReservedColumn()
    {
        var productId = await SeedProductAsync(stock: 10m);

        await using (var ctx = _fixture.CreateContext())
            await NewRepo(ctx).ReserveStockAsync(productId, 4m);

        await AssertInSyncAsync(productId, expectedQuantity: 10m, expectedReserved: 4m);

        await using (var ctx = _fixture.CreateContext())
            await NewRepo(ctx).ReleaseReservedStockAsync(productId, 4m);

        await AssertInSyncAsync(productId, expectedQuantity: 10m, expectedReserved: 0m);
    }

    [Fact]
    public async Task Release_BeyondReserved_NeverGoesNegative()
    {
        var productId = await SeedProductAsync(stock: 10m);

        await using (var ctx = _fixture.CreateContext())
            await NewRepo(ctx).ReserveStockAsync(productId, 2m);

        // Liberar duas vezes a mesma reserva acontece (cancelamento seguido de
        // expiração) e não pode produzir reserva negativa em nenhuma das fontes.
        await using (var ctx = _fixture.CreateContext())
            await NewRepo(ctx).ReleaseReservedStockAsync(productId, 5m);

        await AssertInSyncAsync(productId, expectedQuantity: 10m, expectedReserved: 0m);
    }

    [Fact]
    public async Task FirstMirror_AdoptsExistingStock_InsteadOfOnlyTheDelta()
    {
        // O produto nasce com estoque semeado direto pelo EF, sem passar pelo
        // repositório — como acontece no cadastro de produto e no backfill do
        // SIC. A primeira operação de estoque precisa adotar o saldo existente,
        // não começar do delta, ou a linha nasceria divergente.
        var productId = await SeedProductAsync(stock: 100m);

        await using (var ctx = _fixture.CreateContext())
            Assert.True(await NewRepo(ctx).TryDecrementStockAsync(productId, 1m));

        await AssertInSyncAsync(productId, expectedQuantity: 99m, expectedReserved: 0m);
    }

    [Fact]
    public async Task Decrement_WithoutEnoughStock_TouchesNeitherSource()
    {
        var productId = await SeedProductAsync(stock: 2m);

        await using (var ctx = _fixture.CreateContext())
            Assert.False(await NewRepo(ctx).TryDecrementStockAsync(productId, 5m));

        await using var verify = _fixture.CreateContext();
        var product = await verify.Products.AsNoTracking().FirstAsync(p => p.Id == productId);
        Assert.Equal(2m, product.StockQuantity);

        // A guarda impediu o UPDATE, então nem sequer chegou a existir linha espelho.
        Assert.False(await verify.ProductStocks.AsNoTracking().AnyAsync(s => s.ProductId == productId));
    }

    [Fact]
    public async Task StockIsScopedByBranch_NotShared()
    {
        var productId = await SeedProductAsync(stock: 10m);

        var penha = new BranchContext { BranchId = Guid.NewGuid() };
        var villa = new BranchContext { BranchId = Guid.NewGuid() };

        await using (var ctx = _fixture.CreateContext())
            await new ProductRepository(ctx, penha).ReserveStockAsync(productId, 3m);

        await using (var ctx = _fixture.CreateContext())
            await new ProductRepository(ctx, villa).ReserveStockAsync(productId, 5m);

        // IgnoreQueryFilters porque o objetivo aqui é justamente ver as duas
        // filiais de uma vez; no uso normal o filtro global esconde a outra —
        // é o que BranchIsolationTests verifica.
        await using var verify = _fixture.CreateContext();
        var rows = await verify.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.ProductId == productId).ToListAsync();

        // Duas linhas distintas: o catálogo é da empresa, o estoque é da loja.
        Assert.Equal(2, rows.Count);
        Assert.Equal(3m, rows.Single(r => r.BranchId == penha.BranchId).Reserved);
        Assert.Equal(5m, rows.Single(r => r.BranchId == villa.BranchId).Reserved);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static ProductRepository NewRepo(StoctableDbContext ctx)
        => new(ctx, new BranchContext());

    /// <summary>Confere as duas fontes de uma vez — é isso que "reconciliar" significa aqui.</summary>
    private async Task AssertInSyncAsync(Guid productId, decimal expectedQuantity, decimal expectedReserved)
    {
        await using var verify = _fixture.CreateContext();

        var product = await verify.Products.AsNoTracking().FirstAsync(p => p.Id == productId);
        Assert.Equal(expectedQuantity, product.StockQuantity);
        Assert.Equal(expectedReserved, product.StockReserved);

        var stock = await verify.ProductStocks.AsNoTracking()
            .SingleAsync(s => s.ProductId == productId && s.BranchId == BranchContext.LegacySingleBranchId);
        Assert.Equal(expectedQuantity, stock.Quantity);
        Assert.Equal(expectedReserved, stock.Reserved);
        Assert.Equal(expectedQuantity - expectedReserved, stock.Available);
    }

    private async Task<Guid> SeedProductAsync(decimal stock)
    {
        await using var ctx = _fixture.CreateContext();

        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}".Substring(0, 20),
            Name = "Produto de Teste",
            SalePrice = 10m,
            CostPrice = 5m,
            StockQuantity = stock,
            StockReserved = 0m,
            IsActive = true,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        return product.Id;
    }
}
