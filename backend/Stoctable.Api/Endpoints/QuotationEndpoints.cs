using System.Security.Claims;
using Stoctable.Application.Services.Quotations;
using Stoctable.Communication.Requests.Quotations;
using Stoctable.Domain.Enums;

namespace Stoctable.Api.Endpoints;

public static class QuotationEndpoints
{
    public static void MapQuotationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotations")
            .WithTags("Quotations")
            .RequireAuthorization("SalesRep");

        group.MapGet("/", async (string? status, QuotationService service, CancellationToken ct) =>
        {
            var parsedStatus = Enum.TryParse<QuotationStatus>(status, ignoreCase: true, out var s) ? s : QuotationStatus.Draft;
            var result = await service.GetByStatusAsync(parsedStatus, ct);
            return Results.Ok(result.Data);
        }).WithName("GetQuotationsByStatus");

        group.MapGet("/{id:guid}", async (Guid id, QuotationService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetQuotationById");

        group.MapPost("/", async (CreateQuotationRequest request, ClaimsPrincipal user, QuotationService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirst("sub")?.Value, out var salespersonId))
                return Results.Unauthorized();

            var result = await service.CreateAsync(request, salespersonId, ct);
            return result.IsSuccess
                ? Results.Created($"/api/quotations/{result.Data!.Id}", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("CreateQuotation");

        group.MapPost("/{id:guid}/items", async (Guid id, AddQuotationItemRequest request, QuotationService service, CancellationToken ct) =>
        {
            var result = await service.AddItemAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("AddQuotationItem");

        group.MapDelete("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, QuotationService service, CancellationToken ct) =>
        {
            var result = await service.RemoveItemAsync(id, itemId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("RemoveQuotationItem");

        group.MapPost("/{id:guid}/finalize", async (Guid id, FinalizeQuotationRequest request, QuotationService service, CancellationToken ct) =>
        {
            var result = await service.FinalizeAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("FinalizeQuotation");

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelQuotationRequest request, QuotationService service, CancellationToken ct) =>
        {
            var result = await service.CancelAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("CancelQuotation");

        group.MapPost("/{id:guid}/convert", async (Guid id, ClaimsPrincipal user, QuotationService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirst("sub")?.Value, out var cashierId))
                return Results.Unauthorized();

            var result = await service.ConvertToSaleAsync(id, cashierId, ct);
            return result.IsSuccess
                ? Results.Ok(new { saleId = result.Data })
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("Cashier").WithName("ConvertQuotationToSale");
    }
}
