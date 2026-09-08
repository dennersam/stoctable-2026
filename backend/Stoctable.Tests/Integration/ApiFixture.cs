using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Search;
using Stoctable.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Sobe a API de verdade — pipeline HTTP inteiro — contra dois Postgres
/// efêmeros: o control plane e o banco de uma empresa.
///
/// Existe porque o resto da suíte chama serviços diretamente, e assim
/// middleware, ORDEM dos middlewares, políticas de autorização e resolução de
/// tenant ficavam sem cobertura nenhuma. Foi exatamente aí que morreu o bug em
/// que /api/auth/select-branch respondia 403: o middleware exigia filial numa
/// requisição cujo propósito é escolher a filial. Nenhum teste de serviço
/// alcançaria isso — só um que atravesse HTTP.
/// </summary>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@teste.local";
    public const string AdminPassword = "Admin@123";
    public const string SingleBranchEmail = "unica@teste.local";

    private const string JwtSecret = "segredo-de-teste-com-mais-de-32-caracteres";

    private readonly PostgreSqlContainer _controlDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("control_test")
        .WithUsername("postgres").WithPassword("postgres").Build();

    private readonly PostgreSqlContainer _tenantDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("tenant_test")
        .WithUsername("postgres").WithPassword("postgres").Build();

    /// <summary>Chave AES-GCM usada tanto pelo seed quanto pela aplicação.</summary>
    private readonly string _encryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public Guid CompanyId { get; private set; }
    public Guid MegaBranchId { get; private set; }
    public Guid PenhaBranchId { get; private set; }

    /// <summary>Filial de OUTRA empresa — nenhuma conta daqui pode acessá-la.</summary>
    public Guid ForeignBranchId { get; private set; }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_controlDb.StartAsync(), _tenantDb.StartAsync());

        await using (var tenant = CreateTenantContext())
        {
            await tenant.Database.EnsureCreatedAsync();
            await tenant.Database.ExecuteSqlRawAsync(SearchSchema.Up);
        }

        await using (var control = CreateControlContext())
        {
            await control.Database.MigrateAsync();
            await SeedControlPlaneAsync(control);
        }

        ApplyConfigurationViaEnvironment();

        // Força a construção do host agora, para que uma falha de startup
        // apareça aqui e não no meio do primeiro teste.
        _ = Server;
    }

    /// <summary>
    /// A configuração vai por variável de ambiente, e não só pelo
    /// ConfigureAppConfiguration do WebApplicationFactory.
    ///
    /// O Program.cs lê Jwt:Secret e KeyVault:Url do builder ANTES de chamar
    /// Build(), e os hooks do WebApplicationFactory só são aplicados no Build.
    /// Injetar por lá chega tarde: a aplicação já leu a string vazia do
    /// appsettings e sobe com uma chave de assinatura de tamanho zero, o que
    /// derruba TODA requisição com 500 — inclusive o /health.
    ///
    /// Variável de ambiente funciona porque WebApplication.CreateBuilder já
    /// inclui esse provedor por padrão, antes de qualquer leitura.
    /// </summary>
    private void ApplyConfigurationViaEnvironment()
    {
        foreach (var (chave, valor) in Settings())
            Environment.SetEnvironmentVariable(chave.Replace(":", "__"), valor);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_controlDb.DisposeAsync().AsTask(), _tenantDb.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" e não "Development": em Development o Program roda o
        // DbSeeder no startup, que aplicaria migrations num banco que o fixture
        // já preparou do seu jeito.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings()));
    }

    private Dictionary<string, string?> Settings() => new()
    {
        ["Jwt:Secret"] = JwtSecret,
        ["Jwt:Issuer"] = "stoctable-api",
        ["Jwt:Audience"] = "stoctable-app",
        ["Jwt:ExpirationMinutes"] = "60",
        ["Jwt:RefreshTokenDays"] = "7",
        ["KeyVault:Url"] = "",
        ["ControlPlaneConnectionString"] = _controlDb.GetConnectionString(),
        ["DefaultBranchConnectionString"] = _tenantDb.GetConnectionString(),
        ["TenantConnectionEncryptionKey"] = _encryptionKey,
    };

    public ControlPlaneDbContext CreateControlContext()
    {
        var opts = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(_controlDb.GetConnectionString(), npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory_ControlPlane"))
            .Options;
        return new ControlPlaneDbContext(opts);
    }

    public StoctableDbContext CreateTenantContext(BranchContext? branch = null)
    {
        var opts = new DbContextOptionsBuilder<StoctableDbContext>()
            .UseNpgsql(_tenantDb.GetConnectionString())
            .Options;
        return new StoctableDbContext(opts, branch);
    }

    private async Task SeedControlPlaneAsync(ControlPlaneDbContext control)
    {
        var protector = new ConnectionStringProtector(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenantConnectionEncryptionKey"] = _encryptionKey,
                })
                .Build());

        var company = new Company
        {
            Cnpj = "10800462000147",
            RazaoSocial = "Empresa de Teste Ltda",
            NomeFantasia = "TESTE",
            Status = CompanyStatus.Ready,
            DatabaseName = "tenant_test",
            // É isto que o CompanyConnectionResolver vai decifrar para abrir o
            // banco: o caminho real, não um atalho de teste.
            ConnectionStringEncrypted = protector.Protect(_tenantDb.GetConnectionString()),
        };

        var mega = new Branch { Code = "MEGA", RazaoSocial = "Matriz Ltda", NomeFantasia = "MEGA", IsHeadquarters = true };
        var penha = new Branch { Code = "PENHA", RazaoSocial = "Penha Ltda", NomeFantasia = "PENHA" };
        company.Branches = [mega, penha];

        var admin = NewAccount(company.Id, AdminEmail, "admin");
        var unica = NewAccount(company.Id, SingleBranchEmail, "unica");
        company.Accounts = [admin, unica];

        // Segunda empresa: dá uma filial legítima que ninguém da primeira pode
        // acessar. Sem ela, "filial proibida" seria só um GUID inexistente, e o
        // teste não distinguiria "não existe" de "não é sua".
        var outra = new Company
        {
            Cnpj = "30914869000102",
            RazaoSocial = "Outra Empresa Ltda",
            Status = CompanyStatus.Ready,
        };
        var outraFilial = new Branch { Code = "OUTRA", RazaoSocial = "Outra Ltda", IsHeadquarters = true };
        outra.Branches = [outraFilial];

        control.Companies.AddRange(company, outra);
        await control.SaveChangesAsync();

        // admin acessa as duas lojas; "unica" acessa só a matriz — é o que
        // separa o fluxo com escolha de filial do fluxo sem.
        control.AccountBranches.AddRange(
            new AccountBranch { AccountId = admin.Id, BranchId = mega.Id },
            new AccountBranch { AccountId = admin.Id, BranchId = penha.Id },
            new AccountBranch { AccountId = unica.Id, BranchId = mega.Id });
        await control.SaveChangesAsync();

        CompanyId = company.Id;
        MegaBranchId = mega.Id;
        PenhaBranchId = penha.Id;
        ForeignBranchId = outraFilial.Id;
    }

    private static Account NewAccount(Guid companyId, string email, string username) => new()
    {
        CompanyId = companyId,
        Email = email,
        Username = username,
        FullName = "Usuário de Teste",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
        Role = UserRole.Admin,
    };
}
