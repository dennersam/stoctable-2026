using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class InventoryRepository(StoctableDbContext context) : IInventoryRepository
{
    public async Task<IEnumerable<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken ct = default)
        => await context.InventoryMovements
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

    public async Task AddMovementAsync(InventoryMovement movement, CancellationToken ct = default)
    {
        await context.InventoryMovements.AddAsync(movement, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<StockReservation?> GetActiveReservationAsync(Guid productId, Guid quotationId, CancellationToken ct = default)
        => await context.StockReservations
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.QuotationId == quotationId && r.IsActive, ct);

    public async Task AddReservationAsync(StockReservation reservation, CancellationToken ct = default)
    {
        await context.StockReservations.AddAsync(reservation, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<StockReservation>> GetActiveReservationsByQuotationAsync(Guid quotationId, CancellationToken ct = default)
        => await context.StockReservations
            .Where(r => r.QuotationId == quotationId && r.IsActive)
            .ToListAsync(ct);

    public async Task UpdateReservationAsync(StockReservation reservation, CancellationToken ct = default)
    {
        context.StockReservations.Update(reservation);
        await context.SaveChangesAsync(ct);
    }
}
