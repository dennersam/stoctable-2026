using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class PaymentMethod : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool RequiresInstallments { get; set; }
    public int MaxInstallments { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public ICollection<Payment> Payments { get; set; } = [];
}
