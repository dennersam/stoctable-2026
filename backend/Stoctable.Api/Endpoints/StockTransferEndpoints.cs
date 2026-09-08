using System.Security.Claims;
using Stoctable.Application.Services.Inventory;
using Stoctable.Communication.Requests.Inventory;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Enums;

namespace Stoctable.Api.Endpoints;

public static class StockTransferEndpoints
{
    public static void MapStockTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock-transfers")
            .WithTags("StockTransfers")
            .RequireAuthorization();

        // A listagem não separa quem pode o quê: o filtro de dupla visibilidade
        // já garante que só origem e destino enxergam o documento.
        group.MapGet("/", async (
            string? direction, string? status, StockTransferService service, CancellationToken ct) =>
        {
            var dir = string.Equals(direction, "inbound", StringComparison.OrdinalIgnoreCase)
                ? TransferDirection.Inbound
                : TransferDirection.Outbound;

            StockTransferStatus? parsed = Enum.TryParse<StockTransferStatus>(status, true, out var s) ? s : null;

            var result = await service.GetAsync(dir, parsed, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("ListStockTransfers");

        group.MapGet("/{id:guid}", async (Guid id, StockTransferService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetStockTransfer");

        group.MapPost("/", async (
            CreateStockTransferRequest request, StockTransferService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/stock-transfers/{result.Data!.Id}", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly").WithName("CreateStockTransfer");

        // Enviar tira mercadoria da loja — decisão de quem responde pelo estoque.
        group.MapPost("/{id:guid}/ship", async (
            Guid id, ClaimsPrincipal user, StockTransferService service, CancellationToken ct) =>
        {
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? "system";
            var result = await service.ShipAsync(id, username, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly").WithName("ShipStockTransfer");

        // Receber é conferência de balcão: quem está na loja quando a carga
        // chega não é necessariamente o admin.
        group.MapPost("/{id:guid}/receive", async (
            Guid id, ReceiveStockTransferRequest request, ClaimsPrincipal user,
            StockTransferService service, CancellationToken ct) =>
        {
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? "system";
            var result = await service.ReceiveAsync(id, request, username, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("ReceiveStockTransfer");

        group.MapPost("/{id:guid}/cancel", async (
            Guid id, CancelStockTransferRequest request, StockTransferService service, CancellationToken ct) =>
        {
            var result = await service.CancelAsync(id, request.CancellationReason, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly").WithName("CancelStockTransfer");
    }
}
