namespace Stoctable.Communication.Requests.Quotations;

public record CreateQuotationRequest(
    Guid? CustomerId = null,
    string? Notes = null,
    DateOnly? ValidUntil = null);

public record AddQuotationItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal DiscountPct = 0);

public record FinalizeQuotationRequest(
    decimal DiscountPct = 0,
    string? Notes = null);

public record CancelQuotationRequest(string CancellationReason);
