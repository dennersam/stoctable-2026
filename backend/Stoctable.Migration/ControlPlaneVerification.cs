using Npgsql;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Migration;

/// <summary>
/// Confere o estado dos dois bancos depois do backfill e, principalmente, faz a
/// reconciliação entre <c>products.stock_quantity</c> e <c>product_stocks</c>.
///
/// Enquanto as duas fontes de estoque convivem, esta reconciliação é o que prova
/// que nenhum caminho de escrita ficou de fora. O plano prevê rodá-la
/// diariamente por uma semana antes de derrubar as colunas antigas: qualquer
/// divergência significa que alguma escrita mexeu numa fonte e não na outra.
/// </summary>
public class ControlPlaneVerification(
    string controlPlaneConnStr,
    string tenantConnStr,
    IConnectionStringProtector protector)
{
    /// <summary>
    /// Tabelas que ganharam <c>branch_id</c> na fase 3. Linha sobrando no id
    /// legado é linha invisível: o filtro global não a alcança, então ela some
    /// da aplicação sem nenhum erro.
    /// </summary>
    private static readonly string[] BranchScopedTables =
    [
        "product_stocks", "stock_reservations", "sales", "quotations",
        "payments", "inventory_movements", "audit_logs", "number_sequences",
    ];

    private const string LegacyBranchId = "00000000-0000-0000-0000-000000000001";

    public async Task<bool> RunAsync()
    {
        await using var control = new NpgsqlConnection(controlPlaneConnStr);
        await control.OpenAsync();

        await using var tenant = new NpgsqlConnection(tenantConnStr);
        await tenant.OpenAsync();

        Section("Empresa");
        await PrintAsync(control, """
            SELECT razao_social, nome_fantasia, cnpj, status, database_name,
                   (SELECT count(*) FROM accounts a WHERE a.company_id = c.id) AS contas
              FROM companies c
            """);

        Section("Filiais");
        await PrintAsync(control, """
            SELECT b.code, b.nome_fantasia, b.cnpj, b.is_headquarters,
                   (SELECT count(*) FROM account_branches ab WHERE ab.branch_id = b.id) AS contas
              FROM branches b
             ORDER BY b.is_headquarters DESC, b.code
            """);

        Section("Contas de login");
        await PrintAsync(control, """
            SELECT username, email, role, is_active FROM accounts ORDER BY username
            """);

        Section("Connection string cifrada");
        var okCrypto = await VerifyEncryptedConnectionAsync(control);

        Section("Escopo de filial nas tabelas do tenant");
        var okEscopo = await VerifyBranchScopeAsync(control, tenant);

        Section("Estoque");
        await PrintAsync(tenant, """
            SELECT (SELECT count(*) FROM products)                        AS produtos,
                   (SELECT count(*) FROM product_stocks)                  AS linhas_estoque,
                   (SELECT count(DISTINCT branch_id) FROM product_stocks) AS filiais_distintas,
                   (SELECT count(*) FROM product_stocks
                     WHERE branch_id = '00000000-0000-0000-0000-000000000001') AS ainda_no_id_legado
            """);

        Section("Reconciliação products x product_stocks");
        var divergencias = await ScalarAsync(tenant, """
            SELECT count(*)
              FROM products p
              JOIN product_stocks s ON s.product_id = p.id
             WHERE p.stock_quantity <> s.quantity
                OR p.stock_reserved <> s.reserved
            """);

        if (divergencias == 0)
        {
            Ok("  ✓ Nenhuma divergência — as duas fontes de estoque batem.");
        }
        else
        {
            Warn($"  ⚠ {divergencias} produto(s) divergem entre products e product_stocks:");
            await PrintAsync(tenant, """
                SELECT p.sku, p.name,
                       p.stock_quantity AS products_qtd, s.quantity AS stocks_qtd,
                       p.stock_reserved AS products_res, s.reserved AS stocks_res
                  FROM products p
                  JOIN product_stocks s ON s.product_id = p.id
                 WHERE p.stock_quantity <> s.quantity
                    OR p.stock_reserved <> s.reserved
                 ORDER BY p.sku
                 LIMIT 20
                """);
            Warn("  Cada linha acima é um caminho de escrita que mexeu numa fonte só.");
        }

        // Produto com saldo mas sem linha de estoque: ficaria invisível quando a
        // leitura migrar para product_stocks.
        var semLinha = await ScalarAsync(tenant, """
            SELECT count(*)
              FROM products p
             WHERE (p.stock_quantity <> 0 OR p.stock_reserved <> 0)
               AND NOT EXISTS (SELECT 1 FROM product_stocks s WHERE s.product_id = p.id)
            """);

        if (semLinha > 0)
            Warn($"  ⚠ {semLinha} produto(s) têm saldo em products mas nenhuma linha em product_stocks.");

        return divergencias == 0 && semLinha == 0 && okCrypto && okEscopo;
    }

    /// <summary>
    /// Confere que a connection string cifrada decifra e aponta para o mesmo
    /// banco que estamos usando. Sem isso, toda requisição autenticada responde
    /// 503 — e o erro só apareceria no primeiro login depois do deploy.
    /// </summary>
    private async Task<bool> VerifyEncryptedConnectionAsync(NpgsqlConnection control)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT razao_social, status, connection_string_encrypted FROM companies", control);
        await using var reader = await cmd.ExecuteReaderAsync();

        var tudoOk = true;

        while (await reader.ReadAsync())
        {
            var razao = reader.GetString(0);
            var status = reader.GetString(1);

            if (await reader.IsDBNullAsync(2))
            {
                Warn($"  ⚠ {razao}: sem connection string gravada — login vai falhar com 503.");
                tudoOk = false;
                continue;
            }

            var payload = (byte[])reader.GetValue(2);

            try
            {
                var decifrada = protector.Unprotect(payload);
                var banco = new NpgsqlConnectionStringBuilder(decifrada).Database;
                var esperado = new NpgsqlConnectionStringBuilder(tenantConnStr).Database;

                if (banco == esperado)
                    Ok($"  ✓ {razao} ({status}): decifra e aponta para '{banco}'");
                else
                {
                    Warn($"  ⚠ {razao}: decifra, mas aponta para '{banco}' e não '{esperado}'.");
                    tudoOk = false;
                }
            }
            catch (Exception ex)
            {
                Warn($"  ⚠ {razao}: NÃO decifra ({ex.GetType().Name}). "
                     + "A chave TenantConnectionEncryptionKey provavelmente mudou.");
                tudoOk = false;
            }
        }

        return tudoOk;
    }

    /// <summary>
    /// Nenhuma linha pode ter ficado no id de filial provisório usado pelas
    /// migrations — se ficou, ela existe no banco e é invisível na aplicação.
    /// </summary>
    private async Task<bool> VerifyBranchScopeAsync(NpgsqlConnection control, NpgsqlConnection tenant)
    {
        var filiais = new Dictionary<Guid, string>();
        await using (var cmd = new NpgsqlCommand("SELECT id, code FROM branches", control))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                filiais[reader.GetGuid(0)] = reader.GetString(1);
        }

        var tudoOk = true;

        foreach (var tabela in BranchScopedTables)
        {
            await using var cmd = new NpgsqlCommand(
                $"SELECT branch_id, count(*) FROM {tabela} GROUP BY branch_id ORDER BY count(*) DESC", tenant);
            await using var reader = await cmd.ExecuteReaderAsync();

            var partes = new List<string>();
            while (await reader.ReadAsync())
            {
                var branchId = reader.GetGuid(0);
                var total = reader.GetInt64(1);

                if (branchId == Guid.Parse(LegacyBranchId))
                {
                    partes.Add($"LEGADO={total}");
                    tudoOk = false;
                }
                else if (filiais.TryGetValue(branchId, out var code))
                {
                    partes.Add($"{code}={total}");
                }
                else
                {
                    partes.Add($"DESCONHECIDA({branchId})={total}");
                    tudoOk = false;
                }
            }

            var resumo = partes.Count == 0 ? "vazia" : string.Join(", ", partes);
            Console.WriteLine($"  {tabela,-20} {resumo}");
        }

        if (tudoOk)
            Ok("  ✓ Toda linha pertence a uma filial real — nenhuma sobrou no id provisório.");
        else
            Warn("  ⚠ Há linhas em filial provisória ou desconhecida: invisíveis na aplicação.");

        return tudoOk;
    }

    private static async Task PrintAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var campos = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var valor = reader.IsDBNull(i) ? "—" : reader.GetValue(i)?.ToString();
                campos.Add($"{reader.GetName(i)}={valor}");
            }
            Console.WriteLine("  " + string.Join("  |  ", campos));
        }
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static void Section(string titulo)
    {
        Console.WriteLine();
        Console.WriteLine($"== {titulo} ==");
    }

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
