using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stoctable.Application.Services.Auth;
using Stoctable.Communication.Requests.Auth;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Repositories;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Fase 0 do plano de SaaS: o refresh token passou a ser guardado como hash e o
/// tempo de vida do access token passou a vir de Jwt:ExpirationMinutes — antes
/// era texto puro no banco e 8 horas fixas no código.
/// </summary>
[Trait("Category", "Integration")]
public class AuthTokenTests : IClassFixture<PostgresFixture>
{
    private const string Secret = "test-secret-com-mais-de-32-caracteres-aqui";
    private const string Password = "Senha@123";

    private readonly PostgresFixture _fixture;

    public AuthTokenTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_StoresRefreshTokenHashed_NotPlaintext()
    {
        var username = await SeedUserAsync();

        var result = await BuildService(_fixture.CreateContext())
            .LoginAsync(new LoginRequest(username, Password));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var rawToken = result.Data!.RefreshToken;

        await using var verify = _fixture.CreateContext();
        var stored = await verify.Users.AsNoTracking().FirstAsync(u => u.Username == username);

        Assert.NotNull(stored.RefreshToken);
        Assert.NotEqual(rawToken, stored.RefreshToken);
        Assert.Equal(Sha256Hex(rawToken), stored.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithRawToken_Succeeds_AndRotates()
    {
        var username = await SeedUserAsync();

        var login = await BuildService(_fixture.CreateContext())
            .LoginAsync(new LoginRequest(username, Password));
        Assert.True(login.IsSuccess, login.ErrorMessage);
        var firstToken = login.Data!.RefreshToken;

        var refreshed = await BuildService(_fixture.CreateContext())
            .RefreshTokenAsync(new RefreshTokenRequest(firstToken));

        Assert.True(refreshed.IsSuccess, refreshed.ErrorMessage);
        Assert.NotEqual(firstToken, refreshed.Data!.RefreshToken);

        // O token antigo deixa de valer assim que é rotacionado.
        var reused = await BuildService(_fixture.CreateContext())
            .RefreshTokenAsync(new RefreshTokenRequest(firstToken));
        Assert.False(reused.IsSuccess);
    }

    [Fact]
    public async Task Refresh_WithHashInsteadOfRawToken_Fails()
    {
        var username = await SeedUserAsync();

        var login = await BuildService(_fixture.CreateContext())
            .LoginAsync(new LoginRequest(username, Password));
        Assert.True(login.IsSuccess, login.ErrorMessage);

        // Quem só tivesse o conteúdo da coluna (um dump do banco) não consegue
        // trocar por uma sessão — é esse o ponto de guardar o hash.
        var stolenHash = Sha256Hex(login.Data!.RefreshToken);
        var result = await BuildService(_fixture.CreateContext())
            .RefreshTokenAsync(new RefreshTokenRequest(stolenHash));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task AccessToken_HonoursConfiguredExpirationMinutes()
    {
        var username = await SeedUserAsync();

        var result = await BuildService(_fixture.CreateContext(), expirationMinutes: 15)
            .LoginAsync(new LoginRequest(username, Password));
        Assert.True(result.IsSuccess, result.ErrorMessage);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Data!.AccessToken);
        var lifetime = jwt.ValidTo - DateTime.UtcNow;

        Assert.InRange(lifetime.TotalMinutes, 13, 16);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static AuthService BuildService(StoctableDbContext ctx, int expirationMinutes = 15)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = "stoctable-api",
                ["Jwt:Audience"] = "stoctable-app",
                ["Jwt:ExpirationMinutes"] = expirationMinutes.ToString(),
                ["Jwt:RefreshTokenDays"] = "7",
            })
            .Build();

        return new AuthService(new UserRepository(ctx), config);
    }

    private async Task<string> SeedUserAsync()
    {
        await using var ctx = _fixture.CreateContext();

        var username = $"user{Guid.NewGuid():N}".Substring(0, 20);
        ctx.Users.Add(new User
        {
            Username = username,
            Email = $"{username}@test.local",
            FullName = "Usuário de Teste",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            Role = UserRole.Admin,
            IsActive = true,
        });

        await ctx.SaveChangesAsync();
        return username;
    }

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
