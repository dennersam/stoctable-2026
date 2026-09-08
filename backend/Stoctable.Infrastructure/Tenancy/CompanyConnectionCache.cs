using System.Collections.Concurrent;

namespace Stoctable.Infrastructure.Tenancy;

/// <summary>
/// Cache de connection string por EMPRESA (antes era por filial, quando cada
/// filial tinha um banco). Singleton, com expiração e invalidação explícita —
/// sem elas, trocar a senha do banco exigiria reiniciar a aplicação.
/// </summary>
public class CompanyConnectionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, Entry> _cache = new();

    private readonly record struct Entry(string ConnectionString, DateTimeOffset ExpiresAt);

    public bool TryGet(Guid companyId, out string connectionString)
    {
        if (_cache.TryGetValue(companyId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            connectionString = entry.ConnectionString;
            return true;
        }

        _cache.TryRemove(companyId, out _);
        connectionString = null!;
        return false;
    }

    public void Set(Guid companyId, string connectionString)
        => _cache[companyId] = new Entry(connectionString, DateTimeOffset.UtcNow.Add(Ttl));

    /// <summary>Chamado ao concluir o provisionamento e ao suspender uma empresa.</summary>
    public void Invalidate(Guid companyId) => _cache.TryRemove(companyId, out _);

    public void Clear() => _cache.Clear();
}
