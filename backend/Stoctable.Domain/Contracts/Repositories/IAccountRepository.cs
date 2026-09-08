using Stoctable.Domain.Entities.ControlPlane;

namespace Stoctable.Domain.Contracts.Repositories;

/// <summary>
/// Acesso às contas de login no control plane. Separado de
/// <see cref="IUserRepository"/> de propósito: aquele lê a tabela <c>users</c>
/// dentro do banco de cada empresa, que virou projeção de exibição; este lê a
/// identidade de verdade, que precisa ser consultada ANTES de saber qual banco
/// de empresa abrir.
/// </summary>
public interface IAccountRepository
{
    /// <summary>Busca por e-mail (identidade global), já com a empresa.</summary>
    Task<Account?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<Account?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<Account?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Account>> ListByCompanyAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>Filiais que a conta pode acessar, ordenadas com a matriz primeiro.</summary>
    Task<IReadOnlyList<Branch>> GetBranchesAsync(Guid accountId, CancellationToken ct = default);

    Task<IReadOnlyList<Branch>> ListCompanyBranchesAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>E-mail é único no SaaS inteiro, então a checagem é global.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    /// <summary>Username é apelido de exibição: único apenas dentro da empresa.</summary>
    Task<bool> UsernameExistsAsync(Guid companyId, string username, CancellationToken ct = default);

    Task AddAsync(Account account, CancellationToken ct = default);

    Task ReplaceBranchesAsync(Guid accountId, IEnumerable<Guid> branchIds, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);
}
