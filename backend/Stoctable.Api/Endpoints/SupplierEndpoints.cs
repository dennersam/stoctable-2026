using Stoctable.Application.Services.Suppliers;

namespace Stoctable.Api.Endpoints;

file record SupplierListQuery(int Page = 1, int PageSize = 20, string? Search = null);

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/suppliers")
            .WithTags("Suppliers")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async ([AsParameters] SupplierListQuery query, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.GetPagedAsync(query.Page, query.PageSize, query.Search, ct);
            return Results.Ok(result.Data);
        }).WithName("GetAllSuppliers");

        group.MapGet("/search", async (string q, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.SearchAsync(q, ct);
            return Results.Ok(result.Data);
        }).WithName("SearchSuppliers");

        group.MapGet("/{id:guid}", async (Guid id, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetSupplierById");

        group.MapPost("/", async (SupplierRequest request, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/suppliers/{result.Data!.Id}", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("CreateSupplier");

        group.MapPut("/{id:guid}", async (Guid id, SupplierRequest request, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("UpdateSupplier");

        group.MapDelete("/{id:guid}", async (Guid id, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.DeactivateAsync(id, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("DeactivateSupplier");
    }
}
