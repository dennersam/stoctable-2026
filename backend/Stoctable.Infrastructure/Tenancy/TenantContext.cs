namespace Stoctable.Infrastructure.Tenancy;

/// <summary>
/// Contexto de tenant com escopo por requisição.
/// Preenchido pelo TenantResolutionMiddleware a partir do header X-Branch-Id.
/// </summary>
public class TenantContext
{
    public string? BranchId { get; set; }
    public string? ConnectionString { get; set; }

    public bool IsResolved => !string.IsNullOrEmpty(ConnectionString);
}
