using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities;

public class AuditLog : IBranchScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Filial em que a alteração aconteceu.</summary>
    public Guid BranchId { get; set; }

    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }

    /// <summary>Valores anteriores à mudança, serializados como JSON</summary>
    public string? OldValues { get; set; }

    /// <summary>Valores após a mudança, serializados como JSON</summary>
    public string? NewValues { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
