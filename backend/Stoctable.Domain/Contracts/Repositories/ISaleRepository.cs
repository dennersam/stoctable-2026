using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Contracts.Repositories;

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Sale>> GetByStatusAsync(SaleStatus status, CancellationToken ct = default);
    Task<string> GenerateNextNumberAsync(CancellationToken ct = default);
}
