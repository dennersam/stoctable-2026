using Stoctable.Domain.Entities;

namespace Stoctable.Domain.Contracts.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetByIdNoTrackingAsync(Guid id, CancellationToken ct = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<IEnumerable<Product>> SearchAsync(string query, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetLowStockAsync(CancellationToken ct = default);
    Task<int> GetNextSkuAsync(CancellationToken ct = default);
    Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default);

    /// <summary>
    /// Decrementa stock_quantity e stock_reserved atomicamente em uma única
    /// SQL UPDATE com guarda WHERE stock_quantity &gt;= quantity. Retorna
    /// true se a linha foi atualizada (estoque havia), false caso contrário —
    /// previne oversell sob concorrência sem precisar de optimistic lock.
    /// </summary>
    Task<bool> TryDecrementStockAsync(Guid productId, decimal quantity, CancellationToken ct = default);

    /// <summary>
    /// Devolve quantidade ao stock_quantity em uma única SQL UPDATE.
    /// Sem guarda — devolução de estoque (cancelar venda) sempre deve
    /// proceder. Retorna true se a linha existe.
    /// </summary>
    Task<bool> IncrementStockAsync(Guid productId, decimal quantity, CancellationToken ct = default);

    /// <summary>
    /// Compromete quantidade a um orçamento finalizado, aumentando o reservado.
    ///
    /// Existe como método do repositório — em vez de mutar
    /// <c>Product.StockReserved</c> e salvar — porque toda escrita de estoque
    /// precisa ser espelhada em <c>product_stocks</c>, e uma mutação via EF na
    /// entidade passaria despercebida por esse espelhamento.
    /// </summary>
    Task ReserveStockAsync(Guid productId, decimal quantity, CancellationToken ct = default);

    /// <summary>
    /// Libera reserva (orçamento cancelado, expirado ou convertido em venda).
    /// Nunca deixa o reservado negativo — a liberação é idempotente por natureza.
    /// </summary>
    Task ReleaseReservedStockAsync(Guid productId, decimal quantity, CancellationToken ct = default);
}
