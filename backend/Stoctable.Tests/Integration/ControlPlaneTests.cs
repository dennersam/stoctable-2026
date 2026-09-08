using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Fase 1 do plano de SaaS. O control plane ainda não é lido por nenhum serviço,
/// então estes testes valem por duas coisas: provam que as migrations aplicam
/// numa base limpa, e travam as regras de unicidade que sustentam o modelo de
/// identidade (e-mail global, username por empresa, código de filial por empresa).
/// </summary>
[Trait("Category", "Integration")]
public class ControlPlaneTests : IClassFixture<ControlPlaneFixture>
{
    private readonly ControlPlaneFixture _fixture;

    public ControlPlaneTests(ControlPlaneFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migrations_CreateEverySchemaObject()
    {
        await using var ctx = _fixture.CreateContext();

        // Se as migrations não tivessem aplicado, cada uma destas consultas
        // estouraria com "relation does not exist". Não afirmamos contagem: o
        // container é compartilhado pela classe e outros testes já semearam.
        Assert.True(await ctx.Companies.CountAsync() >= 0);
        Assert.True(await ctx.Branches.CountAsync() >= 0);
        Assert.True(await ctx.Accounts.CountAsync() >= 0);
        Assert.True(await ctx.AccountBranches.CountAsync() >= 0);
        Assert.True(await ctx.ProvisioningJobs.CountAsync() >= 0);

        Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await ctx.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Company_WithBranchesAndAccount_RoundTrips()
    {
        var (companyId, _) = await SeedMegamotosAsync();

        await using var verify = _fixture.CreateContext();
        var company = await verify.Companies.AsNoTracking()
            .Include(c => c.Branches)
            .Include(c => c.Accounts).ThenInclude(a => a.Branches)
            .FirstAsync(c => c.Id == companyId);

        Assert.Equal(CompanyStatus.Ready, company.Status);
        Assert.Equal(3, company.Branches.Count);
        Assert.Single(company.Branches.Where(b => b.IsHeadquarters));

        // A conta administradora enxerga as três lojas.
        var account = Assert.Single(company.Accounts);
        Assert.Equal(3, account.Branches.Count);
    }

    [Fact]
    public async Task Branch_DisplayName_PrefersNomeFantasia()
    {
        var (companyId, _) = await SeedMegamotosAsync();

        await using var verify = _fixture.CreateContext();
        var branches = await verify.Branches.AsNoTracking()
            .Where(b => b.CompanyId == companyId).ToListAsync();

        Assert.Contains(branches, b => b.DisplayName == "MOTOPENHA");
        Assert.Contains(branches, b => b.DisplayName == "VILLAMOTOS");
    }

    [Fact]
    public async Task Email_IsUniqueAcrossTheWholeSaas()
    {
        var (_, email) = await SeedMegamotosAsync();
        var otherCompanyId = await SeedBareCompanyAsync();

        await using var ctx = _fixture.CreateContext();
        ctx.Accounts.Add(NewAccount(otherCompanyId, email, "outro"));

        // Mesmo sendo outra empresa: e-mail é a identidade de login do SaaS.
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Username_IsUniquePerCompany_NotGlobally()
    {
        var (megamotosId, _) = await SeedMegamotosAsync();
        var otherCompanyId = await SeedBareCompanyAsync();

        await using var ctx = _fixture.CreateContext();
        ctx.Accounts.Add(NewAccount(otherCompanyId, $"admin{Guid.NewGuid():N}@outra.local", "admin"));

        // "admin" já existe na Megamotos, mas noutra empresa é permitido.
        await ctx.SaveChangesAsync();

        var ids = new[] { megamotosId, otherCompanyId };
        Assert.Equal(2, await ctx.Accounts.CountAsync(a => a.Username == "admin" && ids.Contains(a.CompanyId)));
    }

    [Fact]
    public async Task BranchCode_IsUniquePerCompany()
    {
        var (companyId, _) = await SeedMegamotosAsync();

        await using var ctx = _fixture.CreateContext();
        ctx.Branches.Add(new Branch
        {
            CompanyId = companyId,
            Code = "PENHA",
            RazaoSocial = "Duplicata Ltda",
        });

        // O código entra no prefixo dos documentos: repetir colidiria a numeração.
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Cnpj_IsUniqueAcrossCompanies()
    {
        await using var ctx = _fixture.CreateContext();
        var cnpj = NewCnpj();

        ctx.Companies.Add(new Company { Cnpj = cnpj, RazaoSocial = "Primeira" });
        await ctx.SaveChangesAsync();

        ctx.Companies.Add(new Company { Cnpj = cnpj, RazaoSocial = "Segunda" });
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task ProvisioningJob_StatusTokenIsUnique_AndStepRoundTrips()
    {
        var companyId = await SeedBareCompanyAsync();
        var token = Guid.NewGuid().ToString("N");

        await using var ctx = _fixture.CreateContext();
        ctx.ProvisioningJobs.Add(new ProvisioningJob
        {
            CompanyId = companyId,
            StatusToken = token,
            Step = ProvisioningStep.MigrationsApplied,
            State = ProvisioningState.Running,
            Attempts = 2,
        });
        await ctx.SaveChangesAsync();

        await using var verify = _fixture.CreateContext();
        var job = await verify.ProvisioningJobs.AsNoTracking().FirstAsync(j => j.StatusToken == token);
        Assert.Equal(ProvisioningStep.MigrationsApplied, job.Step);
        Assert.Equal(ProvisioningState.Running, job.State);

        verify.ProvisioningJobs.Add(new ProvisioningJob { CompanyId = companyId, StatusToken = token });
        await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingCompany_CascadesToBranchesAccountsAndMemberships()
    {
        var (companyId, _) = await SeedMegamotosAsync();

        await using (var ctx = _fixture.CreateContext())
        {
            var company = await ctx.Companies.FirstAsync(c => c.Id == companyId);
            ctx.Companies.Remove(company);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Branches.AnyAsync(b => b.CompanyId == companyId));
        Assert.False(await verify.Accounts.AnyAsync(a => a.CompanyId == companyId));
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Reproduz o cliente real: Megamotos matriz + MOTOPENHA + VILLAMOTOS.</summary>
    private async Task<(Guid CompanyId, string AdminEmail)> SeedMegamotosAsync()
    {
        await using var ctx = _fixture.CreateContext();

        var company = new Company
        {
            Cnpj = NewCnpj(),
            RazaoSocial = "Megamotos Comercio de Motopecas Ltda",
            NomeFantasia = "Megamotos",
            Status = CompanyStatus.Ready,
            DatabaseName = "neondb",
            DatabaseProvider = "neon",
            ProvisionedAt = DateTimeOffset.UtcNow,
        };

        var matriz = new Branch
        {
            Code = "MATRIZ", RazaoSocial = "Megamotos Comercio de Motopecas Ltda",
            NomeFantasia = "Megamotos", IsHeadquarters = true, City = "São Paulo", State = "SP",
        };
        var penha = new Branch
        {
            Code = "PENHA", RazaoSocial = "Motopenha Comercio Ltda",
            NomeFantasia = "MOTOPENHA", City = "São Paulo", State = "SP",
        };
        var villa = new Branch
        {
            Code = "VILLA", RazaoSocial = "Villamotos Comercio Ltda",
            NomeFantasia = "VILLAMOTOS", City = "São Paulo", State = "SP",
        };
        company.Branches = [matriz, penha, villa];

        var email = $"admin{Guid.NewGuid():N}@megamotos.local";
        var admin = NewAccount(company.Id, email, "admin");
        company.Accounts = [admin];

        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();

        // Acesso às três lojas.
        foreach (var branch in new[] { matriz, penha, villa })
            ctx.AccountBranches.Add(new AccountBranch { AccountId = admin.Id, BranchId = branch.Id });
        await ctx.SaveChangesAsync();

        return (company.Id, email);
    }

    private async Task<Guid> SeedBareCompanyAsync()
    {
        await using var ctx = _fixture.CreateContext();
        var company = new Company { Cnpj = NewCnpj(), RazaoSocial = "Outra Empresa Ltda" };
        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();
        return company.Id;
    }

    private static Account NewAccount(Guid companyId, string email, string username) => new()
    {
        CompanyId = companyId,
        Email = email,
        Username = username,
        FullName = "Administrador",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
        Role = UserRole.Admin,
    };

    /// <summary>CNPJ fictício de 14 dígitos, só para exercitar a unicidade.</summary>
    private static string NewCnpj() => Random.Shared.NextInt64(10_000_000_000_000, 99_999_999_999_999).ToString();
}
