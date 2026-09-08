using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

/// <summary>
/// Estoque de um produto em uma filial.
///
/// Existe porque produto é da EMPRESA e estoque é da FILIAL: as colunas
/// <c>stock_quantity</c> e <c>stock_reserved</c> ficavam na linha do produto, o
/// que tornava as duas coisas impossíveis ao mesmo tempo. O catálogo continua
/// único; a quantidade passa a ser por loja.
///
/// Durante a transição as colunas antigas em <c>products</c> continuam sendo
/// escritas em paralelo, para que dê para reconciliar as duas fontes antes de
/// derrubá-las. Ver o plano de migração.
/// </summary>
public class ProductStock : BaseEntity, IBranchScoped
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Quantidade física na filial.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Parte da quantidade comprometida por orçamentos finalizados.</summary>
    public decimal Reserved { get; set; }

    /// <summary>O que pode ser vendido agora.</summary>
    public decimal Available => Quantity - Reserved;
}
