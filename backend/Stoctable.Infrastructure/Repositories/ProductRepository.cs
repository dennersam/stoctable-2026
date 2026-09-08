using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Search;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Infrastructure.Repositories;

public class ProductRepository(StoctableDbContext context, BranchContext branchContext)
    : Repository<Product>(context), IProductRepository
{
    private static readonly Expression<Func<Product, string?>>[] SearchFields =
    [
        p => p.Name,
        p => p.Sku,
        p => p.Barcode,
        p => p.Manufacturer!.Name,
    ];

    public override async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Category)
            .Include(p => p.Manufacturer)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    // Carrega o produto sem rastreamento — use quando apenas leitura é necessária
    // (ex: obter SalePrice para criar QuotationItem) para evitar relationship fixup
    // indesejado entre Product.QuotationItems e o QuotationItem recém-criado,
    // que causava DbUpdateConcurrencyException no EF Core 9 + Npgsql 9.
    public async Task<Product?> GetByIdNoTrackingAsync(Guid id, CancellationToken ct = default)
        => await DbSet.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public override async Task<IEnumerable<Product>> GetAllAsync(CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Category)
            .Include(p => p.Manufacturer)
            .Include(p => p.Supplier)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(p => p.Sku == sku, ct);

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(p => p.Barcode == barcode, ct);

    public async Task<IEnumerable<Product>> SearchAsync(string query, CancellationToken ct = default)
        => await DbSet
            .Where(p => p.IsActive)
            .WhereMatchesAllTokens(query, SearchFields)
            .Include(p => p.Category)
            .Include(p => p.Manufacturer)
            .Include(p => p.Supplier)
            .OrderBy(p => p.Name)
            .Take(50)
            .ToListAsync(ct);

    public async Task<IEnumerable<Product>> GetLowStockAsync(CancellationToken ct = default)
        => await DbSet
            .Where(p => p.IsActive && p.StockQuantity <= p.StockMinimum)
            .Include(p => p.Manufacturer)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync(ct);

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(p => p.Category)
            .Include(p => p.Manufacturer)
            .Include(p => p.Supplier)
            .AsQueryable();

        query = query.WhereMatchesAllTokens(search, SearchFields);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> GetNextSkuAsync(CancellationToken ct = default)
    {
        var skus = await DbSet.Select(p => p.Sku).ToListAsync(ct);
        var maxInt = skus
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return maxInt + 1;
    }

    public async Task<bool> TryDecrementStockAsync(Guid productId, decimal quantity, CancellationToken ct = default)
    {
        // UPDATE atômico — a guarda WHERE stock_quantity >= quantity garante
        // que dois caixas convertendo orçamentos do mesmo produto nunca
        // resultem em estoque negativo, mesmo sem optimistic lock.
        // Apenas stock_quantity é tocado aqui; stock_reserved é liberado
        // separadamente em StockReservation via ReleaseReservationsAsync.
        var rowsAffected = await Context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE products
                  SET stock_quantity = stock_quantity - {quantity},
                      updated_at     = NOW()
                WHERE id = {productId}
                  AND stock_quantity >= {quantity}", ct);

        if (rowsAffected == 1)
        {
            await MirrorStockAsync(productId, quantity: -quantity, reserved: 0, ct);

            // Mantém o estado em memória coerente caso a entidade esteja
            // sendo rastreada pelo DbContext desta scope.
            var tracked = Context.ChangeTracker.Entries<Product>()
                .FirstOrDefault(e => e.Entity.Id == productId);
            if (tracked is not null)
            {
                tracked.Entity.StockQuantity -= quantity;
                tracked.State = EntityState.Unchanged;
            }
        }

        return rowsAffected == 1;
    }

    public async Task<bool> IncrementStockAsync(Guid productId, decimal quantity, CancellationToken ct = default)
    {
        var rowsAffected = await Context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE products
                  SET stock_quantity = stock_quantity + {quantity},
                      updated_at     = NOW()
                WHERE id = {productId}", ct);

        if (rowsAffected == 1)
        {
            await MirrorStockAsync(productId, quantity: quantity, reserved: 0, ct);

            var tracked = Context.ChangeTracker.Entries<Product>()
                .FirstOrDefault(e => e.Entity.Id == productId);
            if (tracked is not null)
            {
                tracked.Entity.StockQuantity += quantity;
                tracked.State = EntityState.Unchanged;
            }
        }

        return rowsAffected == 1;
    }

    public async Task ReserveStockAsync(Guid productId, decimal quantity, CancellationToken ct = default)
    {
        await Context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE products
                  SET stock_reserved = stock_reserved + {quantity},
                      updated_at     = NOW()
                WHERE id = {productId}", ct);

        await MirrorStockAsync(productId, quantity: 0, reserved: quantity, ct);

        var tracked = Context.ChangeTracker.Entries<Product>()
            .FirstOrDefault(e => e.Entity.Id == productId);
        if (tracked is not null)
        {
            tracked.Entity.StockReserved += quantity;
            tracked.State = EntityState.Unchanged;
        }
    }

    public async Task ReleaseReservedStockAsync(Guid productId, decimal quantity, CancellationToken ct = default)
    {
        // GREATEST(0, ...) porque a liberação pode ser chamada mais de uma vez
        // para a mesma reserva (cancelamento seguido de expiração, por exemplo)
        // e reserva negativa não significa nada.
        await Context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE products
                  SET stock_reserved = GREATEST(0, stock_reserved - {quantity}),
                      updated_at     = NOW()
                WHERE id = {productId}", ct);

        await MirrorStockAsync(productId, quantity: 0, reserved: -quantity, ct);

        var tracked = Context.ChangeTracker.Entries<Product>()
            .FirstOrDefault(e => e.Entity.Id == productId);
        if (tracked is not null)
        {
            tracked.Entity.StockReserved = Math.Max(0, tracked.Entity.StockReserved - quantity);
            tracked.State = EntityState.Unchanged;
        }
    }

    /// <summary>
    /// Aplica o mesmo delta em <c>product_stocks</c>, a fonte da verdade futura.
    ///
    /// Enquanto a transição dura, toda escrita de estoque acontece nos dois
    /// lugares: <c>products</c> continua autoritativo (é dele que as leituras
    /// saem) e esta tabela é escrita em paralelo, para que a reconciliação entre
    /// as duas prove que nenhum caminho de escrita ficou de fora antes de
    /// derrubar as colunas antigas.
    ///
    /// O upsert é necessário porque a linha pode não existir: um produto nunca
    /// movimentado nesta filial não tem registro de estoque.
    ///
    /// A linha criada na PRIMEIRA operação de uma filial nasce de formas
    /// diferentes conforme a filial, e a distinção importa:
    ///
    /// - Filial legada (a única que existe hoje): adota o saldo de
    ///   <c>products</c>. Um produto cadastrado já com estoque inicial, ou
    ///   semeado direto pelo EF, nunca passou por aqui — se a linha nascesse só
    ///   com o delta, começaria divergente da fonte autoritativa.
    /// - Qualquer outra filial: começa do zero e aplica o delta. O saldo em
    ///   <c>products</c> é o agregado da empresa; adotá-lo aqui daria à loja
    ///   nova o estoque inteiro das outras.
    ///
    /// Essa bifurcação existe só enquanto a coluna antiga viver. Quando
    /// <c>products.stock_quantity</c> for derrubada, este método some junto.
    /// </summary>
    private async Task MirrorStockAsync(Guid productId, decimal quantity, decimal reserved, CancellationToken ct)
    {
        var branchId = branchContext.BranchId;
        var legacyBranchId = BranchContext.LegacySingleBranchId;

        await Context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO product_stocks (id, branch_id, product_id, quantity, reserved, created_at, created_by)
               SELECT gen_random_uuid(),
                      {branchId},
                      p.id,
                      CASE WHEN {branchId} = {legacyBranchId} THEN p.stock_quantity ELSE GREATEST(0, {quantity}) END,
                      CASE WHEN {branchId} = {legacyBranchId} THEN p.stock_reserved ELSE GREATEST(0, {reserved}) END,
                      NOW(),
                      'system'
                 FROM products p
                WHERE p.id = {productId}
               ON CONFLICT (branch_id, product_id) DO UPDATE
                  SET quantity   = GREATEST(0, product_stocks.quantity + {quantity}),
                      reserved   = GREATEST(0, product_stocks.reserved + {reserved}),
                      updated_at = NOW()", ct);
    }
}
