namespace Stoctable.Communication.Requests.Inventory;

public record CreateStockTransferItem(Guid ProductId, decimal Quantity);

/// <summary>
/// A origem é sempre a filial ativa da sessão — nunca vem no corpo. Só o destino
/// é informado, e é conferido contra as claims assinadas.
/// </summary>
public record CreateStockTransferRequest(
    Guid DestinationBranchId,
    List<CreateStockTransferItem> Items,
    string? Notes = null);

public record ReceiveStockTransferItem(Guid ProductId, decimal QuantityReceived);

/// <summary>
/// Conferência do destino. Item omitido assume que chegou tudo o que saiu, de
/// modo que o recebimento sem divergência é só um POST com lista vazia.
/// </summary>
public record ReceiveStockTransferRequest(List<ReceiveStockTransferItem>? Items = null);

public record CancelStockTransferRequest(string CancellationReason = "");
