using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class AccountRepository(ControlPlaneDbContext context) : IAccountRepository
{
    public async Task<Account?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Accounts
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.Email == Normalize(email), ct);

    public async Task<Account?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await context.Accounts
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.RefreshTokenHash == tokenHash, ct);

    public async Task<Account?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await context.Accounts
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.PasswordResetTokenHash == tokenHash, ct);

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Accounts
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Account>> ListByCompanyAsync(Guid companyId, CancellationToken ct = default)
        => await context.Accounts
            .Where(a => a.CompanyId == companyId)
            .OrderBy(a => a.FullName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Branch>> GetBranchesAsync(Guid accountId, CancellationToken ct = default)
        => await context.AccountBranches
            .Where(ab => ab.AccountId == accountId)
            .Select(ab => ab.Branch!)
            .Where(b => b.IsActive)
            .OrderByDescending(b => b.IsHeadquarters)
            .ThenBy(b => b.Code)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Branch>> ListCompanyBranchesAsync(Guid companyId, CancellationToken ct = default)
        => await context.Branches
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .OrderByDescending(b => b.IsHeadquarters)
            .ThenBy(b => b.Code)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await context.Accounts.AnyAsync(a => a.Email == Normalize(email), ct);

    public async Task<bool> UsernameExistsAsync(Guid companyId, string username, CancellationToken ct = default)
        => await context.Accounts.AnyAsync(a => a.CompanyId == companyId && a.Username == username, ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
    {
        account.Email = Normalize(account.Email);
        context.Accounts.Add(account);
        await context.SaveChangesAsync(ct);
    }

    public async Task ReplaceBranchesAsync(Guid accountId, IEnumerable<Guid> branchIds, CancellationToken ct = default)
    {
        var atuais = await context.AccountBranches
            .Where(ab => ab.AccountId == accountId)
            .ToListAsync(ct);

        context.AccountBranches.RemoveRange(atuais);

        foreach (var branchId in branchIds.Distinct())
            context.AccountBranches.Add(new AccountBranch { AccountId = accountId, BranchId = branchId });

        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    /// <summary>
    /// E-mail é guardado em minúsculas; normalizar em todo acesso evita que
    /// "Fulano@x" e "fulano@x" virem duas contas na prática.
    /// </summary>
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
