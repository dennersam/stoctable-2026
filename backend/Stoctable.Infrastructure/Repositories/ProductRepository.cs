using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class ProductRepository(StoctableDbContext context) : Repository<Product>(context), IProductRepository
{
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
                (p.Manufacturer != null && p.Manufacturer.ToLower().Contains(lower))))
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Product>> GetLowStockAsync(CancellationToken ct = default)
        => await DbSet
            .Where(p => p.IsActive && p.StockQuantity <= p.StockMinimum)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync(ct);
}
