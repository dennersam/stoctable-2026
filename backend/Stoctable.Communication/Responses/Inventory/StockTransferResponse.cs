namespace Stoctable.Communication.Responses.Inventory;

public record StockTransferItemResponse(
    Guid ProductId,
    string? ProductName,
    string? Sku,
    decimal QuantitySent,
    decimal? QuantityReceived);

public record StockTransferResponse(
    Guid Id,
    string TransferNumber,
    Guid OriginBranchId,
    Guid DestinationBranchId,
    string Status,
    bool HasDivergence,
    DateTimeOffset? ShippedAt,
    string? ShippedBy,
    DateTimeOffset? ReceivedAt,
    string? ReceivedBy,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    string? Notes,
    DateTimeOffset CreatedAt,
    IReadOnlyList<StockTransferItemResponse> Items);

/// <summary>Saldo de um produto numa filial da rede, para a tela de consulta.</summary>
public record BranchStockResponse(
    Guid BranchId,
    decimal Quantity,
    decimal Reserved,
    decimal Available,
    decimal Minimum,
    /// <summary>Enviado e ainda não recebido — nem está aqui, nem no destino.</summary>
    decimal InTransit);
