using System.Security.Claims;
using Stoctable.Application.Services.Customers;
using Stoctable.Communication.Requests.Customers;

namespace Stoctable.Api.Endpoints;

file record CustomerListQuery(int Page = 1, int PageSize = 20, string? Search = null);

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization("SalesRep");

        group.MapGet("/", async (
            [AsParameters] CustomerListQuery query,
            CustomerService service, CancellationToken ct) =>
        {
            var result = await service.GetPagedAsync(query.Page, query.PageSize, query.Search, ct);
            return Results.Ok(result.Data);
        }).WithName("GetAllCustomers");

        group.MapGet("/search", async (string q, CustomerService service, CancellationToken ct) =>
        {
            var result = await service.SearchAsync(q, ct);
            return Results.Ok(result.Data);
        }).WithName("SearchCustomers");

        group.MapGet("/{id:guid}", async (Guid id, CustomerService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetCustomerById");

        group.MapGet("/{id:guid}/crm", async (Guid id, CustomerService service, CancellationToken ct) =>
        {
            var result = await service.GetCrmNotesAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("GetCustomerCrmNotes");

        group.MapPost("/", async (CreateCustomerRequest request, CustomerService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/customers/{result.Data!.Id}", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("CreateCustomer");

        group.MapPut("/{id:guid}", async (Guid id, CreateCustomerRequest request, CustomerService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("UpdateCustomer");

        group.MapPost("/{id:guid}/crm", async (Guid id, AddCrmNoteRequest request, ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
        {
            var username = user.FindFirst(ClaimTypes.Name)?.Value ?? "system";
            var result = await service.AddCrmNoteAsync(id, request.Note, request.NoteType, username, ct);
            return result.IsSuccess
                ? Results.Created($"/api/customers/{id}/crm", result.Data)
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).WithName("AddCrmNote");

        group.MapDelete("/{id:guid}", async (Guid id, CustomerService service, CancellationToken ct) =>
        {
            var result = await service.DeactivateAsync(id, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(result.ErrorMessage, statusCode: result.StatusCode);
        }).RequireAuthorization("AdminOnly").WithName("DeactivateCustomer");
    }
}

public record AddCrmNoteRequest(string Note, string NoteType = "general");
