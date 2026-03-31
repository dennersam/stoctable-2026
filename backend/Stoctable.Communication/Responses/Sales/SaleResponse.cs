namespace Stoctable.Communication.Responses.Sales;

public record SaleResponse(
    Guid Id,
    string SaleNumber,
    Guid? QuotationId,
    Guid? CustomerId,
    string? CustomerName,
    Guid? SalespersonId,
    string? SalespersonName,
    Guid? CashierId,
    string? CashierName,
    string Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountRemaining,
    string? Notes,
    DateTimeOffset? CompletedAt,
    List<SaleItemResponse> Items,
    List<PaymentResponse> Payments,
    DateTimeOffset CreatedAt);

public record SaleItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal LineTotal);

public record PaymentResponse(
    Guid Id,
    Guid PaymentMethodId,
    string PaymentMethodName,
    decimal Amount,
    int Installments,
    string Status,
    string? TransactionRef,
    DateTimeOffset? PaidAt);
