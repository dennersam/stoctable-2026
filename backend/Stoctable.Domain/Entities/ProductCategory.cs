using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class ProductCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductCategory? Parent { get; set; }
    public ICollection<ProductCategory> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
