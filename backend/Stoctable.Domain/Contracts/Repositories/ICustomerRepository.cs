using Stoctable.Domain.Entities;

namespace Stoctable.Domain.Contracts.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByDocumentAsync(string documentNumber, CancellationToken ct = default);
    Task<IEnumerable<Customer>> SearchAsync(string query, CancellationToken ct = default);
    Task<Customer?> GetWithCrmNotesAsync(Guid id, CancellationToken ct = default);
    Task<(IEnumerable<Customer> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default);
}
