using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Auth;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Auth;

/// <summary>
/// Mecanismo único de definição de senha por link emailado, usado tanto pelo
/// convite de novos usuários quanto pelo fluxo "esqueci minha senha".
/// O token cru vai no email; armazenamos apenas o hash no banco.
///
/// Opera sobre <see cref="Account"/>, no control plane: a senha mora lá desde
/// que o login passou a acontecer antes de saber qual banco de empresa abrir.
/// Enquanto isto apontava para a tabela do tenant, redefinir a senha gravava
/// num lugar que a autenticação não lia — o usuário trocava a senha e continuava
/// sem conseguir entrar.
/// </summary>
public class PasswordResetService(
    IAccountRepository accountRepository,
    IEmailService emailService,
    IConfiguration configuration)
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan InviteTokenLifetime = TimeSpan.FromHours(48);
    private const int MinPasswordLength = 6;

    /// <summary>
    /// Gera um token de convite na conta (em memória) e envia o email.
    /// O chamador é responsável por persistir.
    /// </summary>
    public async Task SendInviteAsync(Account account, CancellationToken ct = default)
    {
        var rawToken = AssignToken(account, InviteTokenLifetime);
        var link = BuildResetLink(rawToken);

        var body = $"""
            <p>Olá, {account.FullName}!</p>
            <p>Uma conta foi criada para você no Stoctable. Defina sua senha de acesso pelo link abaixo:</p>
            <p><a href="{link}">Definir minha senha</a></p>
            <p>Este link expira em 48 horas.</p>
            """;

        await emailService.SendAsync(account.Email, "Bem-vindo ao Stoctable — defina sua senha", body, ct);
    }

    /// <summary>
    /// Inicia o fluxo "esqueci minha senha". Sempre retorna sucesso para não revelar
    /// se o email existe (evita enumeração de usuários).
    /// </summary>
    public async Task<Result<bool>> RequestResetAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var account = await accountRepository.GetByEmailAsync(request.Email, ct);

        if (account is not null && account.IsActive)
        {
            var rawToken = AssignToken(account, ResetTokenLifetime);
            await accountRepository.SaveAsync(ct);

            var link = BuildResetLink(rawToken);
            var body = $"""
                <p>Olá, {account.FullName}!</p>
                <p>Recebemos uma solicitação para redefinir sua senha. Use o link abaixo:</p>
                <p><a href="{link}">Redefinir minha senha</a></p>
                <p>Este link expira em 1 hora. Se você não fez esta solicitação, ignore este email.</p>
                """;

            await emailService.SendAsync(account.Email, "Redefinição de senha — Stoctable", body, ct);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var account = await FindByTokenAsync(token, ct);
        return account is null
            ? Result<bool>.Failure(ErrorMessages.Auth.InvalidResetToken)
            : Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            return Result<bool>.Failure(ErrorMessages.Auth.WeakPassword);

        var account = await FindByTokenAsync(request.Token, ct);
        if (account is null)
            return Result<bool>.Failure(ErrorMessages.Auth.InvalidResetToken);

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        account.PasswordResetTokenHash = null;
        account.PasswordResetTokenExpiresAt = null;

        // Definir senha nova encerra as sessões abertas: se o motivo da
        // redefinição foi uma senha vazada, deixar o refresh token antigo valendo
        // manteria o invasor dentro.
        account.RefreshTokenHash = null;
        account.RefreshTokenExpiresAt = null;

        await accountRepository.SaveAsync(ct);

        return Result<bool>.Success(true);
    }

    private async Task<Account?> FindByTokenAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = HashToken(token);
        var account = await accountRepository.GetByPasswordResetTokenHashAsync(hash, ct);

        if (account is null || account.PasswordResetTokenExpiresAt is null
            || account.PasswordResetTokenExpiresAt < DateTimeOffset.UtcNow)
            return null;

        return account;
    }

    private static string AssignToken(Account account, TimeSpan lifetime)
    {
        var rawToken = GenerateToken();
        account.PasswordResetTokenHash = HashToken(rawToken);
        account.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.Add(lifetime);
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
