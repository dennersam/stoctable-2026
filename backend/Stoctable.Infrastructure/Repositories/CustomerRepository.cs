using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class CustomerRepository(StoctableDbContext context) : Repository<Customer>(context), ICustomerRepository
{
    public async Task<Customer?> GetByDocumentAsync(string documentNumber, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(c => c.DocumentNumber == documentNumber, ct);

    public async Task<IEnumerable<Customer>> SearchAsync(string query, CancellationToken ct = default)
    {
        var lower = query.ToLower();
        return await DbSet
            .Where(c => c.IsActive && (
                c.FullName.ToLower().Contains(lower) ||
                (c.DocumentNumber != null && c.DocumentNumber.Contains(lower)) ||
                (c.Phone != null && c.Phone.Contains(lower)) ||
                (c.Mobile != null && c.Mobile.Contains(lower))))
            .Include(c => c.CustomerType)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<Customer?> GetWithCrmNotesAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(c => c.CrmNotes.OrderByDescending(n => n.CreatedAt))
            .Include(c => c.CustomerType)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
}
