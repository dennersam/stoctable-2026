using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Concorrência real: tasks paralelas disputando a mesma linha de estoque.
///
/// A suíte nunca teve isso. A garantia contra oversell é a guarda
/// <c>WHERE quantity &gt;= @qty</c> do UPDATE, e uma guarda só é exercitada de
/// verdade quando duas transações chegam juntas — testada em sequência ela passa
/// mesmo estando errada. Como DbContext não é thread-safe, cada task cria o seu.
/// </summary>
[Trait("Category", "Integration")]
public class ConcurrentStockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ParallelDecrements_NeverOversell()
    {
        var productId = await SeedProductAsync(stock: 10m);

        // 20 tentativas de tirar 1 de um saldo de 10: exatamente 10 podem passar.
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var ctx = fixture.CreateContext();
            return (await new ProductStockRepository(ctx, new BranchContext())
                .TryDecrementAsync(productId, 1m)).Success;
        }));

        Assert.Equal(10, results.Count(ok => ok));
        Assert.Equal(10, results.Count(ok => !ok));
        await AssertQuantityAsync(productId, 0m);
    }

    [Fact]
    public async Task ParallelReserves_NeverOverReserve()
    {
        var productId = await SeedProductAsync(stock: 6m);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var ctx = fixture.CreateContext();
            return (await new ProductStockRepository(ctx, new BranchContext())
                .TryReserveAsync(productId, 2m)).Success;
        }));

        // Disponível é 6, cada reserva pega 2 → no máximo 3 passam.
        Assert.Equal(3, results.Count(ok => ok));

        await using var verify = fixture.CreateContext();
        var stock = await verify.ProductStocks.AsNoTracking().FirstAsync(s => s.ProductId == productId);
        Assert.Equal(6m, stock.Reserved);
        Assert.Equal(0m, stock.Available);
    }

    [Fact]
    public async Task ParallelOperationsOnDifferentBranches_DoNotSerialize()
    {
        // Mesma peça, lojas diferentes: as duas baixas devem passar, porque são
        // linhas distintas. Se alguma vez este teste falhar por saldo, é sinal de
        // que uma escrita voltou a mirar o agregado da empresa.
        var productId = await SeedProductAsync(stock: null);

        var mega = new BranchContext { BranchId = Guid.NewGuid() };
        var penha = new BranchContext { BranchId = Guid.NewGuid() };

        foreach (var branch in new[] { mega, penha })
        {
            await using var ctx = fixture.CreateContext(branch);
            await new ProductStockRepository(ctx, branch).IncrementAsync(productId, 5m);
        }

        var results = await Task.WhenAll(new[] { mega, penha }.Select(async branch =>
        {
            await using var ctx = fixture.CreateContext(branch);
            return (await new ProductStockRepository(ctx, branch)
                .TryDecrementAsync(productId, 5m)).Success;
        }));

        Assert.All(results, Assert.True);

        await using var verify = fixture.CreateContext();
        var rows = await verify.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.ProductId == productId).ToListAsync();
        Assert.All(rows, r => Assert.Equal(0m, r.Quantity));
    }

    [Fact]
    public async Task ParallelFirstOperations_CreateExactlyOneRow()
    {
        // Todas as tasks encontram o produto sem linha nesta filial e correm para
        // criá-la. O ON CONFLICT DO NOTHING tem que deixar uma só — e as somas
        // não podem se perder no caminho.
        var productId = await SeedProductAsync(stock: null);

        await Task.WhenAll(Enumerable.Range(0, 15).Select(async _ =>
        {
            await using var ctx = fixture.CreateContext();
            await new ProductStockRepository(ctx, new BranchContext())
                .IncrementAsync(productId, 2m);
        }));

        await using var verify = fixture.CreateContext();
        var rows = await verify.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.ProductId == productId).ToListAsync();

        Assert.Single(rows);
        Assert.Equal(30m, rows[0].Quantity);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task AssertQuantityAsync(Guid productId, decimal expected)
    {
        await using var verify = fixture.CreateContext();
        var stock = await verify.ProductStocks.AsNoTracking().FirstAsync(s => s.ProductId == productId);
        Assert.Equal(expected, stock.Quantity);
    }

    private async Task<Guid> SeedProductAsync(decimal? stock)
    {
        await using var ctx = fixture.CreateContext();

        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}"[..20],
            Name = "Produto Concorrente",
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
