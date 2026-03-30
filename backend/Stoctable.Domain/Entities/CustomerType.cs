using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class CustomerType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal DiscountPct { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Customer> Customers { get; set; } = [];
}
