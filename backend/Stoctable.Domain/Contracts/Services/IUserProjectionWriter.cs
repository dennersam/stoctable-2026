using Stoctable.Domain.Entities.ControlPlane;

namespace Stoctable.Domain.Contracts.Services;

/// <summary>
/// Espelha uma conta do control plane na tabela <c>users</c> do banco da
/// empresa.
///
/// A tabela do tenant não pôde ser apagada: <c>audit_logs.user_id</c>,
/// <c>created_by</c> e o vendedor da venda apontam para ela, e removê-la
/// deixaria toda referência pendurada. Então ela virou projeção de exibição —
/// mesmo Id da conta, sem nenhuma coluna de autenticação.
///
/// São dois bancos, então a projeção É eventualmente consistente e VAI
/// divergir em algum momento (falha entre uma escrita e outra). Isso é aceito
/// de propósito: o dado é só de exibição, e a correção é reprojetar, não uma
/// transação distribuída — que custaria caro para proteger um nome próprio.
/// </summary>
public interface IUserProjectionWriter
{
    Task UpsertAsync(Account account, CancellationToken ct = default);

    /// <summary>Reprojeta todas as contas da empresa. Usado para reparar divergência.</summary>
    Task<int> ResyncAsync(IEnumerable<Account> accounts, CancellationToken ct = default);
}
