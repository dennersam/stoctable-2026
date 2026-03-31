using Stoctable.Domain.Entities;

namespace Stoctable.Domain.Contracts.Repositories;

public interface IManufacturerRepository : IRepository<Manufacturer>
{
    Task<IEnumerable<Manufacturer>> SearchAsync(string query, CancellationToken ct = default);
    Task<IEnumerable<Manufacturer>> GetActiveAsync(CancellationToken ct = default);
}
