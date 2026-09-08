namespace Stoctable.Domain.Contracts.Services;

/// <summary>
/// Descobre a connection string do banco de uma empresa.
///
/// Substitui a busca por segredo <c>STOCTABLE-CONN-{filial}</c> no Key Vault:
/// o banco é por EMPRESA, não por filial, e a connection string mora cifrada
/// no control plane. Ver Company.ConnectionStringEncrypted para o porquê de
/// não ser o Key Vault.
/// </summary>
public interface ICompanyConnectionResolver
{
    Task<string> ResolveAsync(Guid companyId, CancellationToken ct = default);
}
