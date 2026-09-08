using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Infrastructure.Repositories;

/// <summary>
/// Escritas de estoque da filial ativa.
///
/// Todo statement aqui é SQL cru com <c>branch_id</c> explícito. Isso não é
/// preferência de estilo: SQL cru não passa pelo query filter global nem pelo
/// interceptor de carimbo, então omitir a filial não daria erro — escreveria na
/// linha errada em silêncio.
/// </summary>
public class ProductStockRepository(StoctableDbContext context, BranchContext branchContext)
    : IProductStockRepository
{
    /// <summary>
    /// Garante que a linha (filial, produto) existe, nascendo zerada.
    ///
    /// É um statement separado do UPDATE de propósito. A versão "elegante" —
    /// um único comando com CTE de escrita — NÃO funciona no PostgreSQL: todos
    /// os ramos de um comando com CTE enxergam o MESMO snapshot, então o UPDATE
    /// não vê a linha que o INSERT acabou de criar. Não dá erro; afeta zero
    /// linhas. E passa despercebido em teste de uma thread só com a linha já
    /// existente.
    ///
    /// O SELECT de products garante que produto inexistente não vira violação de
    /// FK: nada é inserido, o UPDATE seguinte não casa e a operação falha limpa.
    /// </summary>
    private const string EnsureRowSql = """
        INSERT INTO product_stocks (id, branch_id, product_id, quantity, reserved, minimum, created_at, created_by)
        SELECT gen_random_uuid(), @branch, p.id, 0, 0, 0, NOW(), 'system'
          FROM products p
         WHERE p.id = @product
        ON CONFLICT (branch_id, product_id) DO NOTHING
        """;

    private const string DecrementSql = """
        UPDATE product_stocks
           SET quantity = quantity - @qty, updated_at = NOW()
         WHERE branch_id = @branch AND product_id = @product AND quantity >= @qty
        RETURNING quantity, reserved
        """;

    private const string IncrementSql = """
        UPDATE product_stocks
           SET quantity = quantity + @qty, updated_at = NOW()
         WHERE branch_id = @branch AND product_id = @product
        RETURNING quantity, reserved
        """;

    private const string ReserveSql = """
        UPDATE product_stocks
           SET reserved = reserved + @qty, updated_at = NOW()
         WHERE branch_id = @branch AND product_id = @product AND quantity - reserved >= @qty
        RETURNING quantity, reserved
        """;

    // GREATEST(0, ...) porque a mesma reserva pode ser liberada mais de uma vez
    // (cancelamento seguido de expiração) e reserva negativa não significa nada.
    private const string ReleaseSql = """
        UPDATE product_stocks
           SET reserved = GREATEST(0, reserved - @qty), updated_at = NOW()
         WHERE branch_id = @branch AND product_id = @product
        RETURNING quantity, reserved
        """;

    private const string SetMinimumSql = """
        UPDATE product_stocks
           SET minimum = @qty, updated_at = NOW()
         WHERE branch_id = @branch AND product_id = @product
        RETURNING quantity, reserved
        """;

    public Task<StockOperationResult> TryDecrementAsync(Guid productId, decimal quantity, CancellationToken ct = default)
        => MutateAsync(DecrementSql, productId, quantity, ct);

    public Task<StockOperationResult> IncrementAsync(Guid productId, decimal quantity, CancellationToken ct = default)
        => MutateAsync(IncrementSql, productId, quantity, ct);

    public Task<StockOperationResult> TryReserveAsync(Guid productId, decimal quantity, CancellationToken ct = default)
        => MutateAsync(ReserveSql, productId, quantity, ct);

    public Task<StockOperationResult> ReleaseReservedAsync(Guid productId, decimal quantity, CancellationToken ct = default)
        => MutateAsync(ReleaseSql, productId, quantity, ct);

    public Task<StockOperationResult> SetMinimumAsync(Guid productId, decimal minimum, CancellationToken ct = default)
        => MutateAsync(SetMinimumSql, productId, minimum, ct);

    public async Task<ProductStock?> GetAsync(Guid productId, CancellationToken ct = default)
        => await context.ProductStocks.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductId == productId, ct);

    /// <summary>
    /// A única leitura do sistema que atravessa filiais, e por isso a única que
    /// usa IgnoreQueryFilters. O escopo não vem daqui: quem chama passa as
    /// filiais que as claims autorizam, e o Where as aplica de volta — sem isso
    /// a chamada leria a empresa inteira.
    /// </summary>
    public async Task<IReadOnlyList<BranchStockRow>> GetNetworkAsync(
        Guid productId, IReadOnlyCollection<Guid> branchIds, CancellationToken ct = default)
    {
        if (branchIds.Count == 0) return [];

        var stocks = await context.ProductStocks.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.ProductId == productId && branchIds.Contains(s.BranchId))
            .Select(s => new { s.BranchId, s.Quantity, s.Reserved, s.Minimum })
            .ToListAsync(ct);

        // O que saiu de cada filial e ainda não foi recebido. Sem esta coluna a
        // mercadoria parece ter sumido entre o envio e a conferência.
        var inTransit = await context.StockTransfers.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Status == StockTransferStatus.InTransit && branchIds.Contains(t.BranchId))
            .SelectMany(t => t.Items.Where(i => i.ProductId == productId)
                .Select(i => new { t.BranchId, i.QuantitySent }))
            .ToListAsync(ct);

        var transitByBranch = inTransit
            .GroupBy(x => x.BranchId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantitySent));

        // Filial sem linha entra zerada: a tela precisa dizer "não tem aqui",
        // e não omitir a loja.
        return [.. branchIds.Select(id =>
        {
            var s = stocks.FirstOrDefault(x => x.BranchId == id);
            return new BranchStockRow(
                id,
                s?.Quantity ?? 0,
                s?.Reserved ?? 0,
                s?.Minimum ?? 0,
                transitByBranch.GetValueOrDefault(id));
        })];
    }

    /// <summary>
    /// Garante a linha e aplica o UPDATE guardado, devolvendo o saldo pós-operação.
    ///
    /// Correto sob concorrência em READ COMMITTED: o INSERT que perde a corrida
    /// bloqueia na tupla conflitante e volta sem erro; o UPDATE, sendo um statement
    /// novo, tira um snapshot novo e enxerga a linha. Dois UPDATE concorrentes na
    /// mesma linha serializam pelo row lock, e o segundo REAVALIA o predicado
    /// sobre a versão nova — é exatamente isso que impede o oversell.
    ///
    /// ⚠️ Isso depende do nível de isolamento padrão. Sob REPEATABLE READ o
    /// segundo UPDATE passaria a lançar serialization failure em vez de reavaliar,
    /// e o caminho de erro aqui teria que mudar junto.
    ///
    /// O RETURNING é lido por ADO.NET, e não por SqlQuery do EF, porque o EF
    /// envolve a consulta num subselect (<c>SELECT … FROM (&lt;sql&gt;) AS x</c>)
    /// e o PostgreSQL não aceita statement de escrita nessa posição.
    /// </summary>
    private async Task<StockOperationResult> MutateAsync(
        string updateSql, Guid productId, decimal quantity, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(ct);

        try
        {
            await using (var ensure = CreateCommand(connection, EnsureRowSql, productId, quantity))
                await ensure.ExecuteNonQueryAsync(ct);

            await using var command = CreateCommand(connection, updateSql, productId, quantity);
            await using var reader = await command.ExecuteReaderAsync(ct);

            if (await reader.ReadAsync(ct))
                return new StockOperationResult(true, reader.GetDecimal(0), reader.GetDecimal(1));
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }

        // Nenhuma linha voltou: a guarda reprovou. Relemos o saldo para que o
        // chamador possa dizer na mensagem de erro quanto de fato existe.
        var current = await GetAsync(productId, ct);
        return new StockOperationResult(false, current?.Quantity ?? 0, current?.Reserved ?? 0);
    }

    /// <summary>
    /// Comando parametrizado, amarrado à transação corrente do contexto.
    ///
    /// Sem o <c>Transaction</c> o comando roda fora da transação que o serviço
    /// abriu: um rollback do EF deixaria a baixa de estoque comitada sozinha.
    /// </summary>
    private DbCommand CreateCommand(DbConnection connection, string sql, Guid productId, decimal quantity)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        AddParameter(command, "branch", branchContext.BranchId);
        AddParameter(command, "product", productId);
        AddParameter(command, "qty", quantity);

        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
