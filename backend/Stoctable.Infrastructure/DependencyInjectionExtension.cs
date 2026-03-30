using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Infrastructure;

public static class DependencyInjectionExtension
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Tenancy — BranchConnectionCache é singleton (cache em memória)
        services.AddSingleton<BranchConnectionCache>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantConnectionProvider, TenantConnectionProvider>();

        // DbContext com connection string resolvida dinamicamente via TenantContext
        services.AddDbContext<StoctableDbContext>((sp, options) =>
        {
            var tenantContext = sp.GetRequiredService<TenantContext>();

            // Durante migrations (design-time), usa string de fallback
            var connectionString = tenantContext.IsResolved
                ? tenantContext.ConnectionString!
                : Environment.GetEnvironmentVariable("DEFAULT_CONN_STRING")
                  ?? "Host=localhost;Database=stoctable_branch_dev;Username=postgres;Password=postgres";

            options.UseNpgsql(connectionString);
        });

        return services;
    }
}
