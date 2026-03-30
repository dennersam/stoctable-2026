using Stoctable.Domain.Contracts.Services;

namespace Stoctable.Infrastructure.Tenancy;

public class TenantConnectionProvider(TenantContext tenantContext) : ITenantConnectionProvider
{
    public string GetConnectionString()
    {
        if (!tenantContext.IsResolved)
            throw new InvalidOperationException(
                "Tenant connection string not resolved. Ensure X-Branch-Id header is present.");

        return tenantContext.ConnectionString!;
    }
}
