using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Stoctable.Migration;

public class MigrationRunner(string sicConnStr, string pgConnStr)
{
    // ── ID mapping: SIC controle (int) → Stoctable UUID ──────────────────────
    private readonly Dictionary<int, Guid> _supplierMap = new();
    private readonly Dictionary<int, Guid> _productMap = new();
    private readonly Dictionary<int, Guid> _customerMap = new();
    private readonly Dictionary<int, Guid> _userMap = new();
    private readonly Dictionary<string, Guid> _manufacturerMap = new(StringComparer.OrdinalIgnoreCase);

    public async Task RunAsync()
    {
        await using var sic = new SqlConnection(sicConnStr);
        await using var pg = new NpgsqlConnection(pgConnStr);

        await sic.OpenAsync();
        await pg.OpenAsync();

        Log("Conexões abertas. Iniciando migração...\n");

        await MigrateDefaultCategory(pg);
        await MigrateSuppliers(sic, pg);
        await MigrateManufacturers(sic, pg);
        await MigrateProducts(sic, pg);
        await MigrateCustomers(sic, pg);
        await MigrateUsers(sic, pg);

        Log("\n✓ Migração concluída.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Categoria padrão
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task MigrateDefaultCategory(NpgsqlConnection pg)
    {
        Log("→ Criando categoria padrão 'Geral'...");

        const string sql = """
            INSERT INTO product_categories (id, name, parent_id, is_active, created_at, created_by)
            VALUES (@id, 'Geral', NULL, true, NOW(), 'migration')
            ON CONFLICT DO NOTHING
            """;

        // Check if any category exists first
        var count = (long)(await ScalarAsync(pg, "SELECT COUNT(*) FROM product_categories"))!;
        if (count > 0)
        {
            Log("  categoria já existe, ignorando.");
            return;
        }

        await using var cmd = new NpgsqlCommand(sql, pg);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        await cmd.ExecuteNonQueryAsync();
        Log("  criada.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Fornecedores  TABFOR → suppliers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task MigrateSuppliers(SqlConnection sic, NpgsqlConnection pg)
    {
        Log("→ Migrando fornecedores (TABFOR)...");

        var rows = await ReadSicAsync(sic, "SELECT controle, codigo, empresa, contato, endereco, bairro, cidade, estado, cep, telefone, cgc, email, obs FROM TABFOR", r =>
            new SicSupplier(
                r.GetInt32("controle"),
                Str(r, "codigo"),
                Str(r, "empresa"),
                Str(r, "contato"),
                Str(r, "endereco"),
                Str(r, "bairro"),
                Str(r, "cidade"),
                Str(r, "estado"),
                Str(r, "cep"),
                Str(r, "telefone"),
                Str(r, "cgc"),
                Str(r, "email"),
                Str(r, "obs")
            ));

        const string sql = """
            INSERT INTO suppliers
              (id, company_name, trade_name, cnpj, address, phone, email, contact_person, is_active, notes, created_at, created_by)
            VALUES
              (@id, @company_name, NULL, @cnpj, @address, @phone, @email, @contact_person, true, @notes, NOW(), 'migration')
            ON CONFLICT DO NOTHING
            """;

        int count = 0;
        foreach (var s in rows)
        {
            if (string.IsNullOrWhiteSpace(s.Empresa)) continue;

            var id = Guid.NewGuid();
            _supplierMap[s.Controle] = id;

            await using var cmd = new NpgsqlCommand(sql, pg);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("company_name", (object?)Truncate(s.Empresa, 200) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cnpj", (object?)Truncate(s.Cgc, 18) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("address", (object?)BuildAddress(s.Endereco, s.Bairro, s.Cidade, s.Estado, s.Cep) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("phone", (object?)Truncate(s.Telefone, 30) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email", (object?)Truncate(s.Email, 150) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("contact_person", (object?)Truncate(s.Contato, 100) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("notes", (object?)s.Obs ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            count++;
        }

        Log($"  {count} fornecedor(es) migrado(s).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Fabricantes  TABEST1.fabricante → manufacturers  (deduplicação)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task MigrateManufacturers(SqlConnection sic, NpgsqlConnection pg)
    {
        Log("→ Deduplicando fabricantes de TABEST1...");

        var names = await ReadSicAsync(sic,
            "SELECT DISTINCT LTRIM(RTRIM(fabricante)) AS fabricante FROM TABEST1 WHERE fabricante IS NOT NULL AND LTRIM(RTRIM(fabricante)) <> ''",
            r => Str(r, "fabricante")!);

        const string sql = """
            INSERT INTO manufacturers (id, name, notes, is_active, created_at, created_by)
            VALUES (@id, @name, NULL, true, NOW(), 'migration')
            ON CONFLICT DO NOTHING
            """;

        int count = 0;
        foreach (var name in names)
        {
            var id = Guid.NewGuid();
            _manufacturerMap[name] = id;

            await using var cmd = new NpgsqlCommand(sql, pg);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("name", Truncate(name, 100)!);
            await cmd.ExecuteNonQueryAsync();
            count++;
        }

        Log($"  {count} fabricante(s) criado(s).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Produtos  TABEST1 → products
    // ─────────────────────────────────────────────────────────────────────────

    private async Task MigrateProducts(SqlConnection sic, NpgsqlConnection pg)
    {
        Log("→ Migrando produtos (TABEST1)...");

        var rows = await ReadSicAsync(sic, """
            SELECT controle, codigo, produto, fabricante, lkfornec,
                   precocusto, precovenda, quantidade, estminimo, unidade,
                   inativo, obs, cean, ipi, icms, cst
            FROM TABEST1
            """, r => new SicProduct(
                r.GetInt32("controle"),
                Str(r, "codigo"),
                Str(r, "produto"),
                Str(r, "fabricante"),
                r.IsDBNull("lkfornec") ? null : r.GetInt32("lkfornec"),
                r.IsDBNull("precocusto") ? null : (decimal)r.GetDecimal("precocusto"),
                r.IsDBNull("precovenda") ? null : (decimal)r.GetDecimal("precovenda"),
                r.IsDBNull("quantidade") ? null : r.GetDouble("quantidade"),
                r.IsDBNull("estminimo") ? null : r.GetDouble("estminimo"),
                Str(r, "unidade"),
                r.IsDBNull("inativo") ? null : r.GetBoolean("inativo"),
                Str(r, "obs"),
                Str(r, "cean"),
                r.IsDBNull("ipi") ? null : r.GetDouble("ipi"),
                r.IsDBNull("icms") ? null : r.GetDouble("icms"),
                Str(r, "cst")
            ));

        // Fetch the default category id
        var categoryId = (Guid?)(await ScalarAsync(pg, "SELECT id FROM product_categories LIMIT 1"));

        const string sql = """
            INSERT INTO products
              (id, sku, barcode, name, manufacturer_id, category_id, supplier_id,
               cost_price, sale_price, stock_quantity, stock_reserved, stock_minimum,
               unit, ipi_rate, icms_rate, cst, is_active, notes, created_at, created_by)
            VALUES
              (@id, @sku, @barcode, @name, @manufacturer_id, @category_id, @supplier_id,
               @cost_price, @sale_price, @stock_quantity, 0, @stock_minimum,
               @unit, @ipi_rate, @icms_rate, @cst, @is_active, @notes, NOW(), 'migration')
            ON CONFLICT (sku) DO NOTHING
            """;

        int count = 0, skipped = 0;
        foreach (var p in rows)
        {
            if (string.IsNullOrWhiteSpace(p.Produto)) { skipped++; continue; }

            var sku = string.IsNullOrWhiteSpace(p.Codigo)
                ? $"SIC{p.Controle}"
                : p.Codigo.Trim();

            var id = Guid.NewGuid();
            _productMap[p.Controle] = id;

            Guid? manufacturerId = null;
            if (!string.IsNullOrWhiteSpace(p.Fabricante) &&
                _manufacturerMap.TryGetValue(p.Fabricante.Trim(), out var mId))
                manufacturerId = mId;

            Guid? supplierId = null;
            if (p.LkFornec.HasValue && _supplierMap.TryGetValue(p.LkFornec.Value, out var sId))
                supplierId = sId;

            await using var cmd = new NpgsqlCommand(sql, pg);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("sku", Truncate(sku, 50)!);
            cmd.Parameters.AddWithValue("barcode", (object?)Truncate(p.Cean, 50) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("name", Truncate(p.Produto, 255)!);
            cmd.Parameters.AddWithValue("manufacturer_id", (object?)manufacturerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("category_id", (object?)categoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("supplier_id", (object?)supplierId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cost_price", (decimal)(p.PrecoCusto ?? 0));
            cmd.Parameters.AddWithValue("sale_price", (decimal)(p.PrecoVenda ?? 0));
            cmd.Parameters.AddWithValue("stock_quantity", (decimal)(p.Quantidade ?? 0));
            cmd.Parameters.AddWithValue("stock_minimum", (decimal)(p.EstMinimo ?? 0));
            cmd.Parameters.AddWithValue("unit", Truncate(p.Unidade, 10) ?? "UN");
            cmd.Parameters.AddWithValue("ipi_rate", p.Ipi.HasValue ? (object)(decimal)p.Ipi : DBNull.Value);
            cmd.Parameters.AddWithValue("icms_rate", p.Icms.HasValue ? (object)(decimal)p.Icms : DBNull.Value);
            cmd.Parameters.AddWithValue("cst", (object?)Truncate(p.Cst, 10) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("is_active", !(p.Inativo ?? false));
            cmd.Parameters.AddWithValue("notes", (object?)p.Obs ?? DBNull.Value);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // unique_violation
            {
                // SKU duplicado: prefixar com controle
                cmd.Parameters["sku"].Value = $"SIC{p.Controle}";
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
        }

        Log($"  {count} produto(s) migrado(s). {skipped} ignorado(s) (nome vazio).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Clientes  TABCLI → customers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task MigrateCustomers(SqlConnection sic, NpgsqlConnection pg)
    {
        Log("→ Migrando clientes (TABCLI)...");

        var rows = await ReadSicAsync(sic, """
            SELECT controle, nome, cgc, tagfisica, endereco, endnumero, endcomplemento,
                   bairro, cidade, estado, cep, telefone, celular, email,
                   limitecred, atendbloq, obs, contato
            FROM TABCLI
            """, r => new SicCustomer(
                r.GetInt32("controle"),
                Str(r, "nome"),
                Str(r, "cgc"),
                r.IsDBNull("tagfisica") ? null : r.GetBoolean("tagfisica"),
                Str(r, "endereco"),
                Str(r, "endnumero"),
                Str(r, "endcomplemento"),
                Str(r, "bairro"),
                Str(r, "cidade"),
                Str(r, "estado"),
                Str(r, "cep"),
                Str(r, "telefone"),
                Str(r, "celular"),
                Str(r, "email"),
                r.IsDBNull("limitecred") ? null : (decimal)r.GetDecimal("limitecred"),
                r.IsDBNull("atendbloq") ? null : r.GetBoolean("atendbloq"),
                Str(r, "obs"),
                Str(r, "contato")
            ));

        const string sql = """
            INSERT INTO customers
              (id, full_name, document_type, document_number, email, phone, mobile,
               address, city, state, zip_code, credit_limit, is_active, notes,
               created_at, created_by)
            VALUES
              (@id, @full_name, @document_type, @document_number, @email, @phone, @mobile,
               @address, @city, @state, @zip_code, @credit_limit, @is_active, @notes,
               NOW(), 'migration')
            ON CONFLICT (document_number) DO NOTHING
            """;

        int count = 0, skipped = 0;
        foreach (var c in rows)
        {
            if (string.IsNullOrWhiteSpace(c.Nome)) { skipped++; continue; }

            var id = Guid.NewGuid();
            _customerMap[c.Controle] = id;

            // Monta endereço completo
            var address = BuildCustomerAddress(c.Endereco, c.EndNumero, c.EndComplemento);

            // Concatena observação + contato quando relevante
            var notes = MergeNotes(c.Obs, c.Contato.HasValue() ? $"Contato: {c.Contato}" : null);

            // document_number: índice único → null se vazio para evitar conflito
            var docNumber = string.IsNullOrWhiteSpace(c.Cgc) ? null : Truncate(c.Cgc, 18);

            await using var cmd = new NpgsqlCommand(sql, pg);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("full_name", Truncate(c.Nome, 150)!);
            cmd.Parameters.AddWithValue("document_type", (c.TagFisica ?? true) ? "CPF" : "CNPJ");
            cmd.Parameters.AddWithValue("document_number", (object?)docNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email", (object?)Truncate(c.Email, 150) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("phone", (object?)Truncate(c.Telefone, 20) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("mobile", (object?)Truncate(c.Celular, 20) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("address", (object?)Truncate(address, 255) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("city", (object?)Truncate(c.Cidade, 100) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("state", (object?)Truncate(c.Estado, 2) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("zip_code", (object?)Truncate(c.Cep, 10) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("credit_limit", c.LimiteCred ?? 0m);
            cmd.Parameters.AddWithValue("is_active", !(c.AtendBloq ?? false));
            cmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                // Documento duplicado: inserir sem documento
                cmd.Parameters["document_number"].Value = DBNull.Value;
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
        }

        Log($"  {count} cliente(s) migrado(s). {skipped} ignorado(s) (nome vazio).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Usuários  TABUSER → users
    // ─────────────────────────────────────────────────────────────────────────

    private async Task MigrateUsers(SqlConnection sic, NpgsqlConnection pg)
    {
        Log("→ Migrando usuários (TABUSER)...");

        var rows = await ReadSicAsync(sic,
            "SELECT controle, nome, senha, nivel, inativo FROM TABUSER",
            r => new SicUser(
                r.GetInt32("controle"),
                Str(r, "nome"),
                Str(r, "senha"),
                r.IsDBNull("nivel") ? null : r.GetInt32("nivel"),
                r.IsDBNull("inativo") ? null : r.GetBoolean("inativo")
            ));

        const string sql = """
            INSERT INTO users
              (id, username, email, password_hash, full_name, role, is_active, created_at, created_by)
            VALUES
              (@id, @username, @email, @password_hash, @full_name, @role, @is_active, NOW(), 'migration')
            ON CONFLICT (username) DO NOTHING
            """;

        int count = 0;
        foreach (var u in rows)
        {
            if (string.IsNullOrWhiteSpace(u.Nome)) continue;

            var id = Guid.NewGuid();
            _userMap[u.Controle] = id;

            var username = SanitizeUsername(u.Nome);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(u.Senha ?? "Troca@123");
            var role = u.Nivel == 1 ? "admin" : "atendente";
            // email fictício: usuário não tinha email no SIC 6
            var email = $"{username}@migrado.local";

            await using var cmd = new NpgsqlCommand(sql, pg);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("username", username);
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("password_hash", passwordHash);
            cmd.Parameters.AddWithValue("full_name", Truncate(u.Nome, 150)!);
            cmd.Parameters.AddWithValue("role", role);
            cmd.Parameters.AddWithValue("is_active", !(u.Inativo ?? false));

            try
            {
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                // username duplicado: adicionar sufixo numérico
                cmd.Parameters["username"].Value = $"{username}_{u.Controle}";
                cmd.Parameters["email"].Value = $"{username}_{u.Controle}@migrado.local";
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
        }

        Log($"  {count} usuário(s) migrado(s).");
        Log("  ⚠  As senhas foram migradas como hash BCrypt da senha original do SIC 6.");
        Log("     Usuários com senha vazia receberam 'Troca@123' como senha temporária.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<List<T>> ReadSicAsync<T>(SqlConnection conn, string sql, Func<SqlDataReader, T> map)
    {
        var result = new List<T>();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(map(reader));
        return result;
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync();
    }

    private static string? Str(SqlDataReader r, string col)
    {
        var val = r.IsDBNull(col) ? null : r.GetString(col).Trim();
        return string.IsNullOrEmpty(val) ? null : val;
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : (s.Length > max ? s[..max] : s);

    private static string? BuildAddress(string? endereco, string? bairro, string? cidade, string? estado, string? cep)
    {
        var parts = new[] { endereco, bairro, cidade, estado, cep }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var result = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string? BuildCustomerAddress(string? endereco, string? numero, string? complemento)
    {
        var parts = new[] { endereco, numero, complemento }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var result = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string? MergeNotes(params string?[] parts)
    {
        var result = string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string SanitizeUsername(string nome)
    {
        // Normaliza: minúsculo, remove acentos, substitui espaços por ponto
        var s = nome.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in s)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString()
            .ToLowerInvariant()
            .Replace(' ', '.')
            .Replace("'", "")
            .TrimEnd('.');
    }

    private static void Log(string msg) => Console.WriteLine(msg);
}

internal static class StringExtensions
{
    public static bool HasValue(this string? s) => !string.IsNullOrWhiteSpace(s);
}
