using Microsoft.EntityFrameworkCore;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Infrastructure.Repositories;

/// <summary>
/// Gera o próximo número para uma filial e prefixo usando
/// INSERT...ON CONFLICT DO UPDATE...RETURNING — atômico no PostgreSQL,
/// sem race condition mesmo sob alta concorrência.
///
/// SQL cru não passa pelos filtros globais do EF, então a filial entra
/// explicitamente aqui. O alvo do ON CONFLICT é a chave composta
/// (branch_id, prefix), e a atomicidade se mantém: ON CONFLICT sobre índice
/// único composto é tão atômico quanto sobre coluna única.
/// </summary>
public class NumberSequenceGenerator(StoctableDbContext context, BranchContext branchContext)
{
    public async Task<long> NextAsync(string prefix, CancellationToken ct = default)
    {
        var branchId = branchContext.BranchId;

        var values = await context.Database
            .SqlQuery<long>($@"
                INSERT INTO number_sequences (branch_id, prefix, current_value, updated_at)
                VALUES ({branchId}, {prefix}, 1, NOW())
                ON CONFLICT (branch_id, prefix) DO UPDATE
                   SET current_value = number_sequences.current_value + 1,
                       updated_at    = NOW()
                RETURNING current_value AS ""Value""")
            .ToListAsync(ct);

        return values.Single();
    }
}
