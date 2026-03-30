using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities;

public class Sale : BaseEntity
{
    public string SaleNumber { get; set; } = string.Empty;
    public Guid? QuotationId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalespersonId { get; set; }
    public Guid? CashierId { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.PendingPayment;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Customer? Customer { get; set; }
    public User? Salesperson { get; set; }
    public User? Cashier { get; set; }
    public ICollection<SaleItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
