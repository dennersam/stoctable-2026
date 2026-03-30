using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities;

public class Quotation : BaseEntity
{
    public string QuotationNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid? SalespersonId { get; set; }
    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? ConvertedToSaleId { get; set; }

    public Customer? Customer { get; set; }
    public User? Salesperson { get; set; }
    public ICollection<QuotationItem> Items { get; set; } = [];
    public ICollection<StockReservation> StockReservations { get; set; } = [];
}
