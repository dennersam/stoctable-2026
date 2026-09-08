namespace Stoctable.Infrastructure.Tenancy;

/// <summary>
/// Qual banco abrir nesta requisição. Escopo de requisição, preenchido pelo
/// TenantResolutionMiddleware a partir das claims assinadas do JWT.
///
/// É separado do <see cref="BranchContext"/> de propósito: este resolve a
/// CONEXÃO, aquele resolve QUAIS LINHAS enxergar dentro dela. Os dois
/// coincidiam quando cada filial tinha um banco; deixaram de coincidir quando
/// uma empresa passou a ter um banco com várias filiais dentro.
/// </summary>
public class TenantContext
{
    public Guid? CompanyId { get; set; }
    public string? BranchId { get; set; }
    public string? ConnectionString { get; set; }

    public bool IsResolved => !string.IsNullOrEmpty(ConnectionString);
}
