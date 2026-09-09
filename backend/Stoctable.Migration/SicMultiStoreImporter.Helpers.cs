using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;
using System.Text;

namespace Stoctable.Migration;

public partial class SicMultiStoreImporter
{
    /// <summary>
    /// Contador global de SKU. Não reinicia por loja de propósito: o sku é único
    /// no banco inteiro, então a numeração corre contínua e cada loja acaba
    /// ocupando uma faixa (MEGA 1…, depois MOTO, depois VILLA).
    /// </summary>
    private int _skuSequencial;

    private readonly Dictionary<(string Store, int Id), Guid> _sale = [];
    private readonly Dictionary<(string Store, int Id), Guid> _quotation = [];

    /// <summary>
    /// Aplica o corte de histórico. Null importa tudo — as consultas usam
    /// <c>(@since IS NULL OR a.data >= @since)</c> para não precisarem de duas
    /// versões do SQL.
    /// </summary>
    private void AddSince(SqlCommand cmd)
        => cmd.Parameters.AddWithValue("since", (object?)_since ?? DBNull.Value);

    private static async Task<SqlConnection> OpenSicAsync(SicStoreSource store)
    {
        var conn = new SqlConnection(store.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// COPY binário não aceita null implícito: a coluna precisa ser escrita como
    /// nula explicitamente, senão as colunas seguintes saem deslocadas.
    /// </summary>
    private static async Task WriteNullableAsync<T>(NpgsqlBinaryImporter copy, T? value, NpgsqlDbType type)
        where T : struct
    {
        if (value.HasValue) await copy.WriteAsync(value.Value, type);
        else await copy.WriteNullAsync();
    }

    private static async Task WriteNullableAsync(NpgsqlBinaryImporter copy, string? value, NpgsqlDbType type)
    {
        if (value is not null) await copy.WriteAsync(value, type);
        else await copy.WriteNullAsync();
    }

    /// <summary>
    /// Resolve uma FK do SIC (coluna nula ou apontando para registro que já não
    /// existe é comum na base legada) para o id do Stoctable.
    /// </summary>
    private static Guid? Lookup(
        Dictionary<(string, int), Guid> mapa, string store, SqlDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        return mapa.TryGetValue((store, r.GetInt32(ordinal)), out var id) ? id : null;
    }

    private static async Task ExecAsync(NpgsqlConnection pg, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, pg) { CommandTimeout = 0 };
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? Str(SqlDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        var v = r.GetValue(ordinal)?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string? Cut(string? s, int max)
        => s is null ? null : (s.Length <= max ? s : s[..max]);

    /// <summary>CPF/CNPJ sem pontuação. Vazio ou tamanho inválido vira null.</summary>
    private static string? OnlyDigits(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var digits = new string([.. s.Where(char.IsDigit)]);
        return digits.Length is 11 or 14 ? digits : null;
    }

    private static string? Join(params string?[] parts)
    {
        var joined = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? Address(string? endereco, string? bairro, string? cidade, string? estado, string? cep)
        => Join(endereco, bairro, cidade, estado, cep);

    /// <summary>
    /// Usuário legível a partir do nome. Colisão entre lojas é resolvida por
    /// reuso — dois "João" viram a mesma conta, o que é aceitável porque estas
    /// contas não fazem login (existem só para atribuir vendas antigas).
    /// </summary>
    private static string Username(string nome)
    {
        var sb = new StringBuilder();
        foreach (var c in nome.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '.' or '-' && sb.Length > 0 && sb[^1] != '.') sb.Append('.');
        }

        var s = sb.ToString().Trim('.');
        return string.IsNullOrWhiteSpace(s) ? $"user{Guid.NewGuid():N}"[..12] : s;
    }

    private async Task PrintSummaryAsync(NpgsqlConnection pg)
    {
        Log("\n─── Resumo no destino ───");

        string[] tabelas =
        [
            "suppliers", "manufacturers", "customers", "users", "payment_methods",
            "products", "product_stocks", "sales", "sale_items", "payments",
            "quotations", "quotation_items",
        ];

        foreach (var t in tabelas)
        {
            await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {t}", pg);
            Log($"  {t,-18} {Convert.ToInt64(await cmd.ExecuteScalarAsync()),12:N0}");
        }
    }

    private static void Log(string msg) => Console.WriteLine(msg);
}
