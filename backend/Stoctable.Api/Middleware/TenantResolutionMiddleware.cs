using System.Security.Claims;
using Stoctable.Application.Services.Auth;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Api.Middleware;

/// <summary>
/// Traduz as claims assinadas do JWT em conexão e escopo de filial.
///
/// Antes esta classe lia o header <c>X-Branch-Id</c> e confiava nele, e ainda
/// por cima rodava ANTES do UseAuthentication — sem ClaimsPrincipal, não havia
/// como conferir nada. Qualquer usuário autenticado lia os dados de outra
/// empresa trocando um header. Agora:
///
///  - roda DEPOIS da autenticação, então as claims existem;
///  - a empresa e a filial saem exclusivamente do token, que é assinado;
///  - o header, se vier, é ignorado e registrado — divergência é sinal de
///    tentativa de acesso indevido, não de configuração.
///
/// O middleware não decide autorização: quem confere se a conta pertence à
/// filial é o AccountService, no momento de emitir o token.
/// </summary>
public class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    private const string LegacyBranchHeader = "X-Branch-Id";
    private const string AuthPathPrefix = "/api/auth";

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        BranchContext branchContext,
        ICompanyConnectionResolver connectionResolver)
    {
        // Os endpoints de autenticação nunca tocam banco de empresa — trabalham
        // só no control plane. Precisam ser desviados ANTES da checagem de
        // filial: /api/auth/select-branch chega autenticado e sem filial (é
        // exatamente o que ele existe para resolver), e sem este desvio o
        // middleware o recusaria com 403, criando um impasse em que a pessoa
        // não consegue escolher a loja porque não escolheu a loja.
        if (context.Request.Path.StartsWithSegments(AuthPathPrefix))
        {
            await next(context);
            return;
        }

        // Requisição anônima (portal, /health, OpenAPI) segue sem tenant
        // resolvido. Não há fallback para um banco padrão: contexto não
        // resolvido tem de falhar alto, e não vazar silenciosamente para o
        // banco de alguma empresa.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var companyId = context.User.FindFirstValue(AccountService.CompanyClaim);
        var branchId = context.User.FindFirstValue(AccountService.BranchClaim);

        if (!Guid.TryParse(companyId, out var company))
        {
            await Deny(context, StatusCodes.Status401Unauthorized,
                "Token sem empresa. Faça login novamente.");
            return;
        }

        // Token de pré-filial: autenticado, mas ainda sem loja escolhida. Só
        // /api/auth/select-branch aceita, e ele não passa por aqui.
        if (!Guid.TryParse(branchId, out var branch))
        {
            await Deny(context, StatusCodes.Status403Forbidden,
                "Selecione uma filial para continuar.");
            return;
        }

        if (context.Request.Headers.TryGetValue(LegacyBranchHeader, out var header)
            && !string.IsNullOrWhiteSpace(header)
            && header.ToString() != branch.ToString())
        {
            logger.LogWarning(
                "Header {Header}={HeaderValue} diverge da filial do token ({TokenBranch}). Header ignorado.",
                LegacyBranchHeader, header.ToString(), branch);
        }

        try
        {
            tenantContext.CompanyId = company;
            tenantContext.BranchId = branch.ToString();
            tenantContext.ConnectionString = await connectionResolver.ResolveAsync(company, context.RequestAborted);
            branchContext.BranchId = branch;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao resolver a conexão da empresa {CompanyId}", company);
            await Deny(context, StatusCodes.Status503ServiceUnavailable,
                "Ambiente da empresa indisponível no momento.");
            return;
        }

        await next(context);
    }

    private static async Task Deny(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error });
    }
}
