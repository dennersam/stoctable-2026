namespace Stoctable.Domain.Contracts;

/// <summary>
/// Abstração mínima para envelopar operações multi-repositório em uma única
/// transação no banco. Cada Repository.SaveChangesAsync interno permanece
/// fazendo flush, mas o commit final é único — em caso de exceção tudo
/// é revertido.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
