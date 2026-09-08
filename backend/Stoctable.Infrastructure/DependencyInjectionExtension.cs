using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stoctable.Domain.Contracts;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Email;
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

        // Filial ativa da requisição. Hoje sempre a mesma (cada banco tem uma
        // filial só); na fase 3 passa a ser populado pela claim assinada do JWT.
        services.AddScoped<BranchContext>();
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
                   .AddInterceptors(auditInterceptor);

            // Desligado por padrão: estas opções despejam parâmetros de query e
            // trechos de connection string no log, e os logs do App Service são
            // legíveis por qualquer pessoa com acesso ao recurso. Não dá para
            // condicionar a ASPNETCORE_ENVIRONMENT porque o ambiente na Azure
            // também roda como Development. Habilite via user secrets:
            //   dotnet user-secrets set "Database:EnableSensitiveDataLogging" "true"
            if (bool.TryParse(config["Database:EnableSensitiveDataLogging"], out var verboseSql) && verboseSql)
            {
                options.EnableSensitiveDataLogging()
                       .EnableDetailedErrors()
                       .LogTo(msg => System.Console.WriteLine(msg), Microsoft.Extensions.Logging.LogLevel.Information);
            }
        });

        // Control plane — empresas, filiais, contas e provisionamento.
        //
        // Contexto separado de propósito: precisa ser legível antes de existir
        // tenant (o login acontece antes de saber qual banco abrir) e guarda as
        // connection strings de todos os tenants. Não recebe o interceptor de
        // auditoria, que escreve em audit_logs — tabela que só existe no tenant.
        //
        // Nesta fase nada consome este contexto ainda; ele sobe às escuras.
        services.AddDbContext<ControlPlaneDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config["ControlPlaneConnectionString"]
                ?? Environment.GetEnvironmentVariable("CONTROL_PLANE_CONN_STRING")
                ?? "Host=localhost;Database=stoctable_control;Username=postgres;Password=postgres";

            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory_ControlPlane"));
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<NumberSequenceGenerator>();

        // Email (dev: loga o conteúdo; trocar por provedor real depois)
        services.AddScoped<IEmailService, LoggingEmailService>();

        return services;
    }
}
