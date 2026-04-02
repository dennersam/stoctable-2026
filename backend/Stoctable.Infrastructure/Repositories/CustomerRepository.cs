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

    public async Task<(IEnumerable<Customer> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = DbSet.Include(c => c.CustomerType).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(lower) ||
                (c.DocumentNumber != null && c.DocumentNumber.Contains(lower)) ||
                (c.Phone != null && c.Phone.Contains(lower)) ||
                (c.Mobile != null && c.Mobile.Contains(lower)) ||
                (c.City != null && c.City.ToLower().Contains(lower)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Customer?> GetWithCrmNotesAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(c => c.CrmNotes.OrderByDescending(n => n.CreatedAt))
            .Include(c => c.CustomerType)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
}
