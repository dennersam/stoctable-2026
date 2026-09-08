using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Contracts.Repositories;

/// <summary>
/// Direção da transferência do ponto de vista da filial ativa.
/// </summary>
public enum TransferDirection
{
    /// <summary>Emitidas por esta filial.</summary>
    Outbound,

    /// <summary>Endereçadas a esta filial.</summary>
    Inbound
}

public interface IStockTransferRepository : IRepository<StockTransfer>
{
    Task<StockTransfer?> GetWithItemsAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<StockTransfer>> GetAsync(
        TransferDirection direction, StockTransferStatus? status, CancellationToken ct = default);

    /// <summary>Próximo número da sequência TRF desta filial de origem.</summary>
    Task<string> GenerateNextNumberAsync(CancellationToken ct = default);
}
