using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Migration;

/// <summary>
/// Corte final do estoque: <c>products.stock_quantity</c> deixa de ser a fonte
/// da verdade e <c>product_stocks</c> assume sozinha.
///
/// <para>
/// <b>Por que isto não é uma migration EF.</b> O saldo antigo tem que ir para a
/// filial MEGA, e o id dela é por empresa e vive no control plane — outro banco.
/// Uma migration do tenant não tem como conhecê-lo, ao contrário do que se fez
/// com <c>LegacySingleBranchId</c>, que era um literal fixo. Este runner já roda
/// por tenant e já alcança o control plane, então é o lugar certo.
/// </para>
///
/// <para>
/// <b>Ordem obrigatória, e ela importa:</b>
/// <list type="number">
///   <item>este comando com <c>apply</c>, que faz o backfill;</item>
///   <item>este comando sem argumento (verificação), que precisa fechar em zero
///         divergências <b>em produção</b>;</item>
///   <item>só então a migration que derruba as colunas — em outro deploy, com
///         pelo menos um ciclo de produção de intervalo.</item>
/// </list>
/// O passo 3 é irreversível na prática: depois dele não existe mais com o que
/// comparar.
/// </para>
///
/// <para>
/// <b>Momento certo de rodar o passo 1:</b> no deploy que sobe o código novo.
/// Antes disso o dual-write ainda mantinha as duas fontes próximas; depois
/// disso <c>products.stock_quantity</c> congela e passa a divergir de forma
/// legítima, à medida que as lojas movimentam estoque. Rodar o backfill tarde
/// demais significa sobrescrever saldo novo com retrato velho.
/// </para>
/// </summary>
public class StockCutover(string controlPlaneConnStr, string tenantConnStr)
{
    private const string StockOwnerCode = "MEGA";

    /// <summary>
    /// Produtos cujo saldo antigo não bate com a linha da filial dona.
    ///
    /// Público porque é exercitado por teste de integração: este SQL e o de
    /// backfill são o passo irreversível da transição, e o fixture da suíte usa
    /// EnsureCreated — ou seja, nunca roda migrations. Sem um teste que aplique
    /// estes comandos sobre dados no formato antigo, eles só seriam validados em
    /// produção, que é o único lugar onde não dá para errar.
    /// </summary>
    public const string DivergenceSql =
        """
        SELECT p.sku, p.name, p.stock_quantity, ps.quantity
          FROM products p
          LEFT JOIN product_stocks ps
                 ON ps.product_id = p.id AND ps.branch_id = @owner
         WHERE COALESCE(ps.quantity, 0) <> p.stock_quantity
         ORDER BY p.sku
        """;

    /// <summary>
    /// Cria a linha que falta e corrige a que divergiu, sempre na filial dona.
    ///
    /// O <c>reserved</c> vem junto porque reserva de orçamento aberto também é
    /// da loja — perdê-la liberaria para venda peça já comprometida. Já o
    /// <c>minimum</c> só preenche o que está zerado: ele pode ter sido ajustado
    /// por filial depois da migration que criou a coluna, e sobrescrever isso
    /// desfaria configuração deliberada.
    /// </summary>
    public const string BackfillSql =
        """
        INSERT INTO product_stocks
            (id, branch_id, product_id, quantity, reserved, minimum, created_at, created_by)
        SELECT gen_random_uuid(), @owner, p.id,
               p.stock_quantity, p.stock_reserved, p.stock_minimum, NOW(), 'cutover'
          FROM products p
        ON CONFLICT (branch_id, product_id) DO UPDATE
           SET quantity   = EXCLUDED.quantity,
               reserved   = EXCLUDED.reserved,
               minimum    = CASE WHEN product_stocks.minimum = 0
                                 THEN EXCLUDED.minimum
                                 ELSE product_stocks.minimum END,
               updated_at = NOW(),
               updated_by = 'cutover'
        """;

    /// <summary>
    /// Um produto cujo saldo antigo não bate com a linha da filial dona.
    /// </summary>
    private record Divergence(string Sku, string Name, decimal Legacy, decimal? Current);

    /// <returns><c>true</c> se, ao final, não há divergência.</returns>
    public async Task<bool> RunAsync(bool apply)
    {
        var ownerId = await ResolveStockOwnerAsync();

        await using var tenant = new NpgsqlConnection(tenantConnStr);
        await tenant.OpenAsync();

        await WarnIfOtherBranchesHaveStockAsync(tenant, ownerId, apply);

        var before = await FindDivergencesAsync(tenant, ownerId);

        if (before.Count == 0)
        {
            Ok("✓ Nenhuma divergência: product_stocks já reflete o saldo antigo.");
            return true;
        }

        Log($"→ {before.Count} produto(s) com saldo divergente ou sem linha na filial dona.");
        foreach (var d in before.Take(20))
            Log($"    {d.Sku,-12} {Truncate(d.Name, 34),-34} antigo={d.Legacy,10:N3}  atual={FormatCurrent(d.Current),10}");

        if (before.Count > 20)
            Log($"    … e mais {before.Count - 20}.");

        if (!apply)
        {
            Warn("\n⚠ Verificação apenas. Rode com 'apply' para gravar o backfill.");
            return false;
        }

        Log("\n→ Aplicando backfill para a filial dona...");
        var affected = await ApplyBackfillAsync(tenant, ownerId);
        Log($"    {affected} linha(s) criadas ou corrigidas.");

        var after = await FindDivergencesAsync(tenant, ownerId);
        if (after.Count > 0)
        {
            Warn($"✗ Ainda restam {after.Count} divergência(s) depois do backfill. NÃO derrube as colunas.");
            return false;
        }

        Ok("✓ Backfill concluído e conferido: as duas fontes batem.");
        Log("  Deixe rodar ao menos um ciclo de produção antes da migration que derruba as colunas.");
        return true;
    }

    private async Task<Guid> ResolveStockOwnerAsync()
    {
        var opts = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(controlPlaneConnStr, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory_ControlPlane"))
            .Options;

        await using var control = new ControlPlaneDbContext(opts);

        // A filial dona é a matriz da empresa — é dela que veio todo o estoque
        // migrado do SIC. Se um dia houver mais de uma empresa neste control
        // plane, a busca precisa passar a receber o CNPJ.
        var branch = await control.Branches
            .Where(b => b.Code == StockOwnerCode)
            .OrderBy(b => b.CreatedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"Filial '{StockOwnerCode}' não encontrada no control plane. "
                + "Rode 'backfill' antes deste comando.");

        Log($"→ Filial dona do saldo antigo: {branch.NomeFantasia} ({branch.Code}) — {branch.Id}");
        return branch.Id;
    }

    /// <summary>
    /// Se as outras lojas já movimentaram estoque, o retrato de
    /// <c>products.stock_quantity</c> é anterior a essa operação e sobrescrever
    /// a MEGA com ele pode desfazer trabalho real. Não bloqueia — só avisa, alto.
    /// </summary>
    private static async Task WarnIfOtherBranchesHaveStockAsync(
        NpgsqlConnection tenant, Guid ownerId, bool apply)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT COUNT(*) FROM product_stocks
             WHERE branch_id <> @owner AND (quantity <> 0 OR reserved <> 0)
            """, tenant);
        cmd.Parameters.AddWithValue("owner", ownerId);

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        if (count == 0) return;

        Warn($"⚠ {count} linha(s) de estoque em OUTRAS filiais já têm saldo.");
        Warn("  Isso indica que o sistema novo já está em uso. O saldo antigo de");
        Warn("  products é um retrato anterior a essas movimentações.");
        if (apply)
            Warn("  O backfill abaixo só toca a filial dona — confira o relatório antes de seguir.");
    }

    /// <summary>
    /// Produto zerado dos dois lados não conta: linha ausente com saldo zero é
    /// exatamente o que se espera de item nunca movimentado.
    /// </summary>
    private static async Task<List<Divergence>> FindDivergencesAsync(
        NpgsqlConnection tenant, Guid ownerId)
    {
        await using var cmd = new NpgsqlCommand(DivergenceSql, tenant);
        cmd.Parameters.AddWithValue("owner", ownerId);

        var found = new List<Divergence>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found.Add(new Divergence(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3)));
        }

        return found;
    }

    private static async Task<int> ApplyBackfillAsync(NpgsqlConnection tenant, Guid ownerId)
    {
        await using var tx = await tenant.BeginTransactionAsync();

        await using var cmd = new NpgsqlCommand(BackfillSql, tenant, tx);
        cmd.Parameters.AddWithValue("owner", ownerId);

        var rows = await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return rows;
    }

    private static string FormatCurrent(decimal? value)
        => value is null ? "(sem linha)" : value.Value.ToString("N3");

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static void Log(string message) => Console.WriteLine(message);

    private static void Ok(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
