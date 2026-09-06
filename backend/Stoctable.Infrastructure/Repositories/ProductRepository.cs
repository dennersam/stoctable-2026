using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Search;

namespace Stoctable.Infrastructure.Repositories;

public class ProductRepository(StoctableDbContext context) : Repository<Product>(context), IProductRepository
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
}
