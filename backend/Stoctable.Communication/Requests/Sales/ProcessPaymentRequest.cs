namespace Stoctable.Communication.Requests.Sales;

public record ProcessPaymentRequest(List<PaymentEntryRequest> Payments);

public record PaymentEntryRequest(
    Guid PaymentMethodId,
    decimal Amount,
    int Installments = 1,
    string? TransactionRef = null);
