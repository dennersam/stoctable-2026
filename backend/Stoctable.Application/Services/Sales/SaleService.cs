using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Sales;
using Stoctable.Communication.Responses.Sales;
using Stoctable.Domain.Contracts;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Sales;

public class SaleService(
    ISaleRepository saleRepository,
    IProductRepository productRepository,
    IInventoryRepository inventoryRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<IEnumerable<SaleResponse>>> GetByStatusAsync(SaleStatus status, CancellationToken ct = default)
    {
        var sales = await saleRepository.GetByStatusAsync(status, ct);
        return Result<IEnumerable<SaleResponse>>.Success(sales.Select(MapToResponse));
    }

    public async Task<Result<SaleResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sale = await saleRepository.GetWithDetailsAsync(id, ct);
        if (sale is null)
            return Result<SaleResponse>.NotFound(ErrorMessages.Sale.NotFound);

        return Result<SaleResponse>.Success(MapToResponse(sale));
    }

    public async Task<Result<SaleResponse>> ProcessPaymentAsync(Guid saleId, ProcessPaymentRequest request, CancellationToken ct = default)
    {
        var sale = await saleRepository.GetWithDetailsAsync(saleId, ct);
        if (sale is null)
            return Result<SaleResponse>.NotFound(ErrorMessages.Sale.NotFound);

        if (sale.Status == SaleStatus.Paid)
            return Result<SaleResponse>.Failure(ErrorMessages.Sale.AlreadyPaid);

        var totalPayment = request.Payments.Sum(p => p.Amount);
        if (totalPayment > sale.TotalAmount)
            return Result<SaleResponse>.Failure(ErrorMessages.Sale.PaymentExceedsTotal);

        foreach (var paymentEntry in request.Payments)
        {
            var payment = new Payment
            {
                SaleId = saleId,
                PaymentMethodId = paymentEntry.PaymentMethodId,
                Amount = paymentEntry.Amount,
                Installments = paymentEntry.Installments,
                TransactionRef = paymentEntry.TransactionRef,
                Status = PaymentStatus.Completed,
                PaidAt = DateTimeOffset.UtcNow
            };
            sale.Payments.Add(payment);
            sale.AmountPaid += payment.Amount;
        }

        if (sale.AmountPaid >= sale.TotalAmount)
        {
            sale.Status = SaleStatus.Paid;
            sale.CompletedAt = DateTimeOffset.UtcNow;
        }
        else if (sale.AmountPaid > 0)
        {
            sale.Status = SaleStatus.PartiallyPaid;
        }

        await saleRepository.UpdateAsync(sale, ct);
        return Result<SaleResponse>.Success(MapToResponse(sale));
    }

    /// <summary>
    /// Cancela uma venda em qualquer status (exceto já cancelada). Devolve
    /// o estoque dos itens, marca pagamentos completados como estornados e
    /// muda o status para Cancelled — tudo em uma única transação.
    /// </summary>
    public async Task<Result<SaleResponse>> CancelAsync(Guid saleId, CancelSaleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CancellationReason))
            return Result<SaleResponse>.Failure(ErrorMessages.Sale.CancellationReasonRequired);

        var sale = await saleRepository.GetWithDetailsAsync(saleId, ct);
        if (sale is null)
            return Result<SaleResponse>.NotFound(ErrorMessages.Sale.NotFound);

        if (sale.Status == SaleStatus.Cancelled)
            return Result<SaleResponse>.Failure(ErrorMessages.Sale.AlreadyCancelled);

        await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            // Devolve estoque de cada item
            foreach (var item in sale.Items)
            {
                await productRepository.IncrementStockAsync(item.ProductId, item.Quantity, innerCt);

                var product = await productRepository.GetByIdNoTrackingAsync(item.ProductId, innerCt);
                if (product is null) continue;

                var movement = new InventoryMovement
                {
                    ProductId = item.ProductId,
                    MovementType = MovementType.AdjustmentIn,
                    Quantity = item.Quantity,
                    QuantityBefore = product.StockQuantity - item.Quantity,
                    QuantityAfter = product.StockQuantity,
                    ReferenceType = "sale_cancellation",
                    ReferenceId = sale.Id
                };
                await inventoryRepository.AddMovementAsync(movement, innerCt);
            }

            // Estorna pagamentos completados
            foreach (var payment in sale.Payments.Where(p => p.Status == PaymentStatus.Completed))
            {
                payment.Status = PaymentStatus.Refunded;
                payment.RefundedAt = DateTimeOffset.UtcNow;
            }

            sale.Status = SaleStatus.Cancelled;
            sale.CancelledAt = DateTimeOffset.UtcNow;
            sale.CancellationReason = request.CancellationReason;
            await saleRepository.UpdateAsync(sale, innerCt);
        }, ct);

        return Result<SaleResponse>.Success(MapToResponse(sale));
    }

    private static SaleResponse MapToResponse(Sale s) => new(
        Id: s.Id,
        SaleNumber: s.SaleNumber,
        QuotationId: s.QuotationId,
        CustomerId: s.CustomerId,
        CustomerName: s.Customer?.FullName,
        SalespersonId: s.SalespersonId,
        SalespersonName: s.Salesperson?.FullName,
        CashierId: s.CashierId,
        CashierName: s.Cashier?.FullName,
        Status: s.Status switch
        {
            SaleStatus.PendingPayment => "pending_payment",
            SaleStatus.PartiallyPaid  => "partially_paid",
            SaleStatus.Paid           => "paid",
            SaleStatus.Cancelled      => "cancelled",
            _                         => s.Status.ToString().ToLower()
        },
        Subtotal: s.Subtotal,
        DiscountAmount: s.DiscountAmount,
        TotalAmount: s.TotalAmount,
        AmountPaid: s.AmountPaid,
        AmountRemaining: Math.Max(0, s.TotalAmount - s.AmountPaid),
        Notes: s.Notes,
        CompletedAt: s.CompletedAt,
        CancelledAt: s.CancelledAt,
        CancellationReason: s.CancellationReason,
        Items: s.Items.Select(i => new SaleItemResponse(
            Id: i.Id,
            ProductId: i.ProductId,
            ProductSku: i.Product?.Sku ?? string.Empty,
            ProductName: i.Product?.Name ?? string.Empty,
            Quantity: i.Quantity,
            UnitPrice: i.UnitPrice,
            DiscountPct: i.DiscountPct,
            LineTotal: i.LineTotal)).ToList(),
        Payments: s.Payments.Select(p => new PaymentResponse(
            Id: p.Id,
            PaymentMethodId: p.PaymentMethodId,
            PaymentMethodName: p.PaymentMethod?.Name ?? string.Empty,
            Amount: p.Amount,
            Installments: p.Installments,
            Status: p.Status.ToString().ToLower(),
            TransactionRef: p.TransactionRef,
            PaidAt: p.PaidAt,
            RefundedAt: p.RefundedAt)).ToList(),
        CreatedAt: s.CreatedAt);
}
