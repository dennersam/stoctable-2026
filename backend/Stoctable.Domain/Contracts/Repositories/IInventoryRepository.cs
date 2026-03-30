using Stoctable.Domain.Entities;

namespace Stoctable.Domain.Contracts.Repositories;

public interface IInventoryRepository
{
    Task<IEnumerable<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken ct = default);
    Task AddMovementAsync(InventoryMovement movement, CancellationToken ct = default);
    Task<StockReservation?> GetActiveReservationAsync(Guid productId, Guid quotationId, CancellationToken ct = default);
    Task AddReservationAsync(StockReservation reservation, CancellationToken ct = default);
    Task<IEnumerable<StockReservation>> GetActiveReservationsByQuotationAsync(Guid quotationId, CancellationToken ct = default);
}
