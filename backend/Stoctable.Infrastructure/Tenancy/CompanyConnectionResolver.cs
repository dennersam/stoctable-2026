using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Tenancy;

/// <summary>
/// Lê a connection string da empresa no control plane, decifra e guarda em
/// cache por alguns minutos.
///
/// O cache tem expiração e invalidação explícita — o antigo era um dicionário
/// permanente, e por isso trocar a senha do banco exigia reiniciar o App
/// Service. O provisionamento invalida a entrada ao concluir.
/// </summary>
public class CompanyConnectionResolver(
    ControlPlaneDbContext control,
    CompanyConnectionCache cache,
    IConnectionStringProtector protector) : ICompanyConnectionResolver
{
    public async Task<string> ResolveAsync(Guid companyId, CancellationToken ct = default)
    {
        if (cache.TryGet(companyId, out var cached))
            return cached;

        var company = await control.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Status, c.ConnectionStringEncrypted })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Empresa {companyId} não encontrada no control plane.");

        if (company.Status != CompanyStatus.Ready)
            throw new InvalidOperationException($"Empresa {companyId} está em '{company.Status}', não 'Ready'.");

        if (company.ConnectionStringEncrypted is null || company.ConnectionStringEncrypted.Length == 0)
            throw new InvalidOperationException($"Empresa {companyId} não tem connection string gravada.");

        var connectionString = protector.Unprotect(company.ConnectionStringEncrypted);
        cache.Set(companyId, connectionString);
        return connectionString;
    }
}
