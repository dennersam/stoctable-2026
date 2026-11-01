using System.Security.Claims;
using Stoctable.Application.Services.Dashboard;

namespace Stoctable.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard");

        // O vendedor vem da claim assinada, nunca de parâmetro: ninguém consulta
        // o painel de outra pessoa mudando a URL.
        group.MapGet("/salesperson", async (ClaimsPrincipal user, DashboardService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var salespersonId))
                return Results.Unauthorized();

            var result = await service.GetSalespersonAsync(salespersonId, ct);
            return Results.Ok(result.Data);
        }).RequireAuthorization("SalesRep").WithName("GetSalespersonDashboard");

        group.MapGet("/cashier", async (DashboardService service, CancellationToken ct) =>
        {
            var result = await service.GetCashierAsync(ct);
            return Results.Ok(result.Data);
        }).RequireAuthorization("Cashier").WithName("GetCashierDashboard");

        group.MapGet("/admin", async (DashboardService service, CancellationToken ct) =>
        {
            var result = await service.GetAdminAsync(ct);
            return Results.Ok(result.Data);
        }).RequireAuthorization("AdminOnly").WithName("GetAdminDashboard");
    }
}
