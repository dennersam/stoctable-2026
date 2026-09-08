using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Auth;
using Stoctable.Communication.Responses.Auth;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Auth;

public class AuthService(IUserRepository userRepository, IConfiguration configuration)
{
    // Usados quando as chaves correspondentes não estão configuradas. Os valores
    // batem com o appsettings.json — antes destas constantes o access token era
    // fixo em 8 horas e o Jwt:ExpirationMinutes do appsettings nunca era lido.
    private const int DefaultExpirationMinutes = 15;
    private const int DefaultRefreshTokenDays = 7;

    private int ExpirationMinutes =>
        int.TryParse(configuration["Jwt:ExpirationMinutes"], out var m) && m > 0 ? m : DefaultExpirationMinutes;

    private int RefreshTokenDays =>
        int.TryParse(configuration["Jwt:RefreshTokenDays"], out var d) && d > 0 ? d : DefaultRefreshTokenDays;

    public async Task<Result<AuthTokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, ct);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidCredentials);

        if (!user.IsActive)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InactiveUser);

        var (accessToken, expiresAt) = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userRepository.UpdateAsync(user, ct);

        var response = new AuthTokenResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: expiresAt,
            User: new UserResponse(
                Id: user.Id,
                Username: user.Username,
                FullName: user.FullName,
                Email: user.Email,
                Role: user.Role.ToString().ToLower(),
                BranchIds: [],
                AvatarUrl: user.AvatarUrl));

        return Result<AuthTokenResponse>.Success(response);
    }

    public async Task<Result<AuthTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidRefreshToken);

        var user = await userRepository.GetByRefreshTokenAsync(HashToken(request.RefreshToken), ct);

        if (user is null || user.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidRefreshToken);

        if (!user.IsActive)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InactiveUser);

        var (accessToken, expiresAt) = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);
        await userRepository.UpdateAsync(user, ct);

        var response = new AuthTokenResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: expiresAt,
            User: new UserResponse(
                Id: user.Id,
                Username: user.Username,
                FullName: user.FullName,
                Email: user.Email,
                Role: user.Role.ToString().ToLower(),
                BranchIds: [],
                AvatarUrl: user.AvatarUrl));

        return Result<AuthTokenResponse>.Success(response);
    }

    private (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(Domain.Entities.User user)
    {
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured.");

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToLower()),
            new Claim("fullName", user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "stoctable",
            audience: configuration["Jwt:Audience"] ?? "stoctable",
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// O refresh token cru só existe na resposta HTTP; no banco fica apenas o
    /// hash. Sem isto, um dump da tabela users entrega sessões ativas prontas
    /// para uso. Mesmo esquema já usado em PasswordResetService.
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
