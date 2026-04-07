using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Interceptors;
using Stoctable.Infrastructure.Repositories;
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

        // Audit interceptor
        services.AddScoped<AuditSaveChangesInterceptor>();

        // DbContext com connection string resolvida dinamicamente via TenantContext
        services.AddDbContext<StoctableDbContext>((sp, options) =>
        {
            var tenantContext = sp.GetRequiredService<TenantContext>();
            var auditInterceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            var config = sp.GetRequiredService<IConfiguration>();

            // Prioridade: TenantContext (runtime) → user-secrets/appsettings → env var → localhost
            var connectionString = tenantContext.IsResolved
                ? tenantContext.ConnectionString!
                : config["DefaultBranchConnectionString"]
                  ?? Environment.GetEnvironmentVariable("DEFAULT_CONN_STRING")
                  ?? "Host=localhost;Database=stoctable_branch_dev;Username=postgres;Password=postgres";

            options.UseNpgsql(connectionString)
                   .AddInterceptors(auditInterceptor)
                   .EnableSensitiveDataLogging()
                   .EnableDetailedErrors()
                   .LogTo(msg => System.Console.WriteLine(msg), Microsoft.Extensions.Logging.LogLevel.Information);
        });

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IManufacturerRepository, ManufacturerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();

        return services;
    }
}
