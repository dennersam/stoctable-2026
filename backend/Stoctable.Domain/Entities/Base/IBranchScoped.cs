namespace Stoctable.Domain.Entities.Base;

/// <summary>
/// Marca uma entidade cujas linhas pertencem a uma filial específica.
///
/// O banco é da EMPRESA — catálogo, clientes e fornecedores são compartilhados
/// entre as lojas. O que é operação de cada loja (venda, caixa, orçamento,
/// estoque, movimentação) carrega esta marca e fica isolado por filtro global
/// no <c>StoctableDbContext</c>.
///
/// Quem implementa isto não precisa preencher <see cref="BranchId"/> na mão:
/// o <c>BranchScopeSaveChangesInterceptor</c> carimba no insert. Preencher
/// manualmente é justamente como se grava linha na filial errada.
/// </summary>
public interface IBranchScoped
{
    Guid BranchId { get; set; }
}
