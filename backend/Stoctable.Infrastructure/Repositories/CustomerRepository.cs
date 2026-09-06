using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Search;

namespace Stoctable.Infrastructure.Repositories;

public class CustomerRepository(StoctableDbContext context) : Repository<Customer>(context), ICustomerRepository
{
    private static readonly Expression<Func<Customer, string?>>[] SearchFields =
    [
        c => c.FullName,
        c => c.DocumentNumber,
        c => c.Phone,
        c => c.Mobile,
        c => c.City,
    ];

    public async Task<Customer?> GetByDocumentAsync(string documentNumber, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(c => c.DocumentNumber == documentNumber, ct);

    public async Task<IEnumerable<Customer>> SearchAsync(string query, CancellationToken ct = default)
        => await DbSet
            .Where(c => c.IsActive)
            .WhereMatchesAllTokens(query, SearchFields)
            .Include(c => c.CustomerType)
            .OrderBy(c => c.FullName)
            .Take(50)
            .ToListAsync(ct);

    public async Task<(IEnumerable<Customer> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = DbSet.Include(c => c.CustomerType).AsQueryable();

        query = query.WhereMatchesAllTokens(search, SearchFields);

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
