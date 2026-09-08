namespace Stoctable.Domain.Contracts.Services;

/// <summary>
/// Empresa e filial da requisição em curso, expostas para a camada de
/// aplicação sem que ela precise conhecer o TenantContext da infraestrutura.
///
/// Os valores vêm das claims assinadas do JWT, resolvidas pelo
/// TenantResolutionMiddleware.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Lança se a requisição não tiver empresa resolvida.</summary>
    Guid CompanyId { get; }

    /// <summary>Lança se a requisição não tiver filial resolvida.</summary>
    Guid BranchId { get; }

    /// <summary>
    /// Filiais que esta conta pode acessar, vindas das claims assinadas.
    ///
    /// Autoriza as duas operações legítimas que olham além da filial ativa:
    /// escolher o destino de uma transferência e consultar o estoque da rede.
    /// Nunca serve para ESCREVER no estoque de outra filial.
    /// </summary>
    IReadOnlyCollection<Guid> AllowedBranchIds { get; }
}
