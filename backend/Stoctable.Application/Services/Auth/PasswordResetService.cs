using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Auth;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Entities;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Auth;

/// <summary>
/// Mecanismo único de definição de senha por link emailado, usado tanto pelo
/// convite de novos usuários quanto pelo fluxo "esqueci minha senha".
/// O token cru vai no email; armazenamos apenas o hash no banco.
/// </summary>
public class PasswordResetService(
    IUserRepository userRepository,
    IEmailService emailService,
    IConfiguration configuration)
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan InviteTokenLifetime = TimeSpan.FromHours(48);
    private const int MinPasswordLength = 6;

    /// <summary>
    /// Gera um token de convite no usuário (em memória) e envia o email de convite.
    /// O chamador é responsável por persistir o usuário.
    /// </summary>
    public async Task SendInviteAsync(User user, CancellationToken ct = default)
    {
        var rawToken = AssignToken(user, InviteTokenLifetime);
        var link = BuildResetLink(rawToken);

        var body = $"""
            <p>Olá, {user.FullName}!</p>
            <p>Uma conta foi criada para você no Stoctable. Defina sua senha de acesso pelo link abaixo:</p>
            <p><a href="{link}">Definir minha senha</a></p>
            <p>Este link expira em 48 horas.</p>
            """;

        await emailService.SendAsync(user.Email, "Bem-vindo ao Stoctable — defina sua senha", body, ct);
    }

    /// <summary>
    /// Inicia o fluxo "esqueci minha senha". Sempre retorna sucesso para não revelar
    /// se o email existe (evita enumeração de usuários).
    /// </summary>
    public async Task<Result<bool>> RequestResetAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);

        if (user is not null && user.IsActive)
        {
            var rawToken = AssignToken(user, ResetTokenLifetime);
            await userRepository.UpdateAsync(user, ct);

            var link = BuildResetLink(rawToken);
            var body = $"""
                <p>Olá, {user.FullName}!</p>
                <p>Recebemos uma solicitação para redefinir sua senha. Use o link abaixo:</p>
                <p><a href="{link}">Redefinir minha senha</a></p>
                <p>Este link expira em 1 hora. Se você não fez esta solicitação, ignore este email.</p>
                """;

            await emailService.SendAsync(user.Email, "Redefinição de senha — Stoctable", body, ct);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var user = await FindByTokenAsync(token, ct);
        return user is null
            ? Result<bool>.Failure(ErrorMessages.Auth.InvalidResetToken)
            : Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            return Result<bool>.Failure(ErrorMessages.Auth.WeakPassword);

        var user = await FindByTokenAsync(request.Token, ct);
        if (user is null)
            return Result<bool>.Failure(ErrorMessages.Auth.InvalidResetToken);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        await userRepository.UpdateAsync(user, ct);

        return Result<bool>.Success(true);
    }

    private async Task<User?> FindByTokenAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = HashToken(token);
        var user = await userRepository.GetByPasswordResetTokenHashAsync(hash, ct);

        if (user is null || user.PasswordResetTokenExpiresAt is null
            || user.PasswordResetTokenExpiresAt < DateTimeOffset.UtcNow)
            return null;

        return user;
    }

    private static string AssignToken(User user, TimeSpan lifetime)
    {
        var rawToken = GenerateToken();
        user.PasswordResetTokenHash = HashToken(rawToken);
        user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        return rawToken;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private string BuildResetLink(string rawToken)
    {
        var baseUrl = (configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
    }
}
