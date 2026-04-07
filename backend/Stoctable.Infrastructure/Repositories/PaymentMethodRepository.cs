using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class PaymentMethodRepository(StoctableDbContext context)
    : Repository<PaymentMethod>(context), IPaymentMethodRepository
{
    public async Task<IEnumerable<PaymentMethod>> GetActiveAsync(CancellationToken ct = default)
        => await DbSet.Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync(ct);
}
