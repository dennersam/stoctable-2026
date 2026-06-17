using Stoctable.Application.Services.Auth;
using Stoctable.Communication.Requests.Auth;

namespace Stoctable.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, AuthService service, CancellationToken ct) =>
        {
            var result = await service.LoginAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        })
        .AllowAnonymous()
        .WithName("Login");

        group.MapPost("/refresh", async (RefreshTokenRequest request, AuthService service, CancellationToken ct) =>
        {
            var result = await service.RefreshTokenAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        })
        .AllowAnonymous()
        .WithName("RefreshToken");

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, PasswordResetService service, CancellationToken ct) =>
        {
            await service.RequestResetAsync(request, ct);
            // Resposta neutra — não revela se o email existe.
            return Results.Ok(new { message = "Se o email estiver cadastrado, enviaremos um link de redefinição." });
        })
        .AllowAnonymous()
        .WithName("ForgotPassword");

        group.MapGet("/reset-password/validate", async (string token, PasswordResetService service, CancellationToken ct) =>
        {
            var result = await service.ValidateTokenAsync(token, ct);
            return result.IsSuccess
                ? Results.Ok(new { valid = true })
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        })
        .AllowAnonymous()
        .WithName("ValidateResetToken");

        group.MapPost("/reset-password", async (ResetPasswordRequest request, PasswordResetService service, CancellationToken ct) =>
        {
            var result = await service.ResetPasswordAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(new { message = "Senha definida com sucesso." })
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        })
        .AllowAnonymous()
        .WithName("ResetPassword");
    }
}
