using Stoctable.Domain.Contracts.Services;

namespace Stoctable.Infrastructure.Tenancy;

public class CurrentTenant(TenantContext tenantContext, BranchContext branchContext) : ICurrentTenant
{
    public Guid CompanyId => tenantContext.CompanyId
        ?? throw new InvalidOperationException(
            "Requisição sem empresa resolvida. Isto indica um endpoint de negócio "
            + "alcançado sem passar pelo TenantResolutionMiddleware autenticado.");

    public Guid BranchId => branchContext.BranchId;

    public IReadOnlyCollection<Guid> AllowedBranchIds => branchContext.AllowedBranchIds;
}
