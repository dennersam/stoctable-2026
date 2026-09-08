using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Cobre a correção da busca por tokens: o predicado antigo exigia o termo
/// inteiro como substring contígua de uma única coluna, então "capacete rosa"
/// não encontrava "Capacete Moto Rosa".
/// </summary>
/// <summary>
/// Semeia o catálogo de busca uma única vez por classe de teste — o xUnit cria
/// uma instância da classe de teste por [Fact], então o seed não pode viver lá.
/// </summary>
public class ProductSearchFixture : PostgresFixture
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        await using var ctx = CreateContext();

        var bosch = new Manufacturer { Id = Guid.NewGuid(), Name = "Bosch", IsActive = true };
        ctx.Manufacturers.Add(bosch);

        ctx.Products.AddRange(
            NewProduct("CAP-01", "Capacete Moto Rosa", barcode: "7891234567890"),
            NewProduct("ACU-01", "Açúcar Cristal Refinado"),
            NewProduct("FUR-01", "Furadeira de Impacto", manufacturerId: bosch.Id),
            NewProduct("DES-01", "Desconto 50% Promocional"),
            NewProduct("CAP-02", "Capacete Preto Fechado"));

        await ctx.SaveChangesAsync();
    }

    private static Product NewProduct(
        string sku, string name, string? barcode = null, Guid? manufacturerId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name,
            Barcode = barcode,
            ManufacturerId = manufacturerId,
            Unit = "UN",
            CostPrice = 10m,
            SalePrice = 20m,
            StockQuantity = 5m,
            IsActive = true,
        };
}

[Trait("Category", "Integration")]
public class ProductSearchTests : IClassFixture<ProductSearchFixture>
{
    private readonly ProductSearchFixture _fixture;

    public ProductSearchTests(ProductSearchFixture fixture) => _fixture = fixture;

    // ─── O bug relatado ─────────────────────────────────────────────────────

    [Fact]
    public async Task Search_MultiplasPalavrasNaoAdjacentes_Encontra()
    {
        var results = await SearchAsync("capacete rosa");
        Assert.Single(results);
        Assert.Equal("Capacete Moto Rosa", results[0].Name);
    }

    [Fact]
    public async Task Search_PalavrasForaDeOrdem_Encontra()
    {
        var results = await SearchAsync("rosa capacete");
        Assert.Single(results);
        Assert.Equal("Capacete Moto Rosa", results[0].Name);
    }

    // ─── Acentuação ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_TermoSemAcento_EncontraNomeComAcento()
    {
        var results = await SearchAsync("acucar");
        Assert.Single(results);
        Assert.Equal("Açúcar Cristal Refinado", results[0].Name);
    }

    [Fact]
    public async Task Search_TermoComAcento_EncontraNomeComAcento()
    {
        var results = await SearchAsync("AÇÚCAR cristal");
        Assert.Single(results);
    }

    // ─── Regressões do comportamento que já funcionava ──────────────────────

    [Fact]
    public async Task Search_SkuParcial_ContinuaEncontrando()
    {
        var results = await SearchAsync("cap-0");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Search_CodigoDeBarrasParcial_ContinuaEncontrando()
    {
        var results = await SearchAsync("7891");
        Assert.Single(results);
        Assert.Equal("Capacete Moto Rosa", results[0].Name);
    }

    [Fact]
    public async Task Search_NomeDoFabricante_ContinuaEncontrando()
    {
        var results = await SearchAsync("bosch");
        Assert.Single(results);
        Assert.Equal("Furadeira de Impacto", results[0].Name);
    }

    // ─── Casos de borda ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_TermoVazio_RetornaTudo(string term)
    {
        var results = await SearchAsync(term);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task Search_PercentualLiteral_NaoEhTratadoComoCuringa()
    {
        // Sem escape, "50%" casaria com qualquer coisa começando em "50".
        var results = await SearchAsync("50%");
        Assert.Single(results);
        Assert.Equal("Desconto 50% Promocional", results[0].Name);
    }

    [Fact]
    public async Task Search_SublinhadoLiteral_NaoEhTratadoComoCuringa()
    {
        // '_' sem escape casa qualquer caractere: "c_p" acharia "cap".
        var results = await SearchAsync("c_p");
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_SemCorrespondencia_RetornaVazio()
    {
        var results = await SearchAsync("xyzzy");
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_UmaPalavraCasaOutraNao_NaoRetornaNada()
    {
        // Confirma o AND entre tokens: "capacete" casa, "guidão" não.
        var results = await SearchAsync("capacete guidao");
        Assert.Empty(results);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<List<Product>> SearchAsync(string term)
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new ProductRepository(ctx, new BranchContext());
        var (items, _) = await repo.GetPagedAsync(page: 1, pageSize: 50, search: term);
        return items.ToList();
    }
}
