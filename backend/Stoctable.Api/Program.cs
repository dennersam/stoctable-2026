using System.Security.Claims;
using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Stoctable.Api.Endpoints;
using Stoctable.Api.Middleware;
using Stoctable.Application;
using Stoctable.Infrastructure;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Seeding;

// Bootstrap Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .WriteTo.Console();

        var appInsightsKey = ctx.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(appInsightsKey))
            config.WriteTo.ApplicationInsights(appInsightsKey, TelemetryConverter.Traces);
    });

    // ─── Azure KeyVault (optional — skipped in dev when KeyVault:Url is empty) ──
    var keyVaultUrl = builder.Configuration["KeyVault:Url"];
    SecretClient? secretClient = null;
    if (!string.IsNullOrEmpty(keyVaultUrl))
    {
        var credential = new DefaultAzureCredential();
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), credential);
        secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
    }

    // Register SecretClient so TenantResolutionMiddleware can receive it (null = dev mode)
    if (secretClient is not null)
        builder.Services.AddSingleton(secretClient);

    // ─── HTTP Context ────────────────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();

    // ─── Application Insights ───────────────────────────────────────────────────
    builder.Services.AddApplicationInsightsTelemetry();

    // ─── Authentication (JWT Bearer) ────────────────────────────────────────────
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret não configurado.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "stoctable",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "stoctable",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.NameIdentifier,
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", p => p.RequireRole("admin"));
        options.AddPolicy("SalesRep",  p => p.RequireRole("admin", "atendente"));
        options.AddPolicy("Cashier",   p => p.RequireRole("admin", "caixa"));
    });

    // ─── CORS ────────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
                    ?? ["http://localhost:5173", "https://localhost:5173"])
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // ─── OpenAPI / Scalar ────────────────────────────────────────────────────────
    builder.Services.AddOpenApi();

    // ─── Application + Infrastructure ───────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure();

    // ─── Build ───────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Pipeline ────────────────────────────────────────────────────────────────
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");

    // A ORDEM aqui é uma questão de segurança, não de estilo.
    //
    // A resolução de tenant precisa vir DEPOIS da autenticação, porque ela lê a
    // empresa e a filial das claims — antes disso não existe ClaimsPrincipal.
    // Enquanto rodou antes, o middleware confiava no header X-Branch-Id e
    // qualquer usuário autenticado lia os dados de outra empresa trocando um
    // cabeçalho.
    //
    // E antes da autorização, para que as policies possam contar com a filial
    // já resolvida.
    app.UseAuthentication();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthorization();

    // ─── Endpoints ───────────────────────────────────────────────────────────────
    app.MapAuthEndpoints();
    app.MapManufacturerEndpoints();
    app.MapProductEndpoints();
    app.MapCustomerEndpoints();
    app.MapQuotationEndpoints();
    app.MapSaleEndpoints();
    app.MapSupplierEndpoints();
    app.MapInventoryEndpoints();
    app.MapStockTransferEndpoints();
    app.MapUserEndpoints();
    app.MapPaymentMethodEndpoints();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
       .AllowAnonymous()
       .WithTags("Health");

    // ─── Seed (apenas em Development) ───────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StoctableDbContext>();
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Falha no seed não derruba a aplicação: uma connection string errada ou
        // um banco indisponível provocava crash no startup, reinício automático e
        // um ciclo que esgotou a cota de CPU do App Service. Registrando o erro e
        // seguindo, /health continua respondendo e o log mostra o que houve.
        try
        {
            await DbSeeder.SeedAsync(db, seedLogger);
        }
        catch (Exception seedEx)
        {
            seedLogger.LogError(seedEx,
                "Falha ao aplicar migrations/seed no startup. A API vai subir, mas os " +
                "endpoints que dependem do banco falharão até a conexão ser corrigida.");
        }
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Torna a classe gerada pelas top-level statements visível para os testes.
/// Sem isto, WebApplicationFactory&lt;Program&gt; não compila — e é ele que
/// permite exercitar o pipeline HTTP de verdade (ordem dos middlewares,
/// políticas de autorização, resolução de tenant), que nenhum teste de serviço
/// alcança.
/// </summary>
public partial class Program;
