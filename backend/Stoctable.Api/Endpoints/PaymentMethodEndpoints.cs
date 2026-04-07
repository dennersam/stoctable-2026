using Stoctable.Application.Services.PaymentMethods;

namespace Stoctable.Api.Endpoints;

public static class PaymentMethodEndpoints
{
    public static void MapPaymentMethodEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payment-methods")
            .WithTags("PaymentMethods")
            .RequireAuthorization();

        group.MapGet("/", async (PaymentMethodService service, CancellationToken ct) =>
        {
            var result = await service.GetActiveAsync(ct);
            return Results.Ok(result.Data);
        }).WithName("GetActivePaymentMethods");
    }
}
