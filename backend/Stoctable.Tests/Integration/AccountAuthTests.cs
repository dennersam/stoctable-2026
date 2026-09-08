using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stoctable.Application.Services.Auth;
using Stoctable.Communication.Requests.Auth;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Autenticação contra o control plane (fase 3). Cobre o que o modelo de
/// identidade novo promete: e-mail como identidade global, filial dentro do
/// token assinado em vez de header, e a impossibilidade de escolher uma loja
/// que a conta não tem.
/// </summary>
[Trait("Category", "Integration")]
public class AccountAuthTests : IClassFixture<ControlPlaneFixture>
{
    private const string Secret = "test-secret-com-mais-de-32-caracteres-aqui";
    private const string Password = "Senha@123";

    private readonly ControlPlaneFixture _fixture;

    public AccountAuthTests(ControlPlaneFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithSingleBranch_IssuesSessionTokenDirectly()
    {
        var (email, branchIds) = await SeedAsync(branchCount: 1);

        var result = await BuildService().LoginAsync(new LoginRequest(email, Password));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(result.Data!.RequiresBranchSelection);
        Assert.Equal(branchIds[0], result.Data.ActiveBranchId);

        var jwt = Read(result.Data.AccessToken);
        Assert.Equal(branchIds[0].ToString(), jwt.Claims.First(c => c.Type == AccountService.BranchClaim).Value);
        // Uma loja só dispensa a tela de escolha, então o papel já vem no token.
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");
    }

    [Fact]
    public async Task Login_WithManyBranches_RequiresSelection_AndTokenCarriesNoRole()
    {
        var (email, branchIds) = await SeedAsync(branchCount: 3);

        var result = await BuildService().LoginAsync(new LoginRequest(email, Password));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(result.Data!.RequiresBranchSelection);
        Assert.Null(result.Data.ActiveBranchId);
        Assert.Equal(3, result.Data.Branches.Count);

        var jwt = Read(result.Data.AccessToken);
        Assert.Equal(AccountService.BranchSelectionScope,
            jwt.Claims.First(c => c.Type == AccountService.ScopeClaim).Value);

        // Sem papel e sem filial ativa: este token não abre endpoint de negócio.
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Role);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == AccountService.BranchClaim);

        // Mas carrega as lojas permitidas, para o seletor montar sem outra ida ao banco.
        var permitidas = jwt.Claims.Where(c => c.Type == AccountService.BranchListClaim).Select(c => c.Value).ToList();
        Assert.Equal(3, permitidas.Count);
        Assert.All(branchIds, id => Assert.Contains(id.ToString(), permitidas));
    }

    [Fact]
    public async Task SelectBranch_WithAllowedBranch_IssuesSessionToken()
    {
        var (email, branchIds) = await SeedAsync(branchCount: 3);
        var service = BuildService();

        var login = await service.LoginAsync(new LoginRequest(email, Password));
        var accountId = login.Data!.User.Id;

        var result = await service.SelectBranchAsync(accountId, new SelectBranchRequest(branchIds[2]));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(result.Data!.RequiresBranchSelection);
        Assert.Equal(branchIds[2], result.Data.ActiveBranchId);

        var jwt = Read(result.Data.AccessToken);
        Assert.Equal(branchIds[2].ToString(), jwt.Claims.First(c => c.Type == AccountService.BranchClaim).Value);
    }

    [Fact]
    public async Task SelectBranch_WithForeignBranch_IsRefused()
    {
        var (email, _) = await SeedAsync(branchCount: 2);
        var (_, outrasFiliais) = await SeedAsync(branchCount: 1);

        var service = BuildService();
        var login = await service.LoginAsync(new LoginRequest(email, Password));

        // A filial existe e está ativa — só não é desta conta. É esta checagem
        // que impede uma empresa de ler os dados de outra.
        var result = await service.SelectBranchAsync(
            login.Data!.User.Id, new SelectBranchRequest(outrasFiliais[0]));

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Fails()
    {
        var (email, _) = await SeedAsync(branchCount: 1);

        var result = await BuildService().LoginAsync(new LoginRequest(email, "senha-errada"));

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        var (email, _) = await SeedAsync(branchCount: 1);

        var result = await BuildService().LoginAsync(new LoginRequest(email.ToUpperInvariant(), Password));

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WhileCompanyIsProvisioning_Returns409()
    {
        var (email, _) = await SeedAsync(branchCount: 1, status: CompanyStatus.Provisioning);

        var result = await BuildService().LoginAsync(new LoginRequest(email, Password));

        // 409 e não 401: é isso que faz o frontend mostrar "preparando seu
        // ambiente" em vez de "usuário ou senha inválidos".
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Login_WhileCompanySuspended_IsRefused()
    {
        var (email, _) = await SeedAsync(branchCount: 1, status: CompanyStatus.Suspended);

        var result = await BuildService().LoginAsync(new LoginRequest(email, Password));

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Refresh_StoresHashOnly_AndKeepsActiveBranch()
    {
        var (email, branchIds) = await SeedAsync(branchCount: 2);
        var service = BuildService();

        var login = await service.LoginAsync(new LoginRequest(email, Password));
        var selected = await service.SelectBranchAsync(
            login.Data!.User.Id, new SelectBranchRequest(branchIds[1]));

        var rawToken = selected.Data!.RefreshToken;

        await using (var verify = _fixture.CreateContext())
        {
            var account = await verify.Accounts.AsNoTracking().FirstAsync(a => a.Email == email);
            Assert.NotEqual(rawToken, account.RefreshTokenHash);
            Assert.Equal(Sha256Hex(rawToken), account.RefreshTokenHash);
        }

        // Sem informar a filial, uma conta com duas lojas voltaria à tela de
        // escolha a cada renovação — por isso o refresh a carrega.
        var refreshed = await BuildService().RefreshTokenAsync(
            new RefreshTokenRequest(rawToken, branchIds[1]));

        Assert.True(refreshed.IsSuccess, refreshed.ErrorMessage);
        Assert.Equal(branchIds[1], refreshed.Data!.ActiveBranchId);
        Assert.NotEqual(rawToken, refreshed.Data.RefreshToken);
    }

    [Fact]
    public async Task Refresh_CannotRestoreABranchTheAccountLost()
    {
        var (email, branchIds) = await SeedAsync(branchCount: 2);
        var service = BuildService();

        var login = await service.LoginAsync(new LoginRequest(email, Password));
        var selected = await service.SelectBranchAsync(
            login.Data!.User.Id, new SelectBranchRequest(branchIds[1]));

        // Administração remove o acesso àquela loja.
        await using (var ctx = _fixture.CreateContext())
        {
            var vinculo = await ctx.AccountBranches
                .FirstAsync(ab => ab.AccountId == login.Data.User.Id && ab.BranchId == branchIds[1]);
            ctx.AccountBranches.Remove(vinculo);
            await ctx.SaveChangesAsync();
        }

        var refreshed = await BuildService().RefreshTokenAsync(
            new RefreshTokenRequest(selected.Data!.RefreshToken, branchIds[1]));

        // O refresh relê as permissões: a sessão cai para a seleção em vez de
        // renovar o acesso a uma loja que a conta não tem mais.
        Assert.True(refreshed.IsSuccess, refreshed.ErrorMessage);
        Assert.NotEqual(branchIds[1], refreshed.Data!.ActiveBranchId);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private AccountService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = "stoctable-api",
                ["Jwt:Audience"] = "stoctable-app",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "7",
            })
            .Build();

        return new AccountService(new AccountRepository(_fixture.CreateContext()), config);
    }

    private async Task<(string Email, List<Guid> BranchIds)> SeedAsync(
        int branchCount, CompanyStatus status = CompanyStatus.Ready)
    {
        await using var ctx = _fixture.CreateContext();

        var company = new Company
        {
            Cnpj = Random.Shared.NextInt64(10_000_000_000_000, 99_999_999_999_999).ToString(),
            RazaoSocial = "Empresa de Teste Ltda",
            NomeFantasia = "Teste",
            Status = status,
        };

        for (var i = 0; i < branchCount; i++)
        {
            company.Branches.Add(new Branch
            {
                Code = $"L{i}{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant(),
                RazaoSocial = $"Loja {i} Ltda",
                NomeFantasia = $"Loja {i}",
                IsHeadquarters = i == 0,
            });
        }

        var email = $"user{Guid.NewGuid():N}@teste.local";
        var account = new Account
        {
            CompanyId = company.Id,
            Email = email,
            Username = $"user{Guid.NewGuid():N}".Substring(0, 16),
            FullName = "Usuário de Teste",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            Role = UserRole.Admin,
        };
        company.Accounts = [account];

        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();

        foreach (var branch in company.Branches)
            ctx.AccountBranches.Add(new AccountBranch { AccountId = account.Id, BranchId = branch.Id });
        await ctx.SaveChangesAsync();

        // Ordem igual à do repositório: matriz primeiro, depois por código.
        var ids = company.Branches
            .OrderByDescending(b => b.IsHeadquarters)
            .ThenBy(b => b.Code)
            .Select(b => b.Id)
            .ToList();

        return (email, ids);
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
