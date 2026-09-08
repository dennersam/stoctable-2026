using Stoctable.Application.Results;
using Stoctable.Domain.Contracts;
using Stoctable.Communication.Responses.Inventory;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Inventory;

public class InventoryService(
    IInventoryRepository inventoryRepository,
    IProductRepository productRepository,
    IProductStockRepository stockRepository,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<IEnumerable<InventoryMovement>>> GetMovementsByProductAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<IEnumerable<InventoryMovement>>.NotFound(ErrorMessages.Product.NotFound);

        var movements = await inventoryRepository.GetMovementsByProductAsync(productId, ct);
        return Result<IEnumerable<InventoryMovement>>.Success(movements);
    }

    /// <summary>
    /// Saldo do produto em todas as filiais que a conta pode ver — a resposta
    /// para "tem essa peça em outra loja?", que é o que motiva uma transferência.
    /// </summary>
    public async Task<Result<IEnumerable<BranchStockResponse>>> GetNetworkStockAsync(
        Guid productId, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<IEnumerable<BranchStockResponse>>.NotFound(ErrorMessages.Product.NotFound);

        var rows = await stockRepository.GetNetworkAsync(productId, currentTenant.AllowedBranchIds, ct);

        return Result<IEnumerable<BranchStockResponse>>.Success(rows.Select(r => new BranchStockResponse(
            BranchId: r.BranchId,
            Quantity: r.Quantity,
            Reserved: r.Reserved,
            Available: r.Quantity - r.Reserved,
            Minimum: r.Minimum,
            InTransit: r.InTransit)));
    }

    /// <summary>
    /// Define o nível de alerta de estoque baixo do produto NESTA filial.
    /// </summary>
    public async Task<Result<bool>> SetMinimumAsync(Guid productId, decimal minimum, CancellationToken ct = default)
    {
        if (minimum < 0)
            return Result<bool>.Failure("O estoque mínimo não pode ser negativo.");

        var product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<bool>.NotFound(ErrorMessages.Product.NotFound);

        await stockRepository.SetMinimumAsync(productId, minimum, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Ajusta o estoque da FILIAL ATIVA. Quantidade negativa é retirada.
    ///
    /// Antes este método mutava <c>product.StockQuantity</c> via rastreamento do
    /// EF e chamava UpdateAsync. Isso tinha dois defeitos ao mesmo tempo: não
    /// passava pelo caminho atômico (a checagem de saldo negativo acontecia em
    /// memória, depois da mutação, sem proteger nada sob concorrência) e mexia
    /// no agregado da EMPRESA, e não no estoque da loja.
    /// </summary>
    public async Task<Result<InventoryMovement>> AdjustStockAsync(
        Guid productId,
        decimal quantity,
        string notes,
        string username,
        CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<InventoryMovement>.NotFound(ErrorMessages.Product.NotFound);

        if (quantity == 0)
            return Result<InventoryMovement>.Failure(ErrorMessages.Inventory.InsufficientStock);

        return await unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            var result = quantity > 0
                ? await stockRepository.IncrementAsync(productId, quantity, c)
                : await stockRepository.TryDecrementAsync(productId, -quantity, c);

            // A guarda do SQL é a única proteção real: reprovou, não há saldo.
            if (!result.Success)
                return Result<InventoryMovement>.Failure(ErrorMessages.Inventory.InsufficientStock);

            var movement = new InventoryMovement
            {
                ProductId = productId,
                MovementType = quantity > 0 ? MovementType.AdjustmentIn : MovementType.AdjustmentOut,
                Quantity = quantity,
                // O "antes" sai por aritmética a partir do saldo que o UPDATE
                // devolveu. Reler o produto aqui seria read-after-write não
                // atômico, e registraria um valor que nunca existiu.
                QuantityBefore = result.QuantityAfter - quantity,
                QuantityAfter = result.QuantityAfter,
                ReferenceType = "adjustment",
                Notes = notes,
                CreatedBy = username
            };

            await inventoryRepository.AddMovementAsync(movement, c);
            return Result<InventoryMovement>.Success(movement, 201);
        }, ct);
    }
}
