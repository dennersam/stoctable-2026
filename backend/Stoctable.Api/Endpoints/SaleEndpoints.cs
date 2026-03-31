using Stoctable.Application.Services.Sales;
using Stoctable.Communication.Requests.Sales;
using Stoctable.Domain.Enums;

namespace Stoctable.Api.Endpoints;

public static class SaleEndpoints
{
    public static void MapSaleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization("Cashier");

        group.MapGet("/", async (string? status, SaleService service, CancellationToken ct) =>
        {
            var parsedStatus = Enum.TryParse<SaleStatus>(status, ignoreCase: true, out var s) ? s : SaleStatus.PendingPayment;
            var result = await service.GetByStatusAsync(parsedStatus, ct);
            return Results.Ok(result.Data);
        }).WithName("GetSalesByStatus");

        group.MapGet("/{id:guid}", async (Guid id, SaleService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetSaleById");

        group.MapPost("/{id:guid}/payment", async (Guid id, ProcessPaymentRequest request, SaleService service, CancellationToken ct) =>
        {
            var result = await service.ProcessPaymentAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("ProcessPayment");
    }
}
