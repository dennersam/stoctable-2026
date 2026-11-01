using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Contracts.Repositories;

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Bloqueia a linha da venda até o fim da transação e só então a carrega. Exige
    /// transação aberta: fora dela o bloqueio termina junto com o comando.
    /// </summary>
    Task<Sale?> GetForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Sale>> GetByStatusAsync(SaleStatus status, CancellationToken ct = default);
    Task<string> GenerateNextNumberAsync(CancellationToken ct = default);
}
