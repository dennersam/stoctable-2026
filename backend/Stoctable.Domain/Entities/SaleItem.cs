using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal LineTotal { get; set; }

    public Sale? Sale { get; set; }
    public Product? Product { get; set; }
}
