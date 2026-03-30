using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class Supplier : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string? Cnpj { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactPerson { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
