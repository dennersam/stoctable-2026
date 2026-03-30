using Stoctable.Domain.Entities;

namespace Stoctable.Domain.Contracts.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<IEnumerable<Product>> SearchAsync(string query, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetLowStockAsync(CancellationToken ct = default);
}
