using System.Collections.Concurrent;

namespace Stoctable.Infrastructure.Tenancy;

/// <summary>
/// Cache singleton de connection strings por filial,
/// evitando múltiplas chamadas ao Azure KeyVault por requisição.
/// </summary>
public class BranchConnectionCache
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public bool TryGet(string branchId, out string connectionString)
        => _cache.TryGetValue(branchId, out connectionString!);

    public void Set(string branchId, string connectionString)
        => _cache[branchId] = connectionString;
}
