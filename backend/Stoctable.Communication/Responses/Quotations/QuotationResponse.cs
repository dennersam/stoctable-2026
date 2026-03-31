namespace Stoctable.Communication.Responses.Quotations;

public record QuotationResponse(
    Guid Id,
    string QuotationNumber,
    Guid? CustomerId,
    string? CustomerName,
    Guid? SalespersonId,
    string? SalespersonName,
    string Status,
    decimal Subtotal,
    decimal DiscountPct,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? Notes,
    DateOnly? ValidUntil,
    DateTimeOffset? FinalizedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    Guid? ConvertedToSaleId,
    List<QuotationItemResponse> Items,
    DateTimeOffset CreatedAt);

public record QuotationItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal LineTotal);
