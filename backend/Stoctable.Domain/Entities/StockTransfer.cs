using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities;

/// <summary>
/// Movimentação de mercadoria entre duas filiais da mesma empresa, em duas etapas:
/// a origem envia (baixa o estoque dela) e o destino confere e recebe (dá entrada
/// no dele).
///
/// <para>
/// <b>Uma linha, não duas.</b> É tentador dar a cada filial o seu registro, mas
/// isso criaria um invariante de consistência entre eles que ninguém manteria —
/// o mesmo argumento que impede <c>SaleItem</c> de ter <c>branch_id</c>. A linha
/// é da ORIGEM no sentido de <see cref="IBranchScoped"/>: é ela que emite o
/// documento e é o estoque dela que sai primeiro. O destino enxerga o mesmo
/// registro através do filtro de dupla visibilidade no DbContext.
/// </para>
///
/// <para>
/// <b>Por que duas etapas resolvem o problema difícil.</b> A origem envia estando
/// logada na origem; o destino recebe estando logado no destino. Cada perna
/// escreve apenas a linha de <c>product_stocks</c> da própria filial, pelo caminho
/// normal, com o BranchContext certo. Nenhuma transação precisa tocar duas
/// filiais, e nenhum caminho de escrita precisa furar o isolamento.
/// </para>
///
/// <para>
/// <b>Cancelamento só antes do envio.</b> Estornar algo já em trânsito exigiria a
/// origem creditar a si mesma enquanto o destino talvez já tenha recebido — de
/// novo a escrita cross-filial que o desenho inteiro evita. Carga que voltou é
/// recebida com quantidade zero (fica registrada a divergência) e o retorno
/// físico vira uma nova transferência no sentido inverso.
/// </para>
/// </summary>
public class StockTransfer : BaseEntity, IBranchScoped
{
    /// <summary>Filial de ORIGEM. Carimbada pelo interceptor na criação.</summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// Filial de DESTINO. Sem chave estrangeira: filial é conceito do control
    /// plane, que vive em outro banco. Quem garante que o destino pertence à
    /// empresa é a validação contra as claims assinadas, no serviço.
    /// </summary>
    public Guid DestinationBranchId { get; set; }

    public string TransferNumber { get; set; } = string.Empty;
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Pending;

    public DateTimeOffset? ShippedAt { get; set; }
    public string? ShippedBy { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? ReceivedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Chegou menos do que saiu. Fica aberto para as duas filiais resolverem.</summary>
    public bool HasDivergence { get; set; }

    public string? Notes { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = [];
}
