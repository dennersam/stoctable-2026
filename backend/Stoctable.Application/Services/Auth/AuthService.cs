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
    public async Task<Result<AuthTokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidCredentials);

        if (!user.IsActive)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InactiveUser);

        var (accessToken, expiresAt) = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
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
                BranchIds: []));

        return Result<AuthTokenResponse>.Success(response);
    }

    public async Task<Result<AuthTokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByRefreshTokenAsync(request.RefreshToken, ct);

        if (user is null || user.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InvalidRefreshToken);

        if (!user.IsActive)
            return Result<AuthTokenResponse>.Unauthorized(ErrorMessages.Auth.InactiveUser);

        var (accessToken, expiresAt) = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
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
                BranchIds: []));

        return Result<AuthTokenResponse>.Success(response);
    }

    private (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(Domain.Entities.User user)
    {
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured.");

        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
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
}
