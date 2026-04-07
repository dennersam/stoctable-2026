using Stoctable.Application.Results;
using Stoctable.Domain.Contracts.Repositories;

namespace Stoctable.Application.Services.PaymentMethods;

public record PaymentMethodResponse(
    Guid Id,
    string Name,
    bool RequiresInstallments,
    int MaxInstallments,
    bool IsActive);

public class PaymentMethodService(IPaymentMethodRepository paymentMethodRepository)
{
    public async Task<Result<IEnumerable<PaymentMethodResponse>>> GetActiveAsync(CancellationToken ct = default)
    {
        var methods = await paymentMethodRepository.GetActiveAsync(ct);
        return Result<IEnumerable<PaymentMethodResponse>>.Success(
            methods.Select(m => new PaymentMethodResponse(m.Id, m.Name, m.RequiresInstallments, m.MaxInstallments, m.IsActive)));
    }
}
