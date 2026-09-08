using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities.ControlPlane;

/// <summary>
/// Estado durável do provisionamento de uma empresa.
///
/// A fila em memória do worker é só uma otimização: a fonte da verdade é esta
/// tabela. Um poller varre periodicamente as linhas não terminais paradas há
/// mais de alguns minutos e as reenfileira — é isso que faz o provisionamento
/// sobreviver a um restart do App Service no meio do caminho.
/// </summary>
public class ProvisioningJob : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Último passo CONCLUÍDO. A retomada começa do seguinte.</summary>
    public ProvisioningStep Step { get; set; } = ProvisioningStep.Pending;

    public ProvisioningState State { get; set; } = ProvisioningState.Queued;

    public int Attempts { get; set; }

    /// <summary>
    /// Mensagem da última falha. Fica no suporte — nunca é devolvida ao usuário
    /// final, porque erros do provedor de banco expõem detalhe de infraestrutura.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>Quando a próxima tentativa pode acontecer (backoff exponencial).</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// Token opaco entregue ao frontend para acompanhar o progresso. Não é o id
    /// da empresa de propósito: o endpoint de status é anônimo, e expor o id
    /// permitiria enumerar as empresas cadastradas.
    /// </summary>
    public string StatusToken { get; set; } = string.Empty;
}
