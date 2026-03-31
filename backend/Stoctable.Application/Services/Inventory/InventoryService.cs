using Stoctable.Application.Results;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Inventory;

public class InventoryService(IInventoryRepository inventoryRepository, IProductRepository productRepository)
{
    public async Task<Result<IEnumerable<InventoryMovement>>> GetMovementsByProductAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<IEnumerable<InventoryMovement>>.NotFound(ErrorMessages.Product.NotFound);

        var movements = await inventoryRepository.GetMovementsByProductAsync(productId, ct);
        return Result<IEnumerable<InventoryMovement>>.Success(movements);
    }

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

        var quantityBefore = product.StockQuantity;
        product.StockQuantity += quantity;

        if (product.StockQuantity < 0)
            return Result<InventoryMovement>.Failure(ErrorMessages.Inventory.InsufficientStock);

        await productRepository.UpdateAsync(product, ct);

        var movementType = quantity >= 0 ? MovementType.AdjustmentIn : MovementType.AdjustmentOut;
        var movement = new InventoryMovement
        {
            ProductId = productId,
            MovementType = movementType,
            Quantity = quantity,
            QuantityBefore = quantityBefore,
            QuantityAfter = product.StockQuantity,
            ReferenceType = "adjustment",
            Notes = notes,
            CreatedBy = username
        };

        await inventoryRepository.AddMovementAsync(movement, ct);
        return Result<InventoryMovement>.Success(movement, 201);
    }
}
