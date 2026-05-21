using Microsoft.EntityFrameworkCore;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

/// <summary>
/// Gera o próximo número para um prefixo (ex: ORC202605) usando
/// INSERT...ON CONFLICT DO UPDATE...RETURNING — atômico no PostgreSQL,
/// sem race condition mesmo sob alta concorrência.
/// </summary>
public class NumberSequenceGenerator(StoctableDbContext context)
{
    public async Task<long> NextAsync(string prefix, CancellationToken ct = default)
    {
        var values = await context.Database
            .SqlQuery<long>($@"
                INSERT INTO number_sequences (prefix, current_value, updated_at)
                VALUES ({prefix}, 1, NOW())
                ON CONFLICT (prefix) DO UPDATE
                   SET current_value = number_sequences.current_value + 1,
                       updated_at    = NOW()
                RETURNING current_value AS ""Value""")
            .ToListAsync(ct);

        return values.Single();
    }
}
