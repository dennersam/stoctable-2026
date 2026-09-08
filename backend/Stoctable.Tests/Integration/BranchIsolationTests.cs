using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// O teste que justifica a fase inteira.
///
/// Os filtros globais do EF são compilados uma vez no modelo, e o modelo é
/// cacheado por tipo de DbContext. Se o filtro capturar o valor da filial em vez
/// de ler o campo de instância do contexto, a primeira filial do processo fica
/// congelada no modelo e TODAS as requisições seguintes passam a enxergar os
/// dados dela. A falha é silenciosa e passa numa suíte que só exercita uma
/// filial — por isso estes testes usam duas, no mesmo processo, alternando a
/// ordem de propósito.
/// </summary>
[Trait("Category", "Integration")]
public class BranchIsolationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    private readonly BranchContext _mega = new() { BranchId = Guid.NewGuid() };
    private readonly BranchContext _penha = new() { BranchId = Guid.NewGuid() };

    public BranchIsolationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Sales_AreVisibleOnlyToTheirOwnBranch()
    {
        var vendaMega = await SeedSaleAsync(_mega);
        var vendaPenha = await SeedSaleAsync(_penha);

        await using (var ctx = _fixture.CreateContext(_mega))
        {
            var ids = await ctx.Sales.AsNoTracking().Select(s => s.Id).ToListAsync();
            Assert.Contains(vendaMega, ids);
            Assert.DoesNotContain(vendaPenha, ids);
        }

        // A ordem inversa importa: se o modelo tivesse congelado a filial da
        // primeira consulta, este bloco veria os dados da MEGA.
        await using (var ctx = _fixture.CreateContext(_penha))
        {
            var ids = await ctx.Sales.AsNoTracking().Select(s => s.Id).ToListAsync();
            Assert.Contains(vendaPenha, ids);
            Assert.DoesNotContain(vendaMega, ids);
        }
    }

    [Fact]
    public async Task FindingByIdAcrossBranches_ReturnsNothing()
    {
        var vendaPenha = await SeedSaleAsync(_penha);

        await using var ctx = _fixture.CreateContext(_mega);

        // Conhecer o id de uma venda de outra loja não dá acesso a ela.
        var encontrada = await ctx.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == vendaPenha);
        Assert.Null(encontrada);
    }

    [Fact]
    public async Task Quotations_AreIsolated()
    {
        var orcMega = await SeedQuotationAsync(_mega);
        var orcPenha = await SeedQuotationAsync(_penha);

        await using var ctx = _fixture.CreateContext(_penha);
        var ids = await ctx.Quotations.AsNoTracking().Select(q => q.Id).ToListAsync();

        Assert.Contains(orcPenha, ids);
        Assert.DoesNotContain(orcMega, ids);
    }

    [Fact]
    public async Task Interceptor_StampsBranch_WithoutTheServiceKnowing()
    {
        // O serviço não preenche BranchId em lugar nenhum — quem carimba é o
        // interceptor. É isso que impede que um caminho de escrita esquecido
        // grave na loja errada.
        Guid saleId;
        await using (var ctx = _fixture.CreateContext(_penha))
        {
            var sale = NewSale();
            ctx.Sales.Add(sale);
            await ctx.SaveChangesAsync();
            saleId = sale.Id;

            Assert.Equal(_penha.BranchId, sale.BranchId);
        }

        // Confere no banco, sem filtro, que a coluna foi mesmo gravada.
        await using var verify = _fixture.CreateContext(_penha);
        var persisted = await verify.Sales.AsNoTracking().FirstAsync(s => s.Id == saleId);
        Assert.Equal(_penha.BranchId, persisted.BranchId);
    }

    [Fact]
    public async Task IgnoreQueryFilters_SeesEveryBranch()
    {
        var vendaMega = await SeedSaleAsync(_mega);
        var vendaPenha = await SeedSaleAsync(_penha);

        await using var ctx = _fixture.CreateContext(_mega);
        var ids = await ctx.Sales.AsNoTracking().IgnoreQueryFilters().Select(s => s.Id).ToListAsync();

        // Escotilha de emergência para relatório consolidado e manutenção — o
        // isolamento é padrão, não uma barreira intransponível.
        Assert.Contains(vendaMega, ids);
        Assert.Contains(vendaPenha, ids);
    }

    [Fact]
    public async Task NumberSequence_CountsIndependentlyPerBranch()
    {
        var prefixo = $"ORC{Guid.NewGuid():N}".Substring(0, 12);

        await using var ctxMega = _fixture.CreateContext(_mega);
        await using var ctxPenha = _fixture.CreateContext(_penha);

        var mega1 = await new NumberSequenceGenerator(ctxMega, _mega).NextAsync(prefixo);
        var mega2 = await new NumberSequenceGenerator(ctxMega, _mega).NextAsync(prefixo);
        var penha1 = await new NumberSequenceGenerator(ctxPenha, _penha).NextAsync(prefixo);

        Assert.Equal(1, mega1);
        Assert.Equal(2, mega2);

        // A PENHA começa do 1 mesmo com o mesmo prefixo: a contagem é da loja.
        Assert.Equal(1, penha1);
    }

    [Fact]
    public async Task ProductStock_IsIsolated_ButCatalogIsShared()
    {
        Guid productId;
        await using (var ctx = _fixture.CreateContext(_mega))
        {
            var product = new Product
            {
                Sku = $"SKU{Guid.NewGuid():N}".Substring(0, 20),
                Name = "Peça compartilhada",
                SalePrice = 50m,
                CostPrice = 25m,
                StockQuantity = 8m,
                IsActive = true,
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            productId = product.Id;
        }

        await using (var ctx = _fixture.CreateContext(_mega))
            await new ProductRepository(ctx, _mega).ReserveStockAsync(productId, 3m);

        // O produto é da empresa: a PENHA enxerga o catálogo...
        await using (var ctx = _fixture.CreateContext(_penha))
        {
            Assert.True(await ctx.Products.AsNoTracking().AnyAsync(p => p.Id == productId));

            // ...mas não o estoque da outra loja.
            Assert.False(await ctx.ProductStocks.AsNoTracking().AnyAsync(s => s.ProductId == productId));
        }

        await using (var ctx = _fixture.CreateContext(_mega))
        {
            var stock = await ctx.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == productId);
            Assert.Equal(3m, stock.Reserved);
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static Sale NewSale() => new()
    {
        SaleNumber = $"TST{Guid.NewGuid():N}".Substring(0, 14),
        Status = SaleStatus.PendingPayment,
        Subtotal = 100m,
        TotalAmount = 100m,
    };

    private async Task<Guid> SeedSaleAsync(BranchContext branch)
    {
        await using var ctx = _fixture.CreateContext(branch);
        var sale = NewSale();
        ctx.Sales.Add(sale);
        await ctx.SaveChangesAsync();
        return sale.Id;
    }

    private async Task<Guid> SeedQuotationAsync(BranchContext branch)
    {
        await using var ctx = _fixture.CreateContext(branch);
        var quotation = new Quotation
        {
            QuotationNumber = $"ORC{Guid.NewGuid():N}".Substring(0, 14),
            Status = QuotationStatus.Draft,
            Subtotal = 100m,
            TotalAmount = 100m,
        };
        ctx.Quotations.Add(quotation);
        await ctx.SaveChangesAsync();
        return quotation.Id;
    }
}
