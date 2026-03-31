using Azure.Security.KeyVault.Secrets;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Api.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    private const string BranchIdHeader = "X-Branch-Id";

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, BranchConnectionCache cache, SecretClient? secretClient)
    {
        // Skip tenant resolution for auth endpoints
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(BranchIdHeader, out var branchIdValues) ||
            string.IsNullOrWhiteSpace(branchIdValues.FirstOrDefault()))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = $"Header '{BranchIdHeader}' é obrigatório." });
            return;
        }

        var branchId = branchIdValues.First()!.Trim();
        tenantContext.BranchId = branchId;

        // Check cache first
        if (cache.TryGet(branchId, out var cachedConn))
        {
            tenantContext.ConnectionString = cachedConn;
            await next(context);
            return;
        }

        // Resolve from KeyVault
        if (secretClient is not null)
        {
            try
            {
                var secretName = $"STOCTABLE-CONN-{branchId.ToUpperInvariant()}";
                var secret = await secretClient.GetSecretAsync(secretName);
                tenantContext.ConnectionString = secret.Value.Value;
                cache.Set(branchId, secret.Value.Value);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve connection for branch {BranchId}", branchId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { error = $"Filial '{branchId}' não encontrada ou inacessível." });
                return;
            }
        }
        else
        {
            // Development fallback: use DEFAULT_CONN_STRING
            var devConn = Environment.GetEnvironmentVariable("DEFAULT_CONN_STRING")
                ?? "Host=localhost;Database=stoctable_branch_dev;Username=postgres;Password=postgres";
            tenantContext.ConnectionString = devConn;
            cache.Set(branchId, devConn);
        }

        await next(context);
    }
}
