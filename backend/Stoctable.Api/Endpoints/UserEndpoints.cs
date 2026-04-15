using System.Security.Claims;
using Stoctable.Application.Services.Users;
using Stoctable.Communication.Requests.Users;

namespace Stoctable.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var meGroup = app.MapGroup("/api/users/me")
            .WithTags("Users")
            .RequireAuthorization();

        meGroup.MapGet("/", async (ClaimsPrincipal principal, UserService service, CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await service.GetMeAsync(userId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetMe");

        meGroup.MapPut("/", async (UpdateProfileRequest request, ClaimsPrincipal principal, UserService service, CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await service.UpdateProfileAsync(userId, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("UpdateMe");

        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async (UserService service, CancellationToken ct) =>
        {
            var result = await service.GetAllAsync(ct);
            return Results.Ok(result.Data);
        }).WithName("GetAllUsers");

        group.MapGet("/{id:guid}", async (Guid id, UserService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetUserById");

        group.MapPost("/", async (CreateUserRequest request, UserService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/users/{result.Data!.Id}", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("CreateUser");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UserService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("UpdateUser");
    }
}
