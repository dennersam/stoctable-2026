using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts;
using Stoctable.Exceptions;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure;

public class UnitOfWork(StoctableDbContext context) : IUnitOfWork
{
    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        // Se já estamos numa transação (chamada aninhada), apenas executa
        // — o commit/rollback fica a cargo do chamador externo.
        if (context.Database.CurrentTransaction is not null)
            return await action(ct);

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(ct);
            throw new ConcurrencyConflictException(
                "Conflito de concorrência: outro processo modificou os mesmos dados.", ex);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await ExecuteInTransactionAsync<object?>(async c =>
        {
            await action(c);
            return null;
        }, ct);
    }
}
