using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// O painel do administrador mostra margem e faturamento da loja. Quem separa os
/// perfis são as políticas no pipeline HTTP, que só um teste por HTTP alcança.
/// </summary>
[Trait("Category", "Integration")]
public class DashboardEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public DashboardEndpointTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("salesperson")]
    [InlineData("cashier")]
    [InlineData("admin")]
    public async Task WithoutToken_Is401(string painel)
    {
        var response = await _fixture.CreateClient().GetAsync($"/api/dashboard/{painel}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Atendente, "salesperson", HttpStatusCode.OK)]
    [InlineData(UserRole.Atendente, "cashier", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Atendente, "admin", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Caixa, "salesperson", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Caixa, "cashier", HttpStatusCode.OK)]
    [InlineData(UserRole.Caixa, "admin", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Admin, "salesperson", HttpStatusCode.OK)]
    [InlineData(UserRole.Admin, "cashier", HttpStatusCode.OK)]
    [InlineData(UserRole.Admin, "admin", HttpStatusCode.OK)]
    public async Task EachRoleReachesOnlyItsPanels(UserRole papel, string painel, HttpStatusCode esperado)
    {
        var conta = await CreateAccountAsync(papel);
        var token = await LoginAsync(conta.Email);

        var response = await GetAsync($"/api/dashboard/{painel}", token);

        Assert.Equal(esperado, response.StatusCode);
    }

    /// <summary>
    /// O vendedor vem da claim assinada, não de parâmetro: ninguém consulta o
    /// painel de outra pessoa mudando a URL.
    /// </summary>
    [Fact]
    public async Task Salesperson_ReturnsDataOfTheLoggedInAccount()
    {
        var eu = await CreateAccountAsync(UserRole.Atendente);
        var outro = await CreateAccountAsync(UserRole.Atendente);
        await SeedPaidSaleAsync(eu, 120m);
        await SeedPaidSaleAsync(outro, 999m);
        var token = await LoginAsync(eu.Email);

        var response = await GetAsync($"/api/dashboard/salesperson?salespersonId={outro.Id}", token);

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(120m, json.RootElement.GetProperty("today").GetProperty("revenue").GetDecimal());
    }

    private async Task<Account> CreateAccountAsync(UserRole papel)
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        await using var control = _fixture.CreateControlContext();
        var conta = new Account
        {
            CompanyId = _fixture.CompanyId,
            Email = $"{id}@teste.local",
            Username = id,
            FullName = $"Conta {id}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ApiFixture.AdminPassword, workFactor: 4),
            Role = papel,
        };
        control.Accounts.Add(conta);
        control.AccountBranches.Add(new AccountBranch { AccountId = conta.Id, BranchId = _fixture.MegaBranchId });
        await control.SaveChangesAsync();
        return conta;
    }

    private async Task SeedPaidSaleAsync(Account vendedor, decimal total)
    {
        await using var ctx = _fixture.CreateTenantContext(new BranchContext { BranchId = _fixture.MegaBranchId });
        ctx.Users.Add(new User
        {
            Id = vendedor.Id,
            Username = vendedor.Username,
            Email = vendedor.Email,
            FullName = vendedor.FullName,
            PasswordHash = "x",
            Role = vendedor.Role,
        });
        ctx.Sales.Add(new Sale
        {
            BranchId = _fixture.MegaBranchId,
            SaleNumber = $"TST{Guid.NewGuid():N}"[..14],
            SalespersonId = vendedor.Id,
            Status = SaleStatus.Paid,
            Subtotal = total,
            TotalAmount = total,
            AmountPaid = total,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _fixture.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { username = email, password = ApiFixture.AdminPassword });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _fixture.CreateClient().SendAsync(request);
    }
}
