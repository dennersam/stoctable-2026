using Npgsql;

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
public class ControlPlaneVerification(string controlPlaneConnStr, string tenantConnStr)
{
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

        return divergencias == 0 && semLinha == 0;
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
