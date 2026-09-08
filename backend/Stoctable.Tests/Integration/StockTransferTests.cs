using Microsoft.EntityFrameworkCore;
using Stoctable.Application.Services.Inventory;
using Stoctable.Communication.Requests.Inventory;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Transferência entre filiais, em duas etapas.
///
/// O que estes testes protegem: que cada perna escreve SÓ no estoque da própria
/// filial, que as regras de quem envia e quem recebe são checadas no serviço (e
/// não só na rota) e que o filtro de dupla visibilidade não virou um buraco por
/// onde uma terceira loja enxerga o documento.
/// </summary>
[Trait("Category", "Integration")]
public class StockTransferTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly Guid _mega = Guid.NewGuid();
    private readonly Guid _penha = Guid.NewGuid();

    [Fact]
    public async Task HappyPath_MovesStockFromOriginToDestination()
    {
        var productId = await SeedProductWithStockAsync(_mega, 10m);
        var transferId = await CreateAsync(productId, 4m);

        // ── Envio, na sessão da ORIGEM ──
        await using (var ctx = fixture.CreateContext(Branch(_mega)))
        {
            var result = await BuildService(ctx, _mega).ShipAsync(transferId, "op.mega");
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        // Saiu da origem e ainda não chegou: em trânsito não é saldo de ninguém.
        await AssertQuantityAsync(productId, _mega, 6m);
        await AssertNoRowAsync(productId, _penha);

        // ── Recebimento, na sessão do DESTINO ──
        await using (var ctx = fixture.CreateContext(Branch(_penha)))
        {
            var result = await BuildService(ctx, _penha)
                .ReceiveAsync(transferId, new ReceiveStockTransferRequest(), "op.penha");
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        await AssertQuantityAsync(productId, _mega, 6m);
        await AssertQuantityAsync(productId, _penha, 4m);

        // A auditoria fica com uma perna em cada filial, ligadas pelo mesmo
        // ReferenceId — é assim que se reconstrói a transferência depois.
        await using var verify = fixture.CreateContext();
        var movements = await verify.InventoryMovements.AsNoTracking().IgnoreQueryFilters()
            .Where(m => m.ReferenceId == transferId).ToListAsync();

        var saida = Assert.Single(movements, m => m.MovementType == MovementType.TransferOut);
        var entrada = Assert.Single(movements, m => m.MovementType == MovementType.TransferIn);
        Assert.Equal(_mega, saida.BranchId);
        Assert.Equal(_penha, entrada.BranchId);
        Assert.Equal(-4m, saida.Quantity);
        Assert.Equal(4m, entrada.Quantity);
    }

    [Fact]
    public async Task PartialReceipt_FlagsDivergence_AndCreditsOnlyWhatArrived()
    {
        var productId = await SeedProductWithStockAsync(_mega, 10m);
        var transferId = await CreateAsync(productId, 5m);

        await using (var ctx = fixture.CreateContext(Branch(_mega)))
            await BuildService(ctx, _mega).ShipAsync(transferId, "op.mega");

        await using (var ctx = fixture.CreateContext(Branch(_penha)))
        {
            var result = await BuildService(ctx, _penha).ReceiveAsync(
                transferId,
                new ReceiveStockTransferRequest([new ReceiveStockTransferItem(productId, 3m)]),
                "op.penha");
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(result.Data!.HasDivergence);
        }

        // As 2 que faltaram não voltam sozinhas para a origem: a diferença fica
        // registrada e é resolvida por gente, com ajuste local.
        await AssertQuantityAsync(productId, _mega, 5m);
        await AssertQuantityAsync(productId, _penha, 3m);
    }

    [Fact]
    public async Task ReceivingZero_IsAllowed_AndLeavesOriginDebited()
    {
        var productId = await SeedProductWithStockAsync(_mega, 8m);
        var transferId = await CreateAsync(productId, 3m);

        await using (var ctx = fixture.CreateContext(Branch(_mega)))
            await BuildService(ctx, _mega).ShipAsync(transferId, "op.mega");

        await using (var ctx = fixture.CreateContext(Branch(_penha)))
        {
            var result = await BuildService(ctx, _penha).ReceiveAsync(
                transferId,
                new ReceiveStockTransferRequest([new ReceiveStockTransferItem(productId, 0m)]),
                "op.penha");
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(result.Data!.HasDivergence);
        }

        await AssertQuantityAsync(productId, _mega, 5m);
        await AssertNoRowAsync(productId, _penha);
    }

    [Fact]
    public async Task Ship_FromDestinationSession_IsRefused()
    {
        var productId = await SeedProductWithStockAsync(_mega, 10m);
        var transferId = await CreateAsync(productId, 2m);

        // O destino ENXERGA a transferência (filtro de dupla visibilidade), mas
        // enviar é ato da origem — a regra vive no serviço, não na rota.
        await using var ctx = fixture.CreateContext(Branch(_penha));
        var result = await BuildService(ctx, _penha).ShipAsync(transferId, "op.penha");

        Assert.False(result.IsSuccess);
        await AssertQuantityAsync(productId, _mega, 10m);
    }

    [Fact]
    public async Task Receive_FromOriginSession_IsRefused()
    {
        var productId = await SeedProductWithStockAsync(_mega, 10m);
        var transferId = await CreateAsync(productId, 2m);

        await using (var ctx = fixture.CreateContext(Branch(_mega)))
            await BuildService(ctx, _mega).ShipAsync(transferId, "op.mega");

        await using var ctx2 = fixture.CreateContext(Branch(_mega));
        var result = await BuildService(ctx2, _mega)
            .ReceiveAsync(transferId, new ReceiveStockTransferRequest(), "op.mega");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Transfer_IsVisibleToBothBranches_ButNotToAThird()
    {
        var productId = await SeedProductWithStockAsync(_mega, 10m);
        var transferId = await CreateAsync(productId, 1m);

        await using (var origem = fixture.CreateContext(Branch(_mega)))
            Assert.NotNull(await origem.StockTransfers.FirstOrDefaultAsync(t => t.Id == transferId));

        await using (var destino = fixture.CreateContext(Branch(_penha)))
            Assert.NotNull(await destino.StockTransfers.FirstOrDefaultAsync(t => t.Id == transferId));

        // Uma loja que não é nem origem nem destino não enxerga nada. Se este
        // teste falhar, o filtro de dupla visibilidade virou um buraco.
        var terceira = Branch(Guid.NewGuid());
        await using (var outra = fixture.CreateContext(terceira))
            Assert.Null(await outra.StockTransfers.FirstOrDefaultAsync(t => t.Id == transferId));
    }

    [Fact]
    public async Task InTransit_CannotBeCancelled()
    {
        var productId = await SeedProductWithStockAsync(_mega, 10m);
        var transferId = await CreateAsync(productId, 2m);

        await using (var ctx = fixture.CreateContext(Branch(_mega)))
            await BuildService(ctx, _mega).ShipAsync(transferId, "op.mega");

        await using var ctx2 = fixture.CreateContext(Branch(_mega));
        var result = await BuildService(ctx2, _mega).CancelAsync(transferId, "mudei de ideia");

        Assert.False(result.IsSuccess);
        await AssertQuantityAsync(productId, _mega, 8m);
    }

    [Fact]
    public async Task Ship_WithoutEnoughStock_RollsBackEverything()
    {
        var productId = await SeedProductWithStockAsync(_mega, 1m);
        var transferId = await CreateAsync(productId, 5m);

        await using (var ctx = fixture.CreateContext(Branch(_mega)))
        {
            var result = await BuildService(ctx, _mega).ShipAsync(transferId, "op.mega");
            Assert.False(result.IsSuccess);
        }

        // Nada baixado e a transferência continua pendente — dá para tentar de
        // novo depois de repor.
        await AssertQuantityAsync(productId, _mega, 1m);

        await using var verify = fixture.CreateContext(Branch(_mega));
        var transfer = await verify.StockTransfers.AsNoTracking().FirstAsync(t => t.Id == transferId);
        Assert.Equal(StockTransferStatus.Pending, transfer.Status);
    }

    [Fact]
    public async Task Create_WithDestinationOutsideTheClaims_IsRefused()
    {
        var productId = await SeedProductWithStockAsync(_mega, 10m);

        // Destino que não está nas claims: filial de outra empresa, ou id
        // inventado por quem chamou a API na mão.
        await using var ctx = fixture.CreateContext(Branch(_mega));
        var service = BuildService(ctx, _mega);

        var result = await service.CreateAsync(new CreateStockTransferRequest(
            DestinationBranchId: Guid.NewGuid(),
            Items: [new CreateStockTransferItem(productId, 1m)]));

        Assert.False(result.IsSuccess);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private BranchContext Branch(Guid id) => new()
    {
        BranchId = id,
        // As duas lojas do teste; é o que as claims assinadas trariam.
        AllowedBranchIds = [_mega, _penha]
    };

    private async Task<Guid> CreateAsync(Guid productId, decimal quantity)
    {
        await using var ctx = fixture.CreateContext(Branch(_mega));
        var result = await BuildService(ctx, _mega).CreateAsync(new CreateStockTransferRequest(
            DestinationBranchId: _penha,
            Items: [new CreateStockTransferItem(productId, quantity)]));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        return result.Data!.Id;
    }

    private StockTransferService BuildService(StoctableDbContext ctx, Guid branchId)
    {
        var branch = Branch(branchId);
        var sequence = new NumberSequenceGenerator(ctx, branch);
        return new StockTransferService(
            new StockTransferRepository(ctx, branch, sequence),
            new ProductRepository(ctx),
            new ProductStockRepository(ctx, branch),
            new InventoryRepository(ctx),
            new FakeCurrentTenant(branchId, [_mega, _penha]),
            new UnitOfWork(ctx));
    }

    private async Task AssertQuantityAsync(Guid productId, Guid branchId, decimal expected)
    {
        await using var verify = fixture.CreateContext();
        var stock = await verify.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(s => s.ProductId == productId && s.BranchId == branchId);
        Assert.Equal(expected, stock.Quantity);
    }

    private async Task AssertNoRowAsync(Guid productId, Guid branchId)
    {
        await using var verify = fixture.CreateContext();
        Assert.False(await verify.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(s => s.ProductId == productId && s.BranchId == branchId && s.Quantity > 0));
    }

    private async Task<Guid> SeedProductWithStockAsync(Guid branchId, decimal quantity)
    {
        await using var ctx = fixture.CreateContext(Branch(branchId));

        var product = new Product
        {
            Sku = $"SKU{Guid.NewGuid():N}"[..20],
            Name = "Peça Transferível",
            SalePrice = 30m,
            CostPrice = 15m,
            IsActive = true,
        };
        ctx.Products.Add(product);
        ctx.ProductStocks.Add(new ProductStock
        {
            BranchId = branchId,
            ProductId = product.Id,
            Quantity = quantity,
        });
        await ctx.SaveChangesAsync();

        return product.Id;
    }

    private sealed class FakeCurrentTenant(Guid branchId, Guid[] allowed) : ICurrentTenant
    {
        public Guid CompanyId => Guid.Empty;
        public Guid BranchId => branchId;
        public IReadOnlyCollection<Guid> AllowedBranchIds => allowed;
    }
}
