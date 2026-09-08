using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

/// <summary>
/// Item de uma transferência.
///
/// Sem <c>branch_id</c> de propósito: só é alcançável pelo pai, que já é
/// filtrado — mesmo padrão de <c>SaleItem</c> e <c>QuotationItem</c>.
/// </summary>
public class StockTransferItem : BaseEntity
{
    public Guid TransferId { get; set; }
    public StockTransfer? Transfer { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Quantidade que saiu da origem.</summary>
    public decimal QuantitySent { get; set; }

    /// <summary>
    /// Quantidade conferida no destino. Fica null até o recebimento — nulo aqui
    /// significa "ainda não conferido", que é diferente de "chegou zero".
    /// </summary>
    public decimal? QuantityReceived { get; set; }
}
