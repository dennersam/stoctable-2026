using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;

namespace Stoctable.Migration;

/// <summary>
/// Importa as três lojas do SIC para um único banco Stoctable, preservando os
/// dados como estão hoje.
///
/// <para>
/// <b>Catálogo por loja, sem unificar.</b> O SIC não tem chave que identifique o
/// mesmo produto entre lojas: o código de barras está 100% vazio nas três e o
/// <c>Codigo</c> é sequência interna — dos 12.899 códigos que MEGA e MOTOPENHA
/// têm em comum, só metade tem o mesmo produto (o 1149 é "Parafuso Coroa
/// CG/ML/TITAN" numa loja e "Parafuso Coroa T.T125" na outra). Unificar por
/// código fundiria estoque de peças diferentes.
/// </para>
///
/// <para>
/// Então cada loja traz o catálogo dela inteiro, com SKU prefixado
/// (<c>MEGA-1214</c>), e o produto ganha linha em <c>product_stocks</c> apenas na
/// filial de origem — nas outras aparece com saldo zero. Isso cabe no modelo sem
/// mudança de schema, porque produto já é da empresa e estoque já é da filial. A
/// unificação do catálogo fica para depois, com curadoria humana.
/// </para>
///
/// <para>
/// <b>Cliente e fornecedor, ao contrário, são unificados</b> — ali existe chave
/// confiável (CPF/CNPJ). Quem não tem documento entra como registro próprio da
/// loja, porque não há como saber se é a mesma pessoa.
/// </para>
/// </summary>
public partial class SicMultiStoreImporter(IReadOnlyList<SicStoreSource> stores, string pgConnStr)
{
    // Unificados entre lojas, pela chave natural.
    private readonly Dictionary<string, Guid> _supplierByDoc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _customerByDoc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _manufacturerByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _paymentMethodByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _userByUsername = new(StringComparer.OrdinalIgnoreCase);

    // Por loja: o "controle" do SIC só é único dentro do banco de origem.
    private readonly Dictionary<(string Store, int Id), Guid> _product = [];
    private readonly Dictionary<(string Store, int Id), Guid> _supplier = [];
    private readonly Dictionary<(string Store, int Id), Guid> _customer = [];
    private readonly Dictionary<(string Store, int Id), Guid> _user = [];
    private readonly Dictionary<(string Store, int Id), Guid> _paymentMethod = [];

    private Guid _categoryId;

    /// <summary>
    /// Corte do histórico. Vendas e orçamentos anteriores a esta data não são
    /// importados.
    ///
    /// Existe por limite de armazenamento, não por desenho: o histórico completo
    /// das três lojas (2002→2026) ocupa 733 MB no Postgres, e o plano do Neon
    /// para neste projeto em 512 MB. O grosso é <c>sale_items</c> e <c>sales</c>,
    /// que sozinhos somam 503 MB. Cadastros, produtos e estoque cabem em ~76 MB.
    /// </summary>
    private DateTime? _since;

    public async Task RunAsync(bool includeHistory, DateTime? historySince = null)
    {
        _since = historySince;

        var started = DateTimeOffset.UtcNow;

        // Sem timeout: o COPY fica aberto enquanto o SQL Server ainda está
        // calculando os agregados de itens e pagamentos sobre centenas de
        // milhares de linhas. Com os 30s padrão do Npgsql, a carga morre com
        // "Exception while reading from stream" antes da primeira linha chegar.
        var builder = new NpgsqlConnectionStringBuilder(pgConnStr) { CommandTimeout = 0 };

        await using var pg = new NpgsqlConnection(builder.ConnectionString);
        await pg.OpenAsync();

        Log("═══ Importação multi-loja do SIC ═══\n");
        foreach (var s in stores)
            Log($"  {s.Code,-6} → filial {s.BranchId}");
        Log("");

        await CleanTargetAsync(pg);
        await EnsureDefaultCategoryAsync(pg);

        // Cadastros primeiro: produtos e documentos apontam para eles.
        await ImportSuppliersAsync(pg);
        await ImportManufacturersAsync(pg);
        await ImportCustomersAsync(pg);
        await ImportUsersAsync(pg);
        await ImportPaymentMethodsAsync(pg);

        foreach (var store in stores)
            await ImportProductsAsync(pg, store);

        if (includeHistory)
        {
            foreach (var store in stores)
            {
                await ImportSalesAsync(pg, store);
                await ImportQuotationsAsync(pg, store);
            }
        }
        else
        {
            Log("→ Histórico de vendas e orçamentos NÃO importado (--sem-historico).");
        }

        if (includeHistory && _since is not null)
            Log($"\n⚠ Histórico limitado a documentos de {_since:dd/MM/yyyy} em diante.");

        await SeedNumberSequencesAsync(pg);

        Log($"\n✓ Concluído em {(DateTimeOffset.UtcNow - started).TotalMinutes:N1} min");
        await PrintSummaryAsync(pg);
    }

    // ─── Limpeza ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Esvazia o destino com TRUNCATE, e não DELETE.
    ///
    /// A diferença não é performance: DELETE deixa as páginas antigas ocupadas
    /// até um VACUUM, e recarregar por cima soma o lixo ao dado novo. Numa
    /// primeira tentativa de carga isso levou o banco a 739 MB para 182 MB de
    /// dados reais — e estourou o teto de armazenamento do plano. TRUNCATE
    /// devolve o espaço imediatamente.
    /// </summary>
    private static async Task CleanTargetAsync(NpgsqlConnection pg)
    {
        Log("→ Limpando tabelas de destino...");

        string[] tabelas =
        [
            "stock_transfer_items", "stock_transfers",
            "payments", "sale_items", "sales",
            "stock_reservations", "quotation_items", "quotations",
            "inventory_movements", "product_stocks", "products",
            // users vem depois de sales/quotations: é referenciado por
            // salesperson_id e cashier_id.
            "customer_crm_notes", "customers", "users", "suppliers", "manufacturers",
            "product_categories", "payment_methods", "number_sequences",
            "audit_logs",
        ];

        // Num TRUNCATE único a ordem entre elas deixa de importar, e o CASCADE
        // alcança qualquer tabela dependente que não esteja na lista.
        await ExecAsync(pg, $"TRUNCATE TABLE {string.Join(", ", tabelas)} CASCADE");

        Log($"  {tabelas.Length} tabela(s) esvaziada(s).");
    }

    private async Task EnsureDefaultCategoryAsync(NpgsqlConnection pg)
    {
        _categoryId = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO product_categories (id, name, is_active, created_at, created_by)
            VALUES (@id, 'Geral', true, NOW(), 'import')
            """, pg);
        cmd.Parameters.AddWithValue("id", _categoryId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Fornecedores (unificados por CNPJ) ─────────────────────────────────────

    private async Task ImportSuppliersAsync(NpgsqlConnection pg)
    {
        Log("→ Fornecedores (TABFOR)...");
        var inseridos = 0;

        foreach (var store in stores)
        {
            await using var sic = await OpenSicAsync(store);
            await using var cmd = new SqlCommand(
                "SELECT controle, codigo, empresa, contato, endereco, bairro, cidade, estado, cep, telefone, cgc, email, obs FROM TABFOR", sic);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var controle = r.GetInt32(0);
                var nome = Str(r, 2);
                if (string.IsNullOrWhiteSpace(nome)) continue;

                var doc = OnlyDigits(Str(r, 10));

                // Documento é a chave de unificação. Sem ele, cada loja mantém o
                // seu — não dá para afirmar que dois "AUTO PEÇAS LTDA" sem CNPJ
                // são a mesma empresa.
                if (doc is not null && _supplierByDoc.TryGetValue(doc, out var existente))
                {
                    _supplier[(store.Code, controle)] = existente;
                    continue;
                }

                var id = Guid.NewGuid();
                _supplier[(store.Code, controle)] = id;
                if (doc is not null) _supplierByDoc[doc] = id;

                await using var ins = new NpgsqlCommand(
                    """
                    INSERT INTO suppliers (id, company_name, trade_name, cnpj, address, phone, email,
                                           contact_person, is_active, notes, created_at, created_by)
                    VALUES (@id, @nome, @nome2, @cnpj, @end, @tel, @mail, @contato, true, @obs, NOW(), 'import')
                    """, pg);
                ins.Parameters.AddWithValue("id", id);
                ins.Parameters.AddWithValue("nome", Cut(nome, 150)!);
                ins.Parameters.AddWithValue("nome2", (object?)Cut(Str(r, 1), 150) ?? DBNull.Value);
                ins.Parameters.AddWithValue("cnpj", (object?)Cut(doc, 18) ?? DBNull.Value);
                ins.Parameters.AddWithValue("end", (object?)Cut(Address(Str(r, 4), Str(r, 5), Str(r, 6), Str(r, 7), Str(r, 8)), 255) ?? DBNull.Value);
                ins.Parameters.AddWithValue("tel", (object?)Cut(Str(r, 9), 20) ?? DBNull.Value);
                ins.Parameters.AddWithValue("mail", (object?)Cut(Str(r, 11), 150) ?? DBNull.Value);
                ins.Parameters.AddWithValue("contato", (object?)Cut(Str(r, 3), 100) ?? DBNull.Value);
                ins.Parameters.AddWithValue("obs", (object?)Str(r, 12) ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync();
                inseridos++;
            }
        }

        Log($"  {inseridos} fornecedor(es) após unificar por CNPJ.");
    }

    // ─── Fabricantes (texto livre no SIC, unificados por nome) ──────────────────

    private async Task ImportManufacturersAsync(NpgsqlConnection pg)
    {
        Log("→ Fabricantes (TABEST1.fabricante)...");

        foreach (var store in stores)
        {
            await using var sic = await OpenSicAsync(store);
            await using var cmd = new SqlCommand(
                "SELECT DISTINCT LTRIM(RTRIM(fabricante)) FROM TABEST1 WHERE fabricante IS NOT NULL AND LTRIM(RTRIM(fabricante)) <> ''", sic);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var nome = r.GetString(0);
                if (_manufacturerByName.ContainsKey(nome)) continue;

                var id = Guid.NewGuid();
                _manufacturerByName[nome] = id;

                await using var ins = new NpgsqlCommand(
                    "INSERT INTO manufacturers (id, name, is_active, created_at, created_by) VALUES (@id, @n, true, NOW(), 'import')", pg);
                ins.Parameters.AddWithValue("id", id);
                ins.Parameters.AddWithValue("n", Cut(nome, 100)!);
                await ins.ExecuteNonQueryAsync();
            }
        }

        Log($"  {_manufacturerByName.Count} fabricante(s).");
    }

    // ─── Clientes (unificados por CPF/CNPJ) ─────────────────────────────────────

    private async Task ImportCustomersAsync(NpgsqlConnection pg)
    {
        Log("→ Clientes (TABCLI)...");
        var inseridos = 0;

        foreach (var store in stores)
        {
            await using var sic = await OpenSicAsync(store);
            await using var cmd = new SqlCommand(
                """
                SELECT controle, nome, cgc, tagfisica, endereco, endnumero, endcomplemento,
                       bairro, cidade, estado, cep, telefone, celular, email,
                       limitecred, atendbloq, obs
                FROM TABCLI
                """, sic);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var controle = r.GetInt32(0);
                var nome = Str(r, 1);
                if (string.IsNullOrWhiteSpace(nome)) continue;

                var doc = OnlyDigits(Str(r, 2));

                if (doc is not null && _customerByDoc.TryGetValue(doc, out var existente))
                {
                    _customer[(store.Code, controle)] = existente;
                    continue;
                }

                var id = Guid.NewGuid();
                _customer[(store.Code, controle)] = id;
                if (doc is not null) _customerByDoc[doc] = id;

                // tagfisica diz se é pessoa física; na dúvida, o tamanho do
                // documento decide (11 dígitos = CPF).
                var fisica = !r.IsDBNull(3) ? r.GetBoolean(3) : doc?.Length != 14;

                await using var ins = new NpgsqlCommand(
                    """
                    INSERT INTO customers (id, full_name, document_type, document_number, address,
                                           city, state, zip_code, phone, mobile, email, credit_limit,
                                           is_active, notes, created_at, created_by)
                    VALUES (@id, @nome, @tipo, @doc, @end, @cidade, @uf, @cep, @tel, @cel, @mail,
                            @limite, @ativo, @obs, NOW(), 'import')
                    """, pg);
                ins.Parameters.AddWithValue("id", id);
                ins.Parameters.AddWithValue("nome", Cut(nome, 150)!);
                ins.Parameters.AddWithValue("tipo", fisica ? "CPF" : "CNPJ");
                ins.Parameters.AddWithValue("doc", (object?)Cut(doc, 18) ?? DBNull.Value);
                // customers não tem coluna de bairro — ele entra no endereço,
                // junto de número e complemento.
                ins.Parameters.AddWithValue("end", (object?)Cut(Join(Str(r, 4), Str(r, 5), Str(r, 6), Str(r, 7)), 255) ?? DBNull.Value);
                ins.Parameters.AddWithValue("cidade", (object?)Cut(Str(r, 8), 100) ?? DBNull.Value);
                ins.Parameters.AddWithValue("uf", (object?)Cut(Str(r, 9), 2) ?? DBNull.Value);
                ins.Parameters.AddWithValue("cep", (object?)Cut(Str(r, 10), 10) ?? DBNull.Value);
                ins.Parameters.AddWithValue("tel", (object?)Cut(Str(r, 11), 20) ?? DBNull.Value);
                ins.Parameters.AddWithValue("cel", (object?)Cut(Str(r, 12), 20) ?? DBNull.Value);
                ins.Parameters.AddWithValue("mail", (object?)Cut(Str(r, 13), 150) ?? DBNull.Value);
                ins.Parameters.AddWithValue("limite", r.IsDBNull(14) ? 0m : r.GetDecimal(14));
                ins.Parameters.AddWithValue("ativo", r.IsDBNull(15) || !r.GetBoolean(15));
                ins.Parameters.AddWithValue("obs", (object?)Str(r, 16) ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync();
                inseridos++;
            }
        }

        Log($"  {inseridos} cliente(s) após unificar por CPF/CNPJ.");
    }

    // ─── Usuários / vendedores ──────────────────────────────────────────────────

    private async Task ImportUsersAsync(NpgsqlConnection pg)
    {
        Log("→ Usuários (TABUSER)...");

        foreach (var store in stores)
        {
            await using var sic = await OpenSicAsync(store);
            await using var cmd = new SqlCommand("SELECT controle, nome FROM TABUSER", sic);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var controle = r.GetInt32(0);
                var nome = Str(r, 1);
                if (string.IsNullOrWhiteSpace(nome)) continue;

                var username = Username(nome);

                if (_userByUsername.TryGetValue(username, out var existente))
                {
                    _user[(store.Code, controle)] = existente;
                    continue;
                }

                var id = Guid.NewGuid();
                _user[(store.Code, controle)] = id;
                _userByUsername[username] = id;

                // Hash inválido de propósito: ninguém entra com estas contas. Elas
                // existem para que venda e orçamento históricos tenham vendedor, e
                // o login real acontece pelo control plane.
                await using var ins = new NpgsqlCommand(
                    """
                    INSERT INTO users (id, username, email, full_name, password_hash, role, is_active, created_at, created_by)
                    VALUES (@id, @u, @mail, @n, '!importado-sem-acesso', 'atendente', false, NOW(), 'import')
                    """, pg);
                ins.Parameters.AddWithValue("id", id);
                ins.Parameters.AddWithValue("u", Cut(username, 50)!);
                // email é obrigatório e o SIC não tem. Endereço sintético e
                // inválido de propósito: estas contas não logam nem recebem mail.
                ins.Parameters.AddWithValue("mail", Cut($"{username}@importado.local", 150)!);
                ins.Parameters.AddWithValue("n", Cut(nome, 150)!);
                await ins.ExecuteNonQueryAsync();
            }
        }

        Log($"  {_userByUsername.Count} usuário(s) (inativos — login é pelo control plane).");
    }

    // ─── Formas de pagamento ────────────────────────────────────────────────────

    private async Task ImportPaymentMethodsAsync(NpgsqlConnection pg)
    {
        Log("→ Formas de pagamento (TABEST7)...");

        foreach (var store in stores)
        {
            await using var sic = await OpenSicAsync(store);
            // A descrição da forma de pagamento mora em "recebimento".
            await using var cmd = new SqlCommand("SELECT controle, recebimento FROM TABEST7", sic);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var controle = r.GetInt32(0);
                var nome = Str(r, 1);
                if (string.IsNullOrWhiteSpace(nome)) continue;
                nome = nome.Trim();

                if (_paymentMethodByName.TryGetValue(nome, out var existente))
                {
                    _paymentMethod[(store.Code, controle)] = existente;
                    continue;
                }

                var id = Guid.NewGuid();
                _paymentMethod[(store.Code, controle)] = id;
                _paymentMethodByName[nome] = id;

                var parcelavel = nome.Contains("cart", StringComparison.OrdinalIgnoreCase)
                                 || nome.Contains("créd", StringComparison.OrdinalIgnoreCase)
                                 || nome.Contains("cred", StringComparison.OrdinalIgnoreCase);

                await using var ins = new NpgsqlCommand(
                    """
                    INSERT INTO payment_methods (id, name, requires_installments, max_installments, is_active, created_at, created_by)
                    VALUES (@id, @n, @parc, @max, true, NOW(), 'import')
                    """, pg);
                ins.Parameters.AddWithValue("id", id);
                ins.Parameters.AddWithValue("n", Cut(nome, 50)!);
                ins.Parameters.AddWithValue("parc", parcelavel);
                ins.Parameters.AddWithValue("max", parcelavel ? 12 : 1);
                await ins.ExecuteNonQueryAsync();
            }
        }

        Log($"  {_paymentMethodByName.Count} forma(s) de pagamento.");
    }
}
