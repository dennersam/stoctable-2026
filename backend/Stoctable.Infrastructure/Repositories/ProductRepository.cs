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
