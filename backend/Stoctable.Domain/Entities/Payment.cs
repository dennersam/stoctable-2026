using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities;

public class Payment : BaseEntity, IBranchScoped
{
    /// <summary>
    /// Filial do recebimento. Redundante com Sale.BranchId de propósito: o
    /// fechamento de caixa consulta pagamentos direto por período e filial, sem
    /// passar pela venda, e é essa consulta que justifica o escopo por filial.
    /// </summary>
    public Guid BranchId { get; set; }

    public Guid SaleId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public int Installments { get; set; } = 1;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Código de autorização do cartão, TxID do PIX, etc.</summary>
    public string? TransactionRef { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }

    public Sale? Sale { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
}
