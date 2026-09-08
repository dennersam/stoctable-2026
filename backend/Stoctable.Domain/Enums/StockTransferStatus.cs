namespace Stoctable.Domain.Enums;

/// <summary>
/// Ciclo de vida de uma transferência entre filiais.
///
/// Só existe caminho para frente, e isso é deliberado — ver o comentário sobre
/// cancelamento em <see cref="Entities.StockTransfer"/>.
/// </summary>
public enum StockTransferStatus
{
    /// <summary>Rascunho na origem. Nenhum estoque foi tocado ainda.</summary>
    Pending,

    /// <summary>Saiu da origem e ainda não chegou ao destino.</summary>
    InTransit,

    /// <summary>Conferida e recebida no destino.</summary>
    Received,

    /// <summary>Descartada antes do envio.</summary>
    Cancelled
}
