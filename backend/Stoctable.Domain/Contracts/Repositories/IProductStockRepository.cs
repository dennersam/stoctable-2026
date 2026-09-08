using Stoctable.Domain.Entities;

namespace Stoctable.Domain.Contracts.Repositories;

/// <summary>
/// Resultado de uma operação de estoque.
///
/// <paramref name="QuantityAfter"/> e <paramref name="ReservedAfter"/> vêm do
/// <c>RETURNING</c> do próprio UPDATE, não de uma leitura posterior. A diferença
/// importa: ler o saldo depois de escrever é read-after-write não atômico, e sob
/// concorrência o movimento de inventário acabava registrando um "antes" que
/// nunca existiu. Quem precisa do valor anterior deriva por aritmética.
///
/// Quando <paramref name="Success"/> é false a guarda reprovou e nada foi
/// escrito — os saldos trazem o estado atual, para a mensagem de erro.
/// </summary>
public readonly record struct StockOperationResult(
    bool Success,
    decimal QuantityAfter,
    decimal ReservedAfter)
{
    public decimal AvailableAfter => QuantityAfter - ReservedAfter;
}

/// <summary>
/// Saldo de uma filial na consulta de rede. <paramref name="InTransit"/> é o que
/// já saiu dela e ainda não foi recebido — não está aqui nem lá.
/// </summary>
public record BranchStockRow(
    Guid BranchId,
    decimal Quantity,
    decimal Reserved,
    decimal Minimum,
    decimal InTransit);

/// <summary>
/// Estoque da FILIAL ATIVA. Toda operação age sobre a linha
/// <c>(branch_id, product_id)</c> da filial do <c>BranchContext</c> da requisição.
///
/// ⚠️ Nenhum método recebe filial como parâmetro, e isso é deliberado. O estoque
/// de outra filial nunca é escrito por este caminho: a transferência entre lojas
/// é um fluxo de duas etapas em que cada perna roda na sessão da própria filial.
/// Um <c>AdjustForBranchAsync(branchId, …)</c> aqui seria o buraco no isolamento
/// que o desenho inteiro existe para evitar.
/// </summary>
public interface IProductStockRepository
{
    /// <summary>
    /// Baixa estoque com guarda atômica <c>WHERE quantity &gt;= qty</c>. Retorna
    /// <c>Success = false</c> sem escrever nada quando não há saldo — é isso que
    /// impede dois caixas venderem a mesma peça sem optimistic lock.
    /// </summary>
    Task<StockOperationResult> TryDecrementAsync(Guid productId, decimal quantity, CancellationToken ct = default);

    /// <summary>
    /// Devolve quantidade ao estoque. Sem guarda — devolução (cancelamento de
    /// venda, recebimento de transferência) sempre deve proceder.
    /// </summary>
    Task<StockOperationResult> IncrementAsync(Guid productId, decimal quantity, CancellationToken ct = default);

    /// <summary>
    /// Compromete quantidade a um orçamento, com guarda sobre o DISPONÍVEL
    /// (<c>quantity - reserved</c>). A guarda fecha a janela entre validar e
    /// reservar, que antes deixava dois orçamentos comprometerem a mesma peça.
    /// </summary>
    Task<StockOperationResult> TryReserveAsync(Guid productId, decimal quantity, CancellationToken ct = default);

    /// <summary>
    /// Libera reserva. Nunca deixa o reservado negativo: a mesma reserva pode ser
    /// liberada mais de uma vez (cancelamento seguido de expiração).
    /// </summary>
    Task<StockOperationResult> ReleaseReservedAsync(Guid productId, decimal quantity, CancellationToken ct = default);

    /// <summary>
    /// Define o nível de alerta de estoque baixo nesta filial, criando a linha
    /// se o produto ainda não foi movimentado aqui.
    /// </summary>
    Task<StockOperationResult> SetMinimumAsync(Guid productId, decimal minimum, CancellationToken ct = default);

    /// <summary>Linha de estoque da filial ativa, ou null se o produto nunca foi movimentado nela.</summary>
    Task<ProductStock?> GetAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Saldo do produto em cada filial de <paramref name="branchIds"/>, para a
    /// consulta "onde tem esta peça".
    ///
    /// É o ÚNICO caminho que enxerga além da filial ativa, e por isso é só
    /// leitura e recebe as filiais permitidas de fora — elas vêm das claims
    /// assinadas, nunca do corpo da requisição.
    /// </summary>
    Task<IReadOnlyList<BranchStockRow>> GetNetworkAsync(
        Guid productId, IReadOnlyCollection<Guid> branchIds, CancellationToken ct = default);
}
