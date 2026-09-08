using System.Collections.Concurrent;

namespace Stoctable.Infrastructure.Tenancy;

/// <summary>
/// Cache singleton de connection strings por filial,
/// evitando múltiplas chamadas ao Azure KeyVault por requisição.
///
/// As entradas expiram: antes disso o cache era permanente, então trocar um
/// segredo no Key Vault (rotação de senha, correção de valor inválido) só
/// surtia efeito depois de reiniciar o App Service.
/// </summary>
public class BranchConnectionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Entry> _cache = new();

    private readonly record struct Entry(string ConnectionString, DateTimeOffset ExpiresAt);

    public bool TryGet(string branchId, out string connectionString)
    {
        if (_cache.TryGetValue(branchId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            connectionString = entry.ConnectionString;
            return true;
        }

        // Entrada vencida sai na leitura: sem isso, uma filial desativada ficaria
        // ocupando memória para sempre.
        _cache.TryRemove(branchId, out _);
        connectionString = null!;
        return false;
    }

    public void Set(string branchId, string connectionString)
        => _cache[branchId] = new Entry(connectionString, DateTimeOffset.UtcNow.Add(Ttl));

    /// <summary>Descarta a entrada de uma filial, forçando releitura no próximo acesso.</summary>
    public void Invalidate(string branchId) => _cache.TryRemove(branchId, out _);

    /// <summary>Descarta tudo. Usado após rotação de segredos em massa.</summary>
    public void Clear() => _cache.Clear();
}
