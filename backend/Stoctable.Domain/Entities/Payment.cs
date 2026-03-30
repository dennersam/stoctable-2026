using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public int Installments { get; set; } = 1;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Código de autorização do cartão, TxID do PIX, etc.</summary>
    public string? TransactionRef { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public Sale? Sale { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
}
