using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Contracts.Repositories;

public interface IQuotationRepository : IRepository<Quotation>
{
    Task<Quotation?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Quotation>> GetByStatusAsync(QuotationStatus status, CancellationToken ct = default);
    Task<string> GenerateNextNumberAsync(CancellationToken ct = default);
}
