using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Stoctable.Application.Services.Auth;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Pipeline HTTP de ponta a ponta: middleware, ordem dos middlewares, políticas
/// de autorização e resolução de tenant.
///
/// É a camada que faltava. O bug do select-branch respondendo 403 passou por
/// 61 testes verdes porque todos chamavam serviços diretamente — o middleware
/// nunca era exercitado.
/// </summary>
[Trait("Category", "Integration")]
public class AuthPipelineTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public AuthPipelineTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    // ─── Superfície anônima ─────────────────────────────────────────────────────

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var response = await _fixture.CreateClient().GetAsync("/health");

        // Já respondeu 400 em produção: o middleware antigo exigia X-Branch-Id
        // em TUDO, inclusive no health check.
        await AssertStatusAsync(HttpStatusCode.OK, response);
    }

    /// <summary>
    /// Falha de status sem o corpo da resposta é quase inútil para depurar:
    /// "esperava 200, veio 500" não diz o que aconteceu.
    /// </summary>
    private static async Task AssertStatusAsync(HttpStatusCode esperado, HttpResponseMessage response)
    {
        if (response.StatusCode == esperado) return;

        var corpo = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Esperava {esperado}, veio {response.StatusCode}. Corpo: {corpo}");
    }

    [Fact]
    public async Task BusinessEndpoint_WithoutToken_Is401()
    {
        var response = await _fixture.CreateClient().GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithBadCredentials_Is401()
    {
        var response = await PostAsync("/api/auth/login", new { username = ApiFixture.AdminEmail, password = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Fluxo com escolha de filial ────────────────────────────────────────────

    [Fact]
    public async Task Login_WithManyBranches_ReturnsPreBranchToken()
    {
        var auth = await LoginAsync(ApiFixture.AdminEmail);

        Assert.True(auth.RequiresBranchSelection);
        Assert.Null(auth.ActiveBranchId);
        Assert.Equal(2, auth.Branches.Count);

        var jwt = ReadJwt(auth.AccessToken);
        Assert.Equal(AccountService.BranchSelectionScope, Claim(jwt, AccountService.ScopeClaim));
        Assert.Null(Claim(jwt, AccountService.BranchClaim));
    }

    [Fact]
    public async Task PreBranchToken_CannotReachBusinessEndpoints()
    {
        var auth = await LoginAsync(ApiFixture.AdminEmail);
        var response = await GetAsync("/api/products", auth.AccessToken);

        // 403 e não 401: o token é válido, só não escolheu loja ainda.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PreBranchToken_CanReachSelectBranch()
    {
        // ESTE é o teste do bug. O middleware exigia branch_id em toda
        // requisição autenticada, inclusive na que existe para escolher a
        // filial — impasse: não dava para escolher a loja porque a loja não
        // tinha sido escolhida.
        var auth = await LoginAsync(ApiFixture.AdminEmail);

        var response = await PostAsync("/api/auth/select-branch",
            new { branchId = _fixture.MegaBranchId }, auth.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SelectBranch_IssuesSessionTokenWithRole()
    {
        var session = await LoginAndSelectAsync(ApiFixture.AdminEmail, _fixture.MegaBranchId);

        Assert.False(session.RequiresBranchSelection);
        Assert.Equal(_fixture.MegaBranchId, session.ActiveBranchId);

        var jwt = ReadJwt(session.AccessToken);
        Assert.Equal(_fixture.MegaBranchId.ToString(), Claim(jwt, AccountService.BranchClaim));
        Assert.Equal(_fixture.CompanyId.ToString(), Claim(jwt, AccountService.CompanyClaim));
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == "role" || c.Type.EndsWith("/role")).Value);
    }

    [Fact]
    public async Task SessionToken_ReachesBusinessEndpoints()
    {
        var session = await LoginAndSelectAsync(ApiFixture.AdminEmail, _fixture.MegaBranchId);

        foreach (var rota in new[] { "/api/products", "/api/quotations", "/api/customers" })
        {
            var response = await GetAsync(rota, session.AccessToken);
            Assert.True(response.IsSuccessStatusCode, $"{rota} respondeu {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Login_WithSingleBranch_SkipsSelection()
    {
        var auth = await LoginAsync(ApiFixture.SingleBranchEmail);

        Assert.False(auth.RequiresBranchSelection);
        Assert.Equal(_fixture.MegaBranchId, auth.ActiveBranchId);

        // Já abre endpoint de negócio sem passar pela escolha.
        var response = await GetAsync("/api/products", auth.AccessToken);
        Assert.True(response.IsSuccessStatusCode);
    }

    // ─── Isolamento ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectBranch_OfAnotherCompany_IsRefused()
    {
        var auth = await LoginAsync(ApiFixture.AdminEmail);

        // A filial existe e está ativa — só pertence a outra empresa. Conhecer
        // o id não pode dar acesso.
        var response = await PostAsync("/api/auth/select-branch",
            new { branchId = _fixture.ForeignBranchId }, auth.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyBranchHeader_IsIgnored()
    {
        // A falha que a fase 3 fechou: antes o header escolhia a filial, então
        // trocá-lo dava acesso aos dados de outra loja.
        var (megaId, penhaId) = await SeedOneSalePerBranchAsync();

        var session = await LoginAndSelectAsync(ApiFixture.AdminEmail, _fixture.MegaBranchId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/sales?status=pending_payment");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Headers.Add("X-Branch-Id", _fixture.PenhaBranchId.ToString());

        var response = await _fixture.CreateClient().SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);

        var corpo = await response.Content.ReadAsStringAsync();

        // O token diz MEGA; o header diz PENHA. Quem vale é o token.
        Assert.Contains(megaId.ToString(), corpo);
        Assert.DoesNotContain(penhaId.ToString(), corpo);
    }

    [Fact]
    public async Task SwitchingBranch_ChangesTheDataSeen()
    {
        var (megaId, penhaId) = await SeedOneSalePerBranchAsync();

        var naMega = await LoginAndSelectAsync(ApiFixture.AdminEmail, _fixture.MegaBranchId);
        var corpoMega = await (await GetAsync("/api/sales?status=pending_payment", naMega.AccessToken))
            .Content.ReadAsStringAsync();

        var naPenha = await LoginAndSelectAsync(ApiFixture.AdminEmail, _fixture.PenhaBranchId);
        var corpoPenha = await (await GetAsync("/api/sales?status=pending_payment", naPenha.AccessToken))
            .Content.ReadAsStringAsync();

        Assert.Contains(megaId.ToString(), corpoMega);
        Assert.DoesNotContain(penhaId.ToString(), corpoMega);

        Assert.Contains(penhaId.ToString(), corpoPenha);
        Assert.DoesNotContain(megaId.ToString(), corpoPenha);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task<(Guid Mega, Guid Penha)> SeedOneSalePerBranchAsync()
    {
        var mega = await SeedSaleAsync(_fixture.MegaBranchId);
        var penha = await SeedSaleAsync(_fixture.PenhaBranchId);
        return (mega, penha);
    }

    private async Task<Guid> SeedSaleAsync(Guid branchId)
    {
        await using var ctx = _fixture.CreateTenantContext(new BranchContext { BranchId = branchId });

        var sale = new Sale
        {
            BranchId = branchId,
            SaleNumber = $"TST{Guid.NewGuid():N}".Substring(0, 14),
            Status = SaleStatus.PendingPayment,
            Subtotal = 10m,
            TotalAmount = 10m,
        };
        ctx.Sales.Add(sale);
        await ctx.SaveChangesAsync();
        return sale.Id;
    }

    private async Task<AuthPayload> LoginAsync(string email)
    {
        var response = await PostAsync("/api/auth/login", new { username = email, password = ApiFixture.AdminPassword });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthPayload>())!;
    }

    private async Task<AuthPayload> LoginAndSelectAsync(string email, Guid branchId)
    {
        var auth = await LoginAsync(email);
        if (!auth.RequiresBranchSelection && auth.ActiveBranchId == branchId) return auth;

        var response = await PostAsync("/api/auth/select-branch", new { branchId }, auth.AccessToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthPayload>())!;
    }

    private Task<HttpResponseMessage> PostAsync(string url, object body, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _fixture.CreateClient().SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _fixture.CreateClient().SendAsync(request);
    }

    private static JwtSecurityToken ReadJwt(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string? Claim(JwtSecurityToken jwt, string type)
        => jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private record AuthPayload(
        string AccessToken,
        string RefreshToken,
        bool RequiresBranchSelection,
        Guid? ActiveBranchId,
        List<BranchPayload> Branches);

    private record BranchPayload(Guid Id, string Code, string Name);
}
