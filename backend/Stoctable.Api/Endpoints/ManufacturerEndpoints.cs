using Stoctable.Application.Services.Manufacturers;

namespace Stoctable.Api.Endpoints;

public static class ManufacturerEndpoints
{
    public static void MapManufacturerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/manufacturers")
            .WithTags("Manufacturers")
            .RequireAuthorization();

        // GET /api/manufacturers — lista ativos (todos os roles autenticados, para uso no form de produto)
        group.MapGet("/", async (ManufacturerService service, CancellationToken ct) =>
        {
            var result = await service.GetAllActiveAsync(ct);
            return Results.Ok(result.Data);
        }).WithName("GetActiveManufacturers");

        // GET /api/manufacturers/all — lista completa (admin)
        group.MapGet("/all", async (ManufacturerService service, CancellationToken ct) =>
        {
            var result = await service.GetAllAsync(ct);
            return Results.Ok(result.Data);
        }).WithName("GetAllManufacturers").RequireAuthorization("AdminOnly");

        group.MapGet("/{id:guid}", async (Guid id, ManufacturerService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetManufacturerById").RequireAuthorization("AdminOnly");

        group.MapPost("/", async (ManufacturerRequest request, ManufacturerService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/manufacturers/{result.Data!.Id}", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("CreateManufacturer").RequireAuthorization("AdminOnly");

        group.MapPut("/{id:guid}", async (Guid id, ManufacturerRequest request, ManufacturerService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("UpdateManufacturer").RequireAuthorization("AdminOnly");

        group.MapDelete("/{id:guid}", async (Guid id, ManufacturerService service, CancellationToken ct) =>
        {
            var result = await service.DeactivateAsync(id, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("DeactivateManufacturer").RequireAuthorization("AdminOnly");
    }
}
