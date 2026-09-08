using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class UserProjectionWriter(StoctableDbContext context) : IUserProjectionWriter
{
    public async Task UpsertAsync(Account account, CancellationToken ct = default)
    {
        await UpsertCoreAsync(account, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<int> ResyncAsync(IEnumerable<Account> accounts, CancellationToken ct = default)
    {
        var total = 0;
        foreach (var account in accounts)
        {
            await UpsertCoreAsync(account, ct);
            total++;
        }

        await context.SaveChangesAsync(ct);
        return total;
    }

    private async Task UpsertCoreAsync(Account account, CancellationToken ct)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == account.Id, ct);

        if (user is null)
        {
            user = new User
            {
                // Mesmo Id da conta: é o que mantém audit_logs e o vendedor da
                // venda apontando para o identificador certo.
                Id = account.Id,
                CreatedBy = "projection",
            };
            context.Users.Add(user);
        }

        user.Username = account.Username;
        user.Email = account.Email;
        user.FullName = account.FullName;
        user.Role = account.Role;
        user.IsActive = account.IsActive;
        user.AvatarUrl = account.AvatarUrl;
        user.LastLoginAt = account.LastLoginAt;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = "projection";
    }
}
