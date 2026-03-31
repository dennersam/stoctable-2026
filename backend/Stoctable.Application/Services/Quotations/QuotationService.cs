using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Quotations;
using Stoctable.Communication.Responses.Quotations;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Quotations;

public class QuotationService(
    IQuotationRepository quotationRepository,
    IProductRepository productRepository,
    IInventoryRepository inventoryRepository,
    ISaleRepository saleRepository)
{
    public async Task<Result<IEnumerable<QuotationResponse>>> GetByStatusAsync(QuotationStatus status, CancellationToken ct = default)
    {
        var quotations = await quotationRepository.GetByStatusAsync(status, ct);
        return Result<IEnumerable<QuotationResponse>>.Success(quotations.Select(MapToResponse));
    }

    public async Task<Result<QuotationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var quotation = await quotationRepository.GetWithItemsAsync(id, ct);
        if (quotation is null)
            return Result<QuotationResponse>.NotFound(ErrorMessages.Quotation.NotFound);

        return Result<QuotationResponse>.Success(MapToResponse(quotation));
    }

    public async Task<Result<QuotationResponse>> CreateAsync(CreateQuotationRequest request, Guid salespersonId, CancellationToken ct = default)
    {
        var number = await quotationRepository.GenerateNextNumberAsync(ct);

        var quotation = new Quotation
        {
            QuotationNumber = number,
            CustomerId = request.CustomerId,
            SalespersonId = salespersonId,
            Notes = request.Notes,
            ValidUntil = request.ValidUntil,
            Status = QuotationStatus.Draft
        };

        await quotationRepository.AddAsync(quotation, ct);
        return Result<QuotationResponse>.Success(MapToResponse(quotation), 201);
    }

    public async Task<Result<QuotationResponse>> AddItemAsync(Guid quotationId, AddQuotationItemRequest request, CancellationToken ct = default)
    {
        var quotation = await quotationRepository.GetWithItemsAsync(quotationId, ct);
        if (quotation is null)
            return Result<QuotationResponse>.NotFound(ErrorMessages.Quotation.NotFound);

        if (quotation.Status != QuotationStatus.Draft)
            return Result<QuotationResponse>.Failure(
                string.Format(ErrorMessages.Quotation.CannotModify, quotation.Status));

        var product = await productRepository.GetByIdAsync(request.ProductId, ct);
        if (product is null)
            return Result<QuotationResponse>.NotFound(ErrorMessages.Product.NotFound);

        // Check if item already exists — update quantity
        var existing = quotation.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existing is not null)
        {
            existing.Quantity = request.Quantity;
            existing.DiscountPct = request.DiscountPct;
            existing.LineTotal = CalculateLineTotal(product.SalePrice, request.Quantity, request.DiscountPct);
        }
        else
        {
            quotation.Items.Add(new QuotationItem
            {
                QuotationId = quotationId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                UnitPrice = product.SalePrice,
                DiscountPct = request.DiscountPct,
                LineTotal = CalculateLineTotal(product.SalePrice, request.Quantity, request.DiscountPct)
            });
        }

        RecalculateTotals(quotation);
        await quotationRepository.UpdateAsync(quotation, ct);
        return Result<QuotationResponse>.Success(MapToResponse(quotation));
    }

    public async Task<Result<QuotationResponse>> RemoveItemAsync(Guid quotationId, Guid itemId, CancellationToken ct = default)
    {
        var quotation = await quotationRepository.GetWithItemsAsync(quotationId, ct);
        if (quotation is null)
            return Result<QuotationResponse>.NotFound(ErrorMessages.Quotation.NotFound);

        if (quotation.Status != QuotationStatus.Draft)
            return Result<QuotationResponse>.Failure(
                string.Format(ErrorMessages.Quotation.CannotModify, quotation.Status));

        var item = quotation.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result<QuotationResponse>.NotFound("Item não encontrado.");

        quotation.Items.Remove(item);
        RecalculateTotals(quotation);
        await quotationRepository.UpdateAsync(quotation, ct);
        return Result<QuotationResponse>.Success(MapToResponse(quotation));
    }

    /// <summary>
    /// Finaliza o orçamento (Option B): reserva estoque para cada item.
    /// Atualiza stock_reserved em products e cria StockReservation por item.
    /// </summary>
    public async Task<Result<QuotationResponse>> FinalizeAsync(Guid quotationId, FinalizeQuotationRequest request, CancellationToken ct = default)
    {
        var quotation = await quotationRepository.GetWithItemsAsync(quotationId, ct);
        if (quotation is null)
            return Result<QuotationResponse>.NotFound(ErrorMessages.Quotation.NotFound);

        if (quotation.Status != QuotationStatus.Draft)
            return Result<QuotationResponse>.Failure(
                string.Format(ErrorMessages.Quotation.CannotModify, quotation.Status));

        if (!quotation.Items.Any())
            return Result<QuotationResponse>.Failure(ErrorMessages.Quotation.EmptyItems);

        // Validate stock availability for all items before reserving
        foreach (var item in quotation.Items)
        {
            var product = await productRepository.GetByIdAsync(item.ProductId, ct);
            if (product is null)
                return Result<QuotationResponse>.Failure(ErrorMessages.Product.NotFound);

            var available = product.StockQuantity - product.StockReserved;
            if (item.Quantity > available)
                return Result<QuotationResponse>.Failure(
                    string.Format(ErrorMessages.Product.InsufficientStock, product.Name));
        }

        // Reserve stock for each item
        foreach (var item in quotation.Items)
        {
            var product = await productRepository.GetByIdAsync(item.ProductId, ct);
            if (product is null) continue;

            product.StockReserved += item.Quantity;
            await productRepository.UpdateAsync(product, ct);

            var reservation = new StockReservation
            {
                ProductId = item.ProductId,
                QuotationId = quotationId,
                Quantity = item.Quantity
            };
            await inventoryRepository.AddReservationAsync(reservation, ct);
        }

        // Apply discount and finalize
        if (request.DiscountPct > 0)
            quotation.DiscountPct = request.DiscountPct;
        if (request.Notes is not null)
            quotation.Notes = request.Notes;

        RecalculateTotals(quotation);
        quotation.Status = QuotationStatus.Finalized;
        quotation.FinalizedAt = DateTimeOffset.UtcNow;

        await quotationRepository.UpdateAsync(quotation, ct);
        return Result<QuotationResponse>.Success(MapToResponse(quotation));
    }

    /// <summary>
    /// Cancela o orçamento e libera todas as reservas de estoque ativas.
    /// </summary>
    public async Task<Result<QuotationResponse>> CancelAsync(Guid quotationId, CancelQuotationRequest request, CancellationToken ct = default)
    {
        var quotation = await quotationRepository.GetWithItemsAsync(quotationId, ct);
        if (quotation is null)
            return Result<QuotationResponse>.NotFound(ErrorMessages.Quotation.NotFound);

        if (quotation.Status == QuotationStatus.Converted)
            return Result<QuotationResponse>.Failure(ErrorMessages.Quotation.AlreadyConverted);

        if (quotation.Status == QuotationStatus.Cancelled)
            return Result<QuotationResponse>.Failure(
                string.Format(ErrorMessages.Quotation.CannotCancel, quotation.Status));

        // Release stock reservations if quotation was finalized
        if (quotation.Status == QuotationStatus.Finalized)
            await ReleaseReservationsAsync(quotationId, ct);

        quotation.Status = QuotationStatus.Cancelled;
        quotation.CancelledAt = DateTimeOffset.UtcNow;
        quotation.CancellationReason = request.CancellationReason;

        await quotationRepository.UpdateAsync(quotation, ct);
        return Result<QuotationResponse>.Success(MapToResponse(quotation));
    }

    /// <summary>
    /// Converte o orçamento em venda. Retorna apenas o ID da venda criada.
    /// A criação completa da Sale é feita pelo SaleService.
    /// </summary>
    public async Task<Result<Guid>> ConvertToSaleAsync(Guid quotationId, Guid cashierId, CancellationToken ct = default)
    {
        var quotation = await quotationRepository.GetWithItemsAsync(quotationId, ct);
        if (quotation is null)
            return Result<Guid>.NotFound(ErrorMessages.Quotation.NotFound);

        if (quotation.Status != QuotationStatus.Finalized)
            return Result<Guid>.Failure(
                string.Format(ErrorMessages.Quotation.CannotModify, quotation.Status));

        var saleNumber = await saleRepository.GenerateNextNumberAsync(ct);

        var saleItems = quotation.Items.Select(i => new SaleItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            DiscountPct = i.DiscountPct,
            LineTotal = i.LineTotal
        }).ToList();

        var sale = new Sale
        {
            SaleNumber = saleNumber,
            QuotationId = quotationId,
            CustomerId = quotation.CustomerId,
            SalespersonId = quotation.SalespersonId,
            CashierId = cashierId,
            Subtotal = quotation.Subtotal,
            DiscountAmount = quotation.DiscountAmount,
            TotalAmount = quotation.TotalAmount,
            Notes = quotation.Notes,
            Status = SaleStatus.PendingPayment,
            Items = saleItems
        };

        await saleRepository.AddAsync(sale, ct);

        // Decrement actual stock and release reservations
        foreach (var item in quotation.Items)
        {
            var product = await productRepository.GetByIdAsync(item.ProductId, ct);
            if (product is null) continue;

            product.StockQuantity -= item.Quantity;
            product.StockReserved -= item.Quantity;
            await productRepository.UpdateAsync(product, ct);

            var movement = new InventoryMovement
            {
                ProductId = item.ProductId,
                MovementType = MovementType.Sale,
                Quantity = -item.Quantity,
                QuantityBefore = product.StockQuantity + item.Quantity,
                QuantityAfter = product.StockQuantity,
                ReferenceType = "sale",
                ReferenceId = sale.Id
            };
            await inventoryRepository.AddMovementAsync(movement, ct);
        }

        await ReleaseReservationsAsync(quotationId, ct);

        quotation.Status = QuotationStatus.Converted;
        quotation.ConvertedToSaleId = sale.Id;
        await quotationRepository.UpdateAsync(quotation, ct);

        return Result<Guid>.Success(sale.Id);
    }

    private async Task ReleaseReservationsAsync(Guid quotationId, CancellationToken ct)
    {
        var reservations = await inventoryRepository.GetActiveReservationsByQuotationAsync(quotationId, ct);
        foreach (var reservation in reservations)
        {
            var product = await productRepository.GetByIdAsync(reservation.ProductId, ct);
            if (product is not null)
            {
                product.StockReserved = Math.Max(0, product.StockReserved - reservation.Quantity);
                await productRepository.UpdateAsync(product, ct);
            }

            reservation.IsActive = false;
            reservation.ReleasedAt = DateTimeOffset.UtcNow;
            await inventoryRepository.UpdateReservationAsync(reservation, ct);
        }
    }

    private static decimal CalculateLineTotal(decimal unitPrice, decimal quantity, decimal discountPct)
    {
        var gross = unitPrice * quantity;
        return gross - (gross * discountPct / 100);
    }

    private static void RecalculateTotals(Quotation quotation)
    {
        quotation.Subtotal = quotation.Items.Sum(i => i.LineTotal);
        quotation.DiscountAmount = quotation.Subtotal * quotation.DiscountPct / 100;
        quotation.TotalAmount = quotation.Subtotal - quotation.DiscountAmount;
    }

    private static QuotationResponse MapToResponse(Quotation q) => new(
        Id: q.Id,
        QuotationNumber: q.QuotationNumber,
        CustomerId: q.CustomerId,
        CustomerName: q.Customer?.FullName,
        SalespersonId: q.SalespersonId,
        SalespersonName: q.Salesperson?.FullName,
        Status: q.Status.ToString().ToLower(),
        Subtotal: q.Subtotal,
        DiscountPct: q.DiscountPct,
        DiscountAmount: q.DiscountAmount,
        TotalAmount: q.TotalAmount,
        Notes: q.Notes,
        ValidUntil: q.ValidUntil,
        FinalizedAt: q.FinalizedAt,
        CancelledAt: q.CancelledAt,
        CancellationReason: q.CancellationReason,
        ConvertedToSaleId: q.ConvertedToSaleId,
        Items: q.Items.Select(i => new QuotationItemResponse(
            Id: i.Id,
            ProductId: i.ProductId,
            ProductSku: i.Product?.Sku ?? string.Empty,
            ProductName: i.Product?.Name ?? string.Empty,
            Quantity: i.Quantity,
            UnitPrice: i.UnitPrice,
            DiscountPct: i.DiscountPct,
            LineTotal: i.LineTotal)).ToList(),
        CreatedAt: q.CreatedAt);
}
