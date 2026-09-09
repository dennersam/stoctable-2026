using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;

namespace Stoctable.Migration;

/// <summary>
/// Produtos, estoque e o histórico de vendas e orçamentos.
///
/// Tudo aqui usa <c>COPY</c> binário em vez de INSERT: são ~1,3 milhão de itens
/// de venda somando as três lojas, e linha a linha isso levaria horas.
/// </summary>
public partial class SicMultiStoreImporter
{
    // ─── Produtos + estoque da filial ───────────────────────────────────────────

    private async Task ImportProductsAsync(NpgsqlConnection pg, SicStoreSource store)
    {
        Log($"→ Produtos de {store.Code} (TABEST1)...");

        await using var sic = await OpenSicAsync(store);
        await using var cmd = new SqlCommand(
            """
            SELECT controle, codigo, produto, fabricante, lkfornec, precocusto, precovenda,
                   quantidade, estminimo, unidade, inativo, obs, ipi, icms, cst
            FROM TABEST1
            -- ORDER BY explícito para a numeração ser reproduzível: sem ele o
            -- SQL Server não garante ordem, e recarregar geraria SKUs
            -- diferentes para os mesmos produtos.
            ORDER BY CASE WHEN TRY_CAST(codigo AS int) IS NULL THEN 1 ELSE 0 END,
                     TRY_CAST(codigo AS int), codigo, controle
            """, sic);
        await using var r = await cmd.ExecuteReaderAsync();

        var produtos = 0;
        var estoques = new List<(Guid ProductId, decimal Qtd, decimal Min)>();

        await using (var copy = await pg.BeginBinaryImportAsync(
            """
            COPY products (id, sku, name, manufacturer_id, category_id, supplier_id,
                           cost_price, sale_price, stock_quantity, stock_reserved, stock_minimum,
                           unit, ipi_rate, icms_rate, cst, is_active, notes, created_at, created_by)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            while (await r.ReadAsync())
            {
                var controle = r.GetInt32(0);
                var nome = Str(r, 2);
                if (string.IsNullOrWhiteSpace(nome)) continue;

                var id = Guid.NewGuid();
                _product[(store.Code, controle)] = id;

                // SKU numérico sequencial, contínuo entre as lojas — é o mesmo
                // formato que o ProductService gera para produto novo
                // (GetNextSkuAsync devolve o maior inteiro + 1). Como as lojas
                // são importadas em ordem, cada uma ocupa uma faixa própria.
                //
                // O código do SIC NÃO pode virar o SKU: ele é sequência interna
                // de cada loja, e o mesmo 1214 existe nas três apontando para
                // peças diferentes. Ele fica preservado nas observações.
                var sku = (++_skuSequencial).ToString();

                var codigo = Str(r, 1)?.Trim();
                var codigoSic = string.IsNullOrWhiteSpace(codigo)
                    ? $"{store.Code}-C{controle}"
                    : $"{store.Code}-{codigo}";

                var obs = Str(r, 11);
                var notas = string.IsNullOrWhiteSpace(obs)
                    ? $"Código SIC: {codigoSic}"
                    : $"{obs}\nCódigo SIC: {codigoSic}";

                var fabricante = Str(r, 3)?.Trim();
                Guid? manufacturerId = fabricante is not null && _manufacturerByName.TryGetValue(fabricante, out var m)
                    ? m : null;

                Guid? supplierId = !r.IsDBNull(4) && _supplier.TryGetValue((store.Code, r.GetInt32(4)), out var s)
                    ? s : null;

                var qtd = r.IsDBNull(7) ? 0m : (decimal)r.GetDouble(7);
                var min = r.IsDBNull(8) ? 0m : (decimal)r.GetDouble(8);

                await copy.StartRowAsync();
                await copy.WriteAsync(id, NpgsqlDbType.Uuid);
                await copy.WriteAsync(Cut(sku, 50)!, NpgsqlDbType.Text);
                await copy.WriteAsync(Cut(nome, 255)!, NpgsqlDbType.Text);
                await WriteNullableAsync(copy, manufacturerId, NpgsqlDbType.Uuid);
                await copy.WriteAsync(_categoryId, NpgsqlDbType.Uuid);
                await WriteNullableAsync(copy, supplierId, NpgsqlDbType.Uuid);
                await copy.WriteAsync(r.IsDBNull(5) ? 0m : r.GetDecimal(5), NpgsqlDbType.Numeric);
                await copy.WriteAsync(r.IsDBNull(6) ? 0m : r.GetDecimal(6), NpgsqlDbType.Numeric);
                // As colunas de estoque em products estão mortas (o saldo real
                // vai para product_stocks); ficam zeradas.
                await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
                await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
                await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
                await copy.WriteAsync(Cut(Str(r, 9), 10) ?? "UN", NpgsqlDbType.Text);
                await WriteNullableAsync(copy, r.IsDBNull(12) ? (decimal?)null : (decimal)r.GetDouble(12), NpgsqlDbType.Numeric);
                await WriteNullableAsync(copy, r.IsDBNull(13) ? (decimal?)null : (decimal)r.GetDouble(13), NpgsqlDbType.Numeric);
                await WriteNullableAsync(copy, Cut(Str(r, 14), 10), NpgsqlDbType.Text);
                await copy.WriteAsync(!(!r.IsDBNull(10) && r.GetBoolean(10)), NpgsqlDbType.Boolean);
                await copy.WriteAsync(notas, NpgsqlDbType.Text);
                await copy.WriteAsync(DateTime.UtcNow, NpgsqlDbType.TimestampTz);
                await copy.WriteAsync("import", NpgsqlDbType.Text);

                produtos++;
                estoques.Add((id, qtd, min));
            }

            await copy.CompleteAsync();
        }

        // O estoque é da FILIAL: cada produto ganha linha só na loja de origem.
        // Nas outras ele existe no catálogo com saldo zero, que é o que o modelo
        // já faz para produto nunca movimentado ali.
        await using (var copy = await pg.BeginBinaryImportAsync(
            """
            COPY product_stocks (id, branch_id, product_id, quantity, reserved, minimum, created_at, created_by)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var (productId, qtd, min) in estoques)
            {
                await copy.StartRowAsync();
                await copy.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
                await copy.WriteAsync(store.BranchId, NpgsqlDbType.Uuid);
                await copy.WriteAsync(productId, NpgsqlDbType.Uuid);
                await copy.WriteAsync(Math.Max(0, qtd), NpgsqlDbType.Numeric);
                await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
                await copy.WriteAsync(Math.Max(0, min), NpgsqlDbType.Numeric);
                await copy.WriteAsync(DateTime.UtcNow, NpgsqlDbType.TimestampTz);
                await copy.WriteAsync("import", NpgsqlDbType.Text);
            }

            await copy.CompleteAsync();
        }

        Log($"  {produtos} produto(s) e {estoques.Count} linha(s) de estoque.");
    }

    // ─── Vendas ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>TABEST3A</c> guarda venda e orçamento na mesma tabela, separados pelo
    /// campo <c>venda</c> (1 = venda, 0 = orçamento). O cabeçalho não tem total:
    /// ele sai da soma dos itens, e o valor pago da soma de <c>TABEST3R</c>.
    /// </summary>
    private async Task ImportSalesAsync(NpgsqlConnection pg, SicStoreSource store)
    {
        Log($"→ Vendas de {store.Code} (TABEST3A/3B/3R)...");

        await using var sic = await OpenSicAsync(store);

        var vendas = 0;
        await using (var copy = await pg.BeginBinaryImportAsync(
            """
            COPY sales (id, branch_id, sale_number, customer_id, salesperson_id, cashier_id, status,
                        subtotal, discount_amount, total_amount, amount_paid, notes,
                        completed_at, created_at, created_by)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            await using var cmd = new SqlCommand(
                """
                SELECT a.controle, a.data, a.lkcliente, a.lkvendedor, a.lkuser, a.obs,
                       ISNULL(i.total, 0) AS total, ISNULL(p.pago, 0) AS pago
                  FROM TABEST3A a
                  LEFT JOIN (SELECT lkest3a, SUM(ISNULL(totvenda,0)) total FROM TABEST3B GROUP BY lkest3a) i
                         ON i.lkest3a = a.controle
                  LEFT JOIN (SELECT lkest3a, SUM(ISNULL(valor,0)) pago FROM TABEST3R GROUP BY lkest3a) p
                         ON p.lkest3a = a.controle
                 WHERE a.venda = 1 AND (@since IS NULL OR a.data >= @since)
                """, sic) { CommandTimeout = 0 };
            AddSince(cmd);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var controle = r.GetInt32(0);
                var id = Guid.NewGuid();
                _sale[(store.Code, controle)] = id;

                var data = r.IsDBNull(1) ? DateTime.UtcNow : DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc);
                var total = r.IsDBNull(6) ? 0m : r.GetDecimal(6);
                var pago = r.IsDBNull(7) ? 0m : r.GetDecimal(7);

                await copy.StartRowAsync();
                await copy.WriteAsync(id, NpgsqlDbType.Uuid);
                await copy.WriteAsync(store.BranchId, NpgsqlDbType.Uuid);
                await copy.WriteAsync($"{store.Code}-{controle}", NpgsqlDbType.Text);
                await WriteNullableAsync(copy, Lookup(_customer, store.Code, r, 2), NpgsqlDbType.Uuid);
                await WriteNullableAsync(copy, Lookup(_user, store.Code, r, 3), NpgsqlDbType.Uuid);
                await WriteNullableAsync(copy, Lookup(_user, store.Code, r, 4), NpgsqlDbType.Uuid);
                // Venda histórica do SIC já aconteceu e foi paga; o SIC não tem
                // marcação de cancelamento utilizável (a coluna está sempre nula).
                await copy.WriteAsync("paid", NpgsqlDbType.Text);
                await copy.WriteAsync(total, NpgsqlDbType.Numeric);
                await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
                await copy.WriteAsync(total, NpgsqlDbType.Numeric);
                await copy.WriteAsync(pago, NpgsqlDbType.Numeric);
                await WriteNullableAsync(copy, Str(r, 5), NpgsqlDbType.Text);
                await copy.WriteAsync(data, NpgsqlDbType.TimestampTz);
                await copy.WriteAsync(data, NpgsqlDbType.TimestampTz);
                await copy.WriteAsync("import", NpgsqlDbType.Text);
                vendas++;
            }

            await copy.CompleteAsync();
        }

        var itens = await CopyDocumentItemsAsync(pg, sic, store, "sale_items", "sale_id", _sale, venda: true);
        var pagamentos = await CopyPaymentsAsync(pg, sic, store);

        Log($"  {vendas} venda(s), {itens} item(ns), {pagamentos} pagamento(s).");
    }

    // ─── Orçamentos ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Orçamento histórico entra como <b>cancelado</b>, e isso é deliberado: um
    /// orçamento finalizado reservaria estoque, e o saldo das lojas nasceria
    /// errado. Nenhuma <c>StockReservation</c> é criada aqui.
    /// </summary>
    private async Task ImportQuotationsAsync(NpgsqlConnection pg, SicStoreSource store)
    {
        Log($"→ Orçamentos de {store.Code} (TABEST3A venda=0)...");

        await using var sic = await OpenSicAsync(store);

        var orcamentos = 0;
        await using (var copy = await pg.BeginBinaryImportAsync(
            """
            COPY quotations (id, branch_id, quotation_number, customer_id, salesperson_id, status,
                             subtotal, discount_pct, discount_amount, total_amount, notes,
                             cancelled_at, cancellation_reason, created_at, created_by)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            await using var cmd = new SqlCommand(
                """
                SELECT a.controle, a.data, a.lkcliente, a.lkvendedor, a.obs, ISNULL(i.total, 0) AS total
                  FROM TABEST3A a
                  LEFT JOIN (SELECT lkest3a, SUM(ISNULL(totvenda,0)) total FROM TABEST3B GROUP BY lkest3a) i
                         ON i.lkest3a = a.controle
                 WHERE a.venda = 0 AND (@since IS NULL OR a.data >= @since)
                """, sic) { CommandTimeout = 0 };
            AddSince(cmd);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var controle = r.GetInt32(0);
                var id = Guid.NewGuid();
                _quotation[(store.Code, controle)] = id;

                var data = r.IsDBNull(1) ? DateTime.UtcNow : DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc);
                var total = r.IsDBNull(5) ? 0m : r.GetDecimal(5);

                await copy.StartRowAsync();
                await copy.WriteAsync(id, NpgsqlDbType.Uuid);
                await copy.WriteAsync(store.BranchId, NpgsqlDbType.Uuid);
                await copy.WriteAsync($"{store.Code}-{controle}", NpgsqlDbType.Text);
                await WriteNullableAsync(copy, Lookup(_customer, store.Code, r, 2), NpgsqlDbType.Uuid);
                await WriteNullableAsync(copy, Lookup(_user, store.Code, r, 3), NpgsqlDbType.Uuid);
                await copy.WriteAsync("cancelled", NpgsqlDbType.Text);
                await copy.WriteAsync(total, NpgsqlDbType.Numeric);
                await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
                await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
                await copy.WriteAsync(total, NpgsqlDbType.Numeric);
                await WriteNullableAsync(copy, Str(r, 4), NpgsqlDbType.Text);
                await copy.WriteAsync(data, NpgsqlDbType.TimestampTz);
                await copy.WriteAsync("Importado do SIC — orçamento histórico, não efetivado.", NpgsqlDbType.Text);
                await copy.WriteAsync(data, NpgsqlDbType.TimestampTz);
                await copy.WriteAsync("import", NpgsqlDbType.Text);
                orcamentos++;
            }

            await copy.CompleteAsync();
        }

        var itens = await CopyDocumentItemsAsync(pg, sic, store, "quotation_items", "quotation_id", _quotation, venda: false);
        Log($"  {orcamentos} orçamento(s), {itens} item(ns).");
    }

    // ─── Itens (venda e orçamento compartilham TABEST3B) ────────────────────────

    private async Task<int> CopyDocumentItemsAsync(
        NpgsqlConnection pg, SqlConnection sic, SicStoreSource store,
        string tabela, string fkColuna, Dictionary<(string, int), Guid> pais, bool venda)
    {
        var total = 0;

        await using var copy = await pg.BeginBinaryImportAsync(
            $"""
            COPY {tabela} (id, {fkColuna}, product_id, quantity, unit_price, discount_pct, line_total, created_at, created_by)
            FROM STDIN (FORMAT BINARY)
            """);

        await using var cmd = new SqlCommand(
            $"""
            SELECT b.lkest3a, b.lkproduto, b.quantidade, b.totvenda, b.datainc
              FROM TABEST3B b
              JOIN TABEST3A a ON a.controle = b.lkest3a
             WHERE a.venda = {(venda ? 1 : 0)} AND (@since IS NULL OR a.data >= @since)
            """, sic) { CommandTimeout = 0 };
        AddSince(cmd);
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            if (r.IsDBNull(0) || r.IsDBNull(1)) continue;
            if (!pais.TryGetValue((store.Code, r.GetInt32(0)), out var paiId)) continue;
            // Item apontando para produto que não existe mais no cadastro: o SIC
            // permite, o Postgres não (há FK). Descartar o item é melhor que
            // perder o documento inteiro.
            if (!_product.TryGetValue((store.Code, r.GetInt32(1)), out var produtoId)) continue;

            var qtd = r.IsDBNull(2) ? 0m : (decimal)r.GetDouble(2);
            var linha = r.IsDBNull(3) ? 0m : r.GetDecimal(3);
            var unit = qtd != 0 ? Math.Round(linha / qtd, 2) : 0m;
            var data = r.IsDBNull(4) ? DateTime.UtcNow : DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc);

            await copy.StartRowAsync();
            await copy.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await copy.WriteAsync(paiId, NpgsqlDbType.Uuid);
            await copy.WriteAsync(produtoId, NpgsqlDbType.Uuid);
            await copy.WriteAsync(qtd, NpgsqlDbType.Numeric);
            await copy.WriteAsync(unit, NpgsqlDbType.Numeric);
            await copy.WriteAsync(0m, NpgsqlDbType.Numeric);
            await copy.WriteAsync(linha, NpgsqlDbType.Numeric);
            await copy.WriteAsync(data, NpgsqlDbType.TimestampTz);
            await copy.WriteAsync("import", NpgsqlDbType.Text);
            total++;
        }

        await copy.CompleteAsync();
        return total;
    }

    // ─── Pagamentos ─────────────────────────────────────────────────────────────

    private async Task<int> CopyPaymentsAsync(NpgsqlConnection pg, SqlConnection sic, SicStoreSource store)
    {
        var total = 0;

        await using var copy = await pg.BeginBinaryImportAsync(
            """
            COPY payments (id, branch_id, sale_id, payment_method_id, amount, installments, status,
                           paid_at, created_at, created_by)
            FROM STDIN (FORMAT BINARY)
            """);

        await using var cmd = new SqlCommand(
            """
            SELECT r.lkest3a, r.lkest7, r.valor, a.data
              FROM TABEST3R r
              JOIN TABEST3A a ON a.controle = r.lkest3a
             WHERE a.venda = 1 AND (@since IS NULL OR a.data >= @since)
            """, sic) { CommandTimeout = 0 };
        AddSince(cmd);
        await using var rd = await cmd.ExecuteReaderAsync();

        while (await rd.ReadAsync())
        {
            if (rd.IsDBNull(0) || rd.IsDBNull(1)) continue;
            if (!_sale.TryGetValue((store.Code, rd.GetInt32(0)), out var saleId)) continue;
            if (!_paymentMethod.TryGetValue((store.Code, rd.GetInt32(1)), out var methodId)) continue;

            var data = rd.IsDBNull(3) ? DateTime.UtcNow : DateTime.SpecifyKind(rd.GetDateTime(3), DateTimeKind.Utc);

            await copy.StartRowAsync();
            await copy.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await copy.WriteAsync(store.BranchId, NpgsqlDbType.Uuid);
            await copy.WriteAsync(saleId, NpgsqlDbType.Uuid);
            await copy.WriteAsync(methodId, NpgsqlDbType.Uuid);
            await copy.WriteAsync(rd.IsDBNull(2) ? 0m : rd.GetDecimal(2), NpgsqlDbType.Numeric);
            await copy.WriteAsync(1, NpgsqlDbType.Integer);
            await copy.WriteAsync("completed", NpgsqlDbType.Text);
            await copy.WriteAsync(data, NpgsqlDbType.TimestampTz);
            await copy.WriteAsync(data, NpgsqlDbType.TimestampTz);
            await copy.WriteAsync("import", NpgsqlDbType.Text);
            total++;
        }

        await copy.CompleteAsync();
        return total;
    }

    // ─── Sequências de numeração ────────────────────────────────────────────────

    /// <summary>
    /// Sem isto o primeiro documento novo de cada loja começaria do 1 e colidiria
    /// com o histórico importado. A sequência parte do maior número já usado.
    /// </summary>
    private async Task SeedNumberSequencesAsync(NpgsqlConnection pg)
    {
        Log("→ Sequências de numeração...");

        foreach (var store in stores)
        {
            var maiorVenda = _sale.Keys.Where(k => k.Item1 == store.Code).Select(k => k.Item2).DefaultIfEmpty(0).Max();
            var maiorOrc = _quotation.Keys.Where(k => k.Item1 == store.Code).Select(k => k.Item2).DefaultIfEmpty(0).Max();

            foreach (var (prefixo, valor) in new[] { ("VEN", maiorVenda), ("ORC", maiorOrc), ("TRF", 0) })
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    INSERT INTO number_sequences (branch_id, prefix, current_value, updated_at)
                    VALUES (@b, @p, @v, NOW())
                    ON CONFLICT (branch_id, prefix) DO UPDATE SET current_value = EXCLUDED.current_value
                    """, pg);
                cmd.Parameters.AddWithValue("b", store.BranchId);
                cmd.Parameters.AddWithValue("p", prefixo);
                cmd.Parameters.AddWithValue("v", (long)valor);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
