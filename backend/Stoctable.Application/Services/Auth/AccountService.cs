using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Auth;
using Stoctable.Communication.Responses.Auth;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Auth;

/// <summary>
/// Autenticação contra o control plane. Substitui o AuthService, que validava
/// senha dentro do banco de uma empresa — impossível num SaaS, porque o login
/// acontece antes de saber qual banco abrir.
///
/// Emite dois formatos de token:
///
///  - PRÉ-FILIAL, curto, quando a conta tem acesso a mais de uma loja. Carrega
///    a empresa e a lista de filiais permitidas, mas nenhuma filial ativa, e o
///    escopo o impede de chamar endpoint de negócio.
///  - SESSÃO, com a filial ativa dentro. A filial deixa de ser um header que o
///    cliente escolhe e passa a ser asserção assinada: trocar de loja exige
///    passar de novo por <see cref="SelectBranchAsync"/>, que é onde o
///    pertencimento é conferido.
/// </summary>
public class AccountService(IAccountRepository accountRepository, IConfiguration configuration)
{
    public const string CompanyClaim = "company_id";
    public const string BranchClaim = "branch_id";
    public const string BranchListClaim = "branch";
    public const string ScopeClaim = "scope";
    public const string BranchSelectionScope = "branch-selection";

    private const int DefaultExpirationMinutes = 60;
    private const int DefaultRefreshTokenDays = 7;
    private const int BranchSelectionMinutes = 5;

    private int ExpirationMinutes =>
        int.TryParse(configuration["Jwt:ExpirationMinutes"], out var m) && m > 0 ? m : DefaultExpirationMinutes;

    private int RefreshTokenDays =>
        int.TryParse(configuration["Jwt:RefreshTokenDays"], out var d) && d > 0 ? d : DefaultRefreshTokenDays;

    public async Task<Result<AuthTokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // O campo continua se chamando Username no contrato para não quebrar o
        // cliente, mas o que se espera nele é o e-mail — a identidade global.
        var account = await accountRepository.GetByEmailAsync(request.Username, ct);

        if (account is null || string.IsNullOrEmpty(account.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidCredentials);

        if (!account.IsActive)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InactiveUser);

        var empresaIndisponivel = CheckCompanyAvailable(account);
        if (empresaIndisponivel is not null) return empresaIndisponivel;

        account.LastLoginAt = DateTimeOffset.UtcNow;

        return await IssueAsync(account, ct);
    }

    public async Task<Result<AuthTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidRefreshToken);

        var account = await accountRepository.GetByRefreshTokenHashAsync(HashToken(request.RefreshToken), ct);

        if (account is null || account.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidRefreshToken);

        if (!account.IsActive)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InactiveUser);

        var empresaIndisponivel = CheckCompanyAvailable(account);
        if (empresaIndisponivel is not null) return empresaIndisponivel;

        // O refresh relê as filiais do banco: é aqui que uma mudança de
        // permissão alcança uma sessão já aberta. O atraso máximo é a vida do
        // access token.
        return await IssueAsync(account, ct, request.BranchId);
    }

    /// <summary>
    /// Troca o token atual por um amarrado à filial escolhida. É o único ponto
    /// em que o pertencimento conta-filial é verificado — depois disso a filial
    /// viaja assinada e a validação é offline.
    /// </summary>
    public async Task<Result<AuthTokenResponse>> SelectBranchAsync(
        Guid accountId, SelectBranchRequest request, CancellationToken ct = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, ct);
        if (account is null || !account.IsActive)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InactiveUser);

        var empresaIndisponivel = CheckCompanyAvailable(account);
        if (empresaIndisponivel is not null) return empresaIndisponivel;

        var branches = await accountRepository.GetBranchesAsync(account.Id, ct);
        if (branches.All(b => b.Id != request.BranchId))
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.BranchNotAllowed);

        return await IssueAsync(account, ct, request.BranchId);
    }

    // ─── Emissão ────────────────────────────────────────────────────────────────

    private async Task<Result<AuthTokenResponse>> IssueAsync(
        Account account, CancellationToken ct, Guid? requestedBranchId = null)
    {
        var branches = await accountRepository.GetBranchesAsync(account.Id, ct);

        if (branches.Count == 0)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.NoBranchAssigned);

        // Uma filial só dispensa a tela de escolha; a pedida só vale se a conta
        // realmente tiver acesso — senão a sessão cai para a seleção.
        var activeBranch =
            (requestedBranchId is not null ? branches.FirstOrDefault(b => b.Id == requestedBranchId) : null)
            ?? (branches.Count == 1 ? branches[0] : null);

        var (accessToken, expiresAt) = GenerateAccessToken(account, branches, activeBranch);

        var refreshToken = GenerateRefreshToken();
        account.RefreshTokenHash = HashToken(refreshToken);
        account.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);
        await accountRepository.SaveAsync(ct);

        return Result<AuthTokenResponse>.Success(new AuthTokenResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: expiresAt,
            User: new UserResponse(
                Id: account.Id,
                Username: account.Username,
                FullName: account.FullName,
                Email: account.Email,
                Role: account.Role.ToString().ToLowerInvariant(),
                BranchIds: branches.Select(b => b.Id.ToString()).ToList(),
                AvatarUrl: account.AvatarUrl),
            Company: new CompanyResponse(
                Id: account.CompanyId,
                RazaoSocial: account.Company?.RazaoSocial ?? string.Empty,
                NomeFantasia: account.Company?.NomeFantasia,
                Cnpj: account.Company?.Cnpj ?? string.Empty),
            Branches: branches.Select(b => new BranchResponse(
                Id: b.Id,
                Code: b.Code,
                Name: b.DisplayName,
                Cnpj: b.Cnpj,
                IsHeadquarters: b.IsHeadquarters)).ToList(),
            RequiresBranchSelection: activeBranch is null,
            ActiveBranchId: activeBranch?.Id));
    }

    private (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(
        Account account, IReadOnlyList<Branch> branches, Branch? activeBranch)
    {
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured.");

        // O token de pré-filial é curto de propósito: ele só existe para
        // atravessar a tela de escolha de loja.
        var expiresAt = activeBranch is null
            ? DateTimeOffset.UtcNow.AddMinutes(BranchSelectionMinutes)
            : DateTimeOffset.UtcNow.AddMinutes(ExpirationMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.Username),
            new(ClaimTypes.Name, account.Username),
            new("fullName", account.FullName),
            new(CompanyClaim, account.CompanyId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // A lista de filiais permitidas viaja junto para que a troca de loja
        // seja validada sem ida ao banco, e para o seletor da interface montar
        // sozinho. O preço é que uma mudança de permissão só alcança a sessão
        // no próximo refresh — daí o access token ser de uma hora, não de oito.
        claims.AddRange(branches.Select(b => new Claim(BranchListClaim, b.Id.ToString())));

        if (activeBranch is null)
        {
            claims.Add(new Claim(ScopeClaim, BranchSelectionScope));
        }
        else
        {
            claims.Add(new Claim(BranchClaim, activeBranch.Id.ToString()));
            // O papel só entra no token de sessão: sem filial escolhida não há
            // endpoint de negócio a autorizar.
            claims.Add(new Claim(ClaimTypes.Role, account.Role.ToString().ToLowerInvariant()));
        }

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "stoctable",
            audience: configuration["Jwt:Audience"] ?? "stoctable",
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static Result<AuthTokenResponse>? CheckCompanyAvailable(Account account)
    {
        var status = account.Company?.Status;

        // Enquanto o ambiente está sendo criado o login não é erro de
        // credencial: o 409 é o que faz o frontend mostrar "preparando seu
        // ambiente" em vez de "usuário ou senha inválidos".
        if (status == CompanyStatus.Provisioning)
            return Result<AuthTokenResponse>.Conflict(ErrorMessages.Auth.CompanyProvisioning);

        if (status == CompanyStatus.Suspended)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.CompanySuspended);

        if (status == CompanyStatus.Failed)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.CompanyUnavailable);

        return null;
    }

    private static string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
