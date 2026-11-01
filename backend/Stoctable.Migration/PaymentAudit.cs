using Npgsql;

namespace Stoctable.Migration;

public record AuditCategory(int Count, IReadOnlyList<string> Sales);

public record PaymentAuditReport(
    AuditCategory CancelledThenPaid,
    AuditCategory CancelledWithReceipts,
    AuditCategory Overpaid,
    AuditCategory OverpaidImported,
    AuditCategory StuckPartiallyPaid,
    AuditCategory NonPositivePayments,
    AuditCategory NonPositivePaymentsImported)
{
    /// <summary>
    /// Fica de fora o que veio do SIC: os valores são fiéis ao legado, e nenhum
    /// caminho de pagamento do Stoctable produziu aquilo.
    /// </summary>
    public bool HasFindings =>
        CancelledThenPaid.Count + CancelledWithReceipts.Count + Overpaid.Count
        + StuckPartiallyPaid.Count + NonPositivePayments.Count > 0;
}

/// <summary>
/// Procura vendas deixadas inconsistentes pelos caminhos de pagamento antigos, que
/// aceitavam pagar venda cancelada, receber acima do saldo e valor negativo.
///
/// <para>
/// <b>Só lê, de propósito.</b> Corrigir exige saber o que aconteceu no balcão: uma
/// venda cancelada e depois paga pode ter sido engano de quem cancelou ou dinheiro
/// que precisa voltar ao cliente. Isso não se decide por SQL.
/// </para>
/// </summary>
public class PaymentAudit(string tenantConnStr)
{
    private const int SampleSize = 200;

    private const string CancelledThenPaidSql = """
        FROM sales s
        WHERE s.cancelled_at IS NOT NULL AND s.status <> 'cancelled'
        """;

    private const string CancelledWithReceiptsSql = """
        FROM sales s
        WHERE s.status = 'cancelled'
          AND EXISTS (SELECT 1 FROM payments p WHERE p.sale_id = s.id AND p.status = 'completed')
        """;

    private const string OverpaidSql = """
        FROM sales s
        WHERE s.amount_paid > s.total_amount AND s.created_by <> 'import'
        """;

    private const string OverpaidImportedSql = """
        FROM sales s
        WHERE s.amount_paid > s.total_amount AND s.created_by = 'import'
        """;

    private const string StuckPartiallyPaidSql = """
        FROM sales s
        WHERE s.status = 'partially_paid' AND s.amount_paid >= s.total_amount
        """;

    private const string NonPositivePaymentsSql = """
        FROM sales s
        WHERE s.created_by <> 'import'
          AND EXISTS (SELECT 1 FROM payments p WHERE p.sale_id = s.id AND p.amount <= 0)
        """;

    private const string NonPositivePaymentsImportedSql = """
        FROM sales s
        WHERE s.created_by = 'import'
          AND EXISTS (SELECT 1 FROM payments p WHERE p.sale_id = s.id AND p.amount <= 0)
        """;

    public async Task<PaymentAuditReport> RunAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(tenantConnStr);
        await connection.OpenAsync(ct);
        await using var transaction = await BeginReadOnlyAsync(connection, ct);

        return new PaymentAuditReport(
            await QueryAsync(connection, transaction, CancelledThenPaidSql, ct),
            await QueryAsync(connection, transaction, CancelledWithReceiptsSql, ct),
            await QueryAsync(connection, transaction, OverpaidSql, ct),
            await QueryAsync(connection, transaction, OverpaidImportedSql, ct),
            await QueryAsync(connection, transaction, StuckPartiallyPaidSql, ct),
            await QueryAsync(connection, transaction, NonPositivePaymentsSql, ct),
            await QueryAsync(connection, transaction, NonPositivePaymentsImportedSql, ct));
    }

    /// <summary>Público para o teste provar que o banco recusa escrita nesta transação.</summary>
    public static async Task<NpgsqlTransaction> BeginReadOnlyAsync(NpgsqlConnection connection, CancellationToken ct = default)
    {
        var transaction = await connection.BeginTransactionAsync(ct);
        await using var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
        await readOnly.ExecuteNonQueryAsync(ct);
        return transaction;
    }

    public static void Print(PaymentAuditReport report)
    {
        Console.WriteLine("Auditoria de pagamentos (somente leitura)");
        Console.WriteLine();
        PrintCategory("Canceladas e depois pagas", report.CancelledThenPaid);
        PrintCategory("Canceladas com recebimento sem estorno", report.CancelledWithReceipts);
        PrintCategory("Recebidas acima do total", report.Overpaid);
        PrintCategory("Parcialmente pagas com saldo quitado", report.StuckPartiallyPaid);
        PrintCategory("Com pagamento zero ou negativo", report.NonPositivePayments);
        Console.WriteLine();
        Console.WriteLine("Informativo, valores fiéis ao SIC:");
        Console.WriteLine($"Importadas acima do total: {report.OverpaidImported.Count}");
        Console.WriteLine($"Importadas com pagamento zero ou negativo: {report.NonPositivePaymentsImported.Count}");
        Console.WriteLine();
        Console.WriteLine(report.HasFindings
            ? "Há vendas para revisar. Nada foi alterado."
            : "Nenhuma inconsistência nas vendas do Stoctable. Nada foi alterado.");
    }

    private static void PrintCategory(string title, AuditCategory category)
    {
        Console.WriteLine($"{title}: {category.Count}");
        foreach (var sale in category.Sales)
            Console.WriteLine($"  {sale}");
    }

    private static async Task<AuditCategory> QueryAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string fromWhere, CancellationToken ct)
    {
        await using var count = new NpgsqlCommand($"SELECT count(*) {fromWhere}", connection, transaction);
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct));

        var sales = new List<string>();
        await using var sample = new NpgsqlCommand(
            $"SELECT s.sale_number {fromWhere} ORDER BY s.created_at DESC LIMIT {SampleSize}", connection, transaction);
        await using var reader = await sample.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            sales.Add(reader.GetString(0));

        return new AuditCategory(total, sales);
    }
}
