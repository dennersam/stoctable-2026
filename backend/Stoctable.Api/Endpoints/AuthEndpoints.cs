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
    }
}
