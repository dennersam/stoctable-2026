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
/// Esta é a fonte da verdade do estoque. As colunas antigas em <c>products</c>
/// ainda existem, mas ninguém mais as lê ou escreve — sobrevivem só até a
/// migration que as derruba, como diagnóstico de reconciliação.
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

    /// <summary>
    /// Nível que dispara o alerta de estoque baixo NESTA filial.
    ///
    /// É por loja, e não do catálogo: uma loja pequena repõe a partir de 2, a
    /// matriz a partir de 20. O mesmo produto, portanto, tem mínimos diferentes.
    /// </summary>
    public decimal Minimum { get; set; }

    /// <summary>O que pode ser vendido agora.</summary>
    public decimal Available => Quantity - Reserved;
}
