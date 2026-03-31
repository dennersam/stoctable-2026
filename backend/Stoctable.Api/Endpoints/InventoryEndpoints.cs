using System.Security.Claims;
using Stoctable.Application.Services.Inventory;

namespace Stoctable.Api.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/movements/{productId:guid}", async (Guid productId, InventoryService service, CancellationToken ct) =>
        {
            var result = await service.GetMovementsByProductAsync(productId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetInventoryMovements");

        group.MapPost("/adjust", async (AdjustStockRequest request, ClaimsPrincipal user, InventoryService service, CancellationToken ct) =>
        {
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? "system";
            var result = await service.AdjustStockAsync(request.ProductId, request.Quantity, request.Notes, username, ct);
            return result.IsSuccess
                ? Results.Created("/api/inventory/movements", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("AdjustStock");
    }
}

public record AdjustStockRequest(Guid ProductId, decimal Quantity, string Notes = "");
