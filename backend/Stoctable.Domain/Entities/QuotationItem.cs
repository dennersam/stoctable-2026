using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class QuotationItem : BaseEntity
{
    public Guid QuotationId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal LineTotal { get; set; }

    public Quotation? Quotation { get; set; }
    public Product? Product { get; set; }
}
