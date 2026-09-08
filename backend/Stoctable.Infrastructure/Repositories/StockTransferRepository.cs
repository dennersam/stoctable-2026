using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Infrastructure.Repositories;

public class StockTransferRepository(
    StoctableDbContext context,
    BranchContext branchContext,
    NumberSequenceGenerator sequence)
    : Repository<StockTransfer>(context), IStockTransferRepository
{
    public async Task<StockTransfer?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <summary>
    /// O filtro global já restringe às transferências que esta filial pode ver
    /// (emitidas por ela ou endereçadas a ela). A direção apenas escolhe qual
    /// das duas pernas o usuário quer olhar.
    /// </summary>
    public async Task<IEnumerable<StockTransfer>> GetAsync(
        TransferDirection direction, StockTransferStatus? status, CancellationToken ct = default)
    {
        var branchId = branchContext.BranchId;

        var query = direction == TransferDirection.Outbound
            ? DbSet.Where(t => t.BranchId == branchId)
            : DbSet.Where(t => t.DestinationBranchId == branchId);

        if (status is not null)
            query = query.Where(t => t.Status == status);

        return await query
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<string> GenerateNextNumberAsync(CancellationToken ct = default)
    {
        // A sequência já é por filial (chave composta branch_id + prefix), e o
        // rascunho nasce na origem — então a numeração é sequencial por loja
        // emissora, que é o que se espera de um documento de saída.
        var next = await sequence.NextAsync("TRF", ct);
        return $"TRF{next:D6}";
    }
}
