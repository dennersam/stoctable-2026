using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stoctable.Domain.Entities;
using Stoctable.Migration;

namespace Stoctable.Tests.Integration;

/// <summary>
/// O SQL do corte de estoque, exercitado contra um Postgres de verdade.
///
/// Este é o passo irreversível da transição: depois que as colunas de
/// <c>products</c> caírem, não existe mais com o que comparar. E era justamente
/// o trecho sem cobertura nenhuma — o fixture da suíte usa EnsureCreated, então
/// nada aqui nunca exercitou migration nem backfill.
///
/// Os testes montam o cenário no FORMATO ANTIGO (saldo na linha do produto, sem
/// linha em product_stocks) e conferem o que o comando faz com ele.
/// </summary>
[Trait("Category", "Integration")]
public class StockCutoverTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly Guid _mega = Guid.NewGuid();

    [Fact]
    public async Task Backfill_CreatesMissingRows_FromLegacyColumns()
    {
        // Produto no formato antigo: saldo na linha do produto e nenhuma linha
        // de estoque. É o estado de todo item nunca movimentado desde a fase 2.
        var (productId, sku) = await SeedLegacyProductAsync(quantity: 42m, reserved: 5m, minimum: 7m);

        Assert.Contains(sku, await FindDivergencesAsync());

        await ApplyBackfillAsync();

        var stock = await GetStockAsync(productId);
        Assert.NotNull(stock);
        Assert.Equal(42m, stock!.Quantity);
        // A reserva vem junto: perdê-la liberaria para venda peça já
        // comprometida por um orçamento aberto.
        Assert.Equal(5m, stock.Reserved);
        Assert.Equal(7m, stock.Minimum);

        Assert.DoesNotContain(sku, await FindDivergencesAsync());
    }

    [Fact]
    public async Task Backfill_CorrectsRowsThatDrifted()
    {
        // Divergência real que existia em produção: o ajuste manual mexia na
        // coluna antiga sem espelhar, então a linha ficava para trás.
        var (productId, sku) = await SeedLegacyProductAsync(quantity: 30m, reserved: 0m, minimum: 0m);
        await InsertStockRowAsync(productId, quantity: 12m);

        await ApplyBackfillAsync();

        var stock = await GetStockAsync(productId);
        Assert.Equal(30m, stock!.Quantity);
        Assert.DoesNotContain(sku, await FindDivergencesAsync());
    }

    [Fact]
    public async Task Backfill_KeepsMinimumAlreadySetPerBranch()
    {
        // O mínimo virou configuração de loja. Se alguém já ajustou o desta
        // filial, o retrato do catálogo não pode desfazer isso.
        var (productId, _) = await SeedLegacyProductAsync(quantity: 10m, reserved: 0m, minimum: 3m);
        await InsertStockRowAsync(productId, quantity: 10m, minimum: 25m);

        await ApplyBackfillAsync();

        var stock = await GetStockAsync(productId);
        Assert.Equal(25m, stock!.Minimum);
    }

    [Fact]
    public async Task Backfill_IsIdempotent()
    {
        var (productId, _) = await SeedLegacyProductAsync(quantity: 15m, reserved: 2m, minimum: 1m);

        await ApplyBackfillAsync();
        await ApplyBackfillAsync();
        await ApplyBackfillAsync();

        // Rodar de novo não duplica linha nem soma saldo em cima do que já
        // estava lá — o ON CONFLICT substitui, não acumula.
        await using var ctx = fixture.CreateContext();
        var rows = await ctx.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.ProductId == productId && s.BranchId == _mega).ToListAsync();

        Assert.Single(rows);
        Assert.Equal(15m, rows[0].Quantity);
    }

    [Fact]
    public async Task Backfill_DoesNotTouchOtherBranches()
    {
        var (productId, _) = await SeedLegacyProductAsync(quantity: 100m, reserved: 0m, minimum: 0m);

        var penha = Guid.NewGuid();
        await InsertStockRowAsync(productId, quantity: 8m, branchId: penha);

        await ApplyBackfillAsync();

        // O saldo antigo é o agregado da empresa e vai inteiro para a MEGA; a
        // outra loja fica exatamente como estava. Se este teste falhar, o
        // backfill está espalhando estoque para quem não tem.
        await using var ctx = fixture.CreateContext();
        var outra = await ctx.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(s => s.ProductId == productId && s.BranchId == penha);

        Assert.Equal(8m, outra.Quantity);
        Assert.Equal(100m, (await GetStockAsync(productId))!.Quantity);
    }

    [Fact]
    public async Task ZeroOnBothSides_IsNotADivergence()
    {
        // Produto zerado e sem linha é o estado normal de item que nunca entrou
        // nesta loja — não pode aparecer como pendência no relatório do corte.
        var (_, sku) = await SeedLegacyProductAsync(quantity: 0m, reserved: 0m, minimum: 0m);

        // Escopado ao SKU deste teste: a consulta do corte é global por desenho,
        // e o container é compartilhado com os outros fatos da classe.
        Assert.DoesNotContain(sku, await FindDivergencesAsync());
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task ApplyBackfillAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(StockCutover.BackfillSql, conn);
        cmd.Parameters.AddWithValue("owner", _mega);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> FindDivergencesAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(StockCutover.DivergenceSql, conn);
        cmd.Parameters.AddWithValue("owner", _mega);

        var skus = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) skus.Add(reader.GetString(0));
        return skus;
    }

    private async Task<ProductStock?> GetStockAsync(Guid productId)
    {
        await using var ctx = fixture.CreateContext();
        return await ctx.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == _mega);
    }

    /// <summary>
    /// Grava direto por SQL porque as colunas antigas são as que interessam
    /// aqui, e o mapeamento do EF já as trata como mortas.
    /// </summary>
    private async Task<(Guid Id, string Sku)> SeedLegacyProductAsync(decimal quantity, decimal reserved, decimal minimum)
    {
        var product = new Product
        {
            Sku = $"CUT{Guid.NewGuid():N}"[..18],
            Name = "Produto do corte",
            SalePrice = 20m,
            CostPrice = 10m,
            IsActive = true,
        };

        await using (var ctx = fixture.CreateContext())
        {
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
        }

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE products
               SET stock_quantity = @q, stock_reserved = @r, stock_minimum = @m
             WHERE id = @id
            """, conn);
        cmd.Parameters.AddWithValue("q", quantity);
        cmd.Parameters.AddWithValue("r", reserved);
        cmd.Parameters.AddWithValue("m", minimum);
        cmd.Parameters.AddWithValue("id", product.Id);
        await cmd.ExecuteNonQueryAsync();

        return (product.Id, product.Sku);
    }

    private async Task InsertStockRowAsync(
        Guid productId, decimal quantity, decimal minimum = 0m, Guid? branchId = null)
    {
        await using var ctx = fixture.CreateContext();
        ctx.ProductStocks.Add(new ProductStock
        {
            BranchId = branchId ?? _mega,
            ProductId = productId,
            Quantity = quantity,
            Minimum = minimum,
        });
        await ctx.SaveChangesAsync();
    }
}
