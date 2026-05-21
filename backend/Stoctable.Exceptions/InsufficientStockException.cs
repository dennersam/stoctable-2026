namespace Stoctable.Exceptions;

/// <summary>
/// Lançada quando uma tentativa de baixa de estoque falha porque a
/// quantidade disponível ficou abaixo do necessário entre o carregamento
/// do orçamento e a conversão em venda.
/// </summary>
public class InsufficientStockException : Exception
{
    public InsufficientStockException(string message) : base(message) { }
}
