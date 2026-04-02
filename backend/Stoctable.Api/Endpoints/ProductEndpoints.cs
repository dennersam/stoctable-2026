using Stoctable.Application.Services.Products;
using Stoctable.Communication.Requests.Products;

namespace Stoctable.Api.Endpoints;

file record ProductListQuery(int Page = 1, int PageSize = 20, string? Search = null);

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapGet("/", async (
            [AsParameters] ProductListQuery query,
            ProductService service, CancellationToken ct) =>
        {
            var result = await service.GetPagedAsync(query.Page, query.PageSize, query.Search, ct);
            return Results.Ok(result.Data);
        }).WithName("GetAllProducts");

        group.MapGet("/search", async (string q, ProductService service, CancellationToken ct) =>
        {
            var result = await service.SearchAsync(q, ct);
            return Results.Ok(result.Data);
        }).WithName("SearchProducts");

        group.MapGet("/low-stock", async (ProductService service, CancellationToken ct) =>
        {
            var result = await service.GetLowStockAsync(ct);
            return Results.Ok(result.Data);
        }).RequireAuthorization("AdminOnly").WithName("GetLowStockProducts");

        group.MapGet("/{id:guid}", async (Guid id, ProductService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetProductById");

        group.MapPost("/", async (CreateProductRequest request, ProductService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/products/{result.Data!.Id}", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly").WithName("CreateProduct");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, ProductService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly").WithName("UpdateProduct");

        group.MapDelete("/{id:guid}", async (Guid id, ProductService service, CancellationToken ct) =>
        {
            var result = await service.DeactivateAsync(id, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly").WithName("DeactivateProduct");

        group.MapPost("/{id:guid}/image", async (Guid id, IFormFile file, ProductService service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await service.UploadImageAsync(id, stream, file.ContentType, file.FileName, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly")
          .DisableAntiforgery()
          .WithName("UploadProductImage");
    }
}
