using Stoctable.Domain.Entities;

namespace Stoctable.Domain.Contracts.Repositories;

public interface ISupplierRepository : IRepository<Supplier>
{
    Task<Supplier?> GetByCnpjAsync(string cnpj, CancellationToken ct = default);
    Task<IEnumerable<Supplier>> SearchAsync(string query, CancellationToken ct = default);
    Task<(IEnumerable<Supplier> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default);
}
