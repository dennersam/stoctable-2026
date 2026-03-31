using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Stoctable.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "Argumento inválido."),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Operação inválida."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Acesso não autorizado."),
            _ => (HttpStatusCode.InternalServerError, "Erro interno do servidor.")
        };

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
