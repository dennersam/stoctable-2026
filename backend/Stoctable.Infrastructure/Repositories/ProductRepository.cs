using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class ProductRepository(StoctableDbContext context) : Repository<Product>(context), IProductRepository
{
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
    {
        var lower = query.ToLower();
        return await DbSet
            .Where(p => p.IsActive && (
                p.Sku.ToLower().Contains(lower) ||
                p.Name.ToLower().Contains(lower) ||
                (p.Barcode != null && p.Barcode.Contains(lower)) ||
                (p.Manufacturer != null && p.Manufacturer.Name.ToLower().Contains(lower))))
            .Include(p => p.Category)
            .Include(p => p.Manufacturer)
            .Include(p => p.Supplier)
            .Take(50)
            .ToListAsync(ct);
    }

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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(p =>
                p.Sku.ToLower().Contains(lower) ||
                p.Name.ToLower().Contains(lower) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(lower)) ||
                (p.Manufacturer != null && p.Manufacturer.Name.ToLower().Contains(lower)));
        }

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
}
