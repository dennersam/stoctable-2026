using System.Security.Cryptography;
using System.Text;

namespace Stoctable.Migration;

/// <summary>
/// Uma loja do SIC a ser importada: o código da filial no Stoctable e o banco
/// SQL Server de onde os dados saem.
/// </summary>
public record SicStoreSource(string Code, Guid BranchId, string ConnectionString)
{
    /// <summary>
    /// Id determinístico derivado do código, para quando não há control plane
    /// (importação para banco de teste).
    ///
    /// Determinístico e não aleatório de propósito: reimportar o mesmo conjunto
    /// gera os mesmos ids, então dá para comparar duas execuções e para conferir
    /// consultas escritas na mão contra a rodada anterior.
    /// </summary>
    public static Guid DeterministicBranchId(string code)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"stoctable-branch-{code.ToUpperInvariant()}"));
        return new Guid(hash);
    }

    /// <summary>
    /// Lê os ids reais das filiais no control plane, por código.
    ///
    /// Devolve vazio (em vez de estourar) quando não há control plane: o
    /// importador continua funcionando contra banco de teste, mas avisa alto que
    /// os ids são determinísticos e não servem para produção.
    /// </summary>
    public static async Task<Dictionary<string, Guid>> ResolveBranchIdsAsync(string? controlPlaneConnStr)
    {
        var mapa = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (controlPlaneConnStr is null) return mapa;

        try
        {
            await using var conn = new Npgsql.NpgsqlConnection(controlPlaneConnStr);
            await conn.OpenAsync();

            await using var cmd = new Npgsql.NpgsqlCommand("SELECT code, id FROM branches", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                mapa[r.GetString(0)] = r.GetGuid(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"::aviso:: não foi possível ler as filiais do control plane: {ex.Message}");
        }

        return mapa;
    }
}
