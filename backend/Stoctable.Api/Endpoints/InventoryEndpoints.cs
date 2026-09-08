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

        // Consulta de rede: leitura, e liberada para todos os papéis — quem
        // atende o balcão precisa saber se a peça existe em outra loja.
        app.MapGet("/api/inventory/network/{productId:guid}", async (
            Guid productId, InventoryService service, CancellationToken ct) =>
        {
            var result = await service.GetNetworkStockAsync(productId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization().WithTags("Inventory").WithName("GetNetworkStock");

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

        // O mínimo é da filial, não do catálogo — por isso mora aqui e não no
        // formulário de produto, que edita entidade da empresa.
        group.MapPut("/minimum", async (SetStockMinimumRequest request, InventoryService service, CancellationToken ct) =>
        {
            var result = await service.SetMinimumAsync(request.ProductId, request.Minimum, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("SetStockMinimum");
    }
}

public record AdjustStockRequest(Guid ProductId, decimal Quantity, string Notes = "");

public record SetStockMinimumRequest(Guid ProductId, decimal Minimum);
