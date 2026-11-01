using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Migration;

namespace Stoctable.Tests.Integration;

/// <summary>
/// A auditoria roda contra dados reais, então duas coisas importam: o SQL de cada
/// categoria achar o que diz achar, e nunca conseguir escrever. O fixture usa
/// EnsureCreated, e só um teste contra o schema de verdade valida esses comandos.
/// </summary>
[Trait("Category", "Integration")]
public class PaymentAuditTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PaymentAuditTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Audit_FindsEachKindOfInconsistency()
    {
        var canceladaDepoisPaga = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.Paid;
            s.AmountPaid = 100m;
            s.CancelledAt = DateTimeOffset.UtcNow;
        });
        var canceladaComRecebimento = await SeedSaleAsync(s => s.Status = SaleStatus.Cancelled);
        await SeedPaymentAsync(canceladaComRecebimento, 100m, PaymentStatus.Completed);
        var recebidaAMais = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.Paid;
            s.AmountPaid = 160m;
        });
        var importadaAMais = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.Paid;
            s.AmountPaid = 160m;
            s.CreatedBy = "import";
        });
        var travada = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.PartiallyPaid;
            s.AmountPaid = 100m;
        });
        var comNegativo = await SeedSaleAsync(s => s.Status = SaleStatus.PartiallyPaid);
        await SeedPaymentAsync(comNegativo, -50m, PaymentStatus.Completed);
        var importadaComNegativo = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.Paid;
            s.CreatedBy = "import";
        });
        await SeedPaymentAsync(importadaComNegativo, -50m, PaymentStatus.Completed);

        var report = await new PaymentAudit(_fixture.ConnectionString).RunAsync();

        Assert.Contains(canceladaDepoisPaga, report.CancelledThenPaid.Sales);
        Assert.Contains(canceladaComRecebimento, report.CancelledWithReceipts.Sales);
        Assert.Contains(recebidaAMais, report.Overpaid.Sales);
        Assert.DoesNotContain(importadaAMais, report.Overpaid.Sales);
        Assert.Contains(importadaAMais, report.OverpaidImported.Sales);
        Assert.Contains(travada, report.StuckPartiallyPaid.Sales);
        Assert.Contains(comNegativo, report.NonPositivePayments.Sales);
        Assert.DoesNotContain(importadaComNegativo, report.NonPositivePayments.Sales);
        Assert.Contains(importadaComNegativo, report.NonPositivePaymentsImported.Sales);
    }

    [Fact]
    public async Task Audit_IgnoresHealthySales()
    {
        var paga = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.Paid;
            s.AmountPaid = 100m;
        });
        await SeedPaymentAsync(paga, 100m, PaymentStatus.Completed);
        var estornada = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.Cancelled;
            s.CancelledAt = DateTimeOffset.UtcNow;
        });
        await SeedPaymentAsync(estornada, 100m, PaymentStatus.Refunded);
        var parcial = await SeedSaleAsync(s =>
        {
            s.Status = SaleStatus.PartiallyPaid;
            s.AmountPaid = 30m;
        });
        await SeedPaymentAsync(parcial, 30m, PaymentStatus.Completed);

        var report = await new PaymentAudit(_fixture.ConnectionString).RunAsync();

        foreach (var saudavel in new[] { paga, estornada, parcial })
        {
            Assert.DoesNotContain(saudavel, report.CancelledThenPaid.Sales);
            Assert.DoesNotContain(saudavel, report.CancelledWithReceipts.Sales);
            Assert.DoesNotContain(saudavel, report.Overpaid.Sales);
            Assert.DoesNotContain(saudavel, report.StuckPartiallyPaid.Sales);
            Assert.DoesNotContain(saudavel, report.NonPositivePayments.Sales);
        }
    }

    /// <summary>A garantia é do banco, não da disciplina de quem escreve o SQL.</summary>
    [Fact]
    public async Task Audit_TransactionRefusesWrites()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await PaymentAudit.BeginReadOnlyAsync(connection);

        await using var write = new NpgsqlCommand("UPDATE sales SET notes = 'x'", connection, transaction);

        var error = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync());
        Assert.Equal("25006", error.SqlState); // read_only_sql_transaction
    }

    private async Task<string> SeedSaleAsync(Action<Sale> configure)
    {
        await using var ctx = _fixture.CreateContext();
        var sale = new Sale
        {
            SaleNumber = $"AUD{Guid.NewGuid():N}"[..14],
            Subtotal = 100m,
            TotalAmount = 100m,
        };
        configure(sale);
        ctx.Sales.Add(sale);
        await ctx.SaveChangesAsync();

        // CreatedBy pode ser sobrescrito na gravação, e a categoria de importados
        // depende dele: grava-se direto, como faz o COPY do importador.
        await ctx.Sales.Where(s => s.Id == sale.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.CreatedBy, sale.CreatedBy));
        return sale.SaleNumber;
    }

    private async Task SeedPaymentAsync(string saleNumber, decimal amount, PaymentStatus status)
    {
        await using var ctx = _fixture.CreateContext();
        var sale = await ctx.Sales.AsNoTracking().FirstAsync(s => s.SaleNumber == saleNumber);
        var method = new PaymentMethod { Name = $"Forma {Guid.NewGuid():N}", IsActive = true, MaxInstallments = 1 };
        ctx.PaymentMethods.Add(method);
        ctx.Payments.Add(new Payment
        {
            SaleId = sale.Id,
            PaymentMethodId = method.Id,
            Amount = amount,
            Status = status,
            PaidAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }
}
