using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class Manufacturer : BaseEntity
{
    /// <summary>Nome do fabricante (ex: Pirelli, Honda, NGK)</summary>
    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}
