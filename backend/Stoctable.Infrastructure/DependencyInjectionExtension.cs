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
        // Tenancy — caches em memória são singleton
        services.AddSingleton<BranchConnectionCache>();
        services.AddSingleton<CompanyConnectionCache>();
        services.AddSingleton<IConnectionStringProtector, ConnectionStringProtector>();
        services.AddScoped<ICompanyConnectionResolver, CompanyConnectionResolver>();
        services.AddScoped<TenantContext>();

        // Filial ativa da requisição. Hoje sempre a mesma (cada banco tem uma
        // filial só); na fase 3 passa a ser populado pela claim assinada do JWT.
        services.AddScoped<BranchContext>();
        services.AddScoped<ITenantConnectionProvider, TenantConnectionProvider>();

        // Audit interceptor
        services.AddScoped<AuditSaveChangesInterceptor>();

        // Carimba branch_id nas entidades novas de escopo de filial.
        services.AddScoped<BranchScopeSaveChangesInterceptor>();

        // DbContext com connection string resolvida dinamicamente via TenantContext
        services.AddDbContext<StoctableDbContext>((sp, options) =>
        {
            var tenantContext = sp.GetRequiredService<TenantContext>();
            var auditInterceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            var branchInterceptor = sp.GetRequiredService<BranchScopeSaveChangesInterceptor>();
            var config = sp.GetRequiredService<IConfiguration>();

            // A connection string vem do tenant resolvido pelas claims. O
            // fallback que existia aqui — cair no DefaultBranchConnectionString
            // quando o tenant não estava resolvido — era um vazamento à espera
            // de acontecer: qualquer caminho que escapasse do middleware
            // passaria a ler o banco de uma empresa qualquer sem erro nenhum.
            //
            // Agora só sobra o fallback de desenvolvimento local, e apenas
            // porque migrations e ferramentas de linha de comando constroem o
            // contexto fora de uma requisição HTTP.
            var connectionString = tenantContext.IsResolved
                ? tenantContext.ConnectionString!
                : config["DefaultBranchConnectionString"]
                  ?? Environment.GetEnvironmentVariable("DEFAULT_CONN_STRING")
                  ?? throw new InvalidOperationException(
                      "Nenhum tenant resolvido e nenhuma connection string padrão configurada. "
                      + "Em requisição autenticada isso indica que o TenantResolutionMiddleware "
                      + "não rodou ou não encontrou as claims de empresa e filial.");

            // A ORDEM importa: o interceptor de auditoria CRIA linhas de
            // AuditLog, e o de filial precisa rodar depois para carimbá-las
            // também. Invertido, todo registro de auditoria nasceria sem filial.
            options.UseNpgsql(connectionString)
                   .AddInterceptors(auditInterceptor, branchInterceptor);

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

        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<IUserProjectionWriter, UserProjectionWriter>();

        // Repositories
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IManufacturerRepository, ManufacturerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductStockRepository, ProductStockRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<NumberSequenceGenerator>();

        // Email (dev: loga o conteúdo; trocar por provedor real depois)
        services.AddScoped<IEmailService, LoggingEmailService>();

        return services;
    }
}
