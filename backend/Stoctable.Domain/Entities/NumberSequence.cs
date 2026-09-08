using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

/// <summary>
/// Sequência de números por filial e prefixo (ex: ORC-MEGA-202609). Usada para
/// gerar identificadores de documento atomicamente via INSERT...ON CONFLICT
/// DO UPDATE no PostgreSQL, sem race condition.
///
/// A chave é composta por (filial, prefixo): cada loja tem a própria contagem,
/// senão duas lojas emitiriam orçamentos com o mesmo número no mesmo dia.
/// </summary>
public class NumberSequence : IBranchScoped
{
    public Guid BranchId { get; set; }

    public string Prefix { get; set; } = string.Empty;
    public long CurrentValue { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
