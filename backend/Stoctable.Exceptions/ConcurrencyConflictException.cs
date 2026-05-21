namespace Stoctable.Exceptions;

/// <summary>
/// Indica que uma operação foi abortada porque outro processo modificou
/// o mesmo dado entre o carregamento e o salvamento (optimistic concurrency).
/// Lançada pela camada de Infrastructure ao capturar DbUpdateConcurrencyException
/// — permite que a Application reaja sem referenciar EF Core.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
    public ConcurrencyConflictException(string message, Exception inner) : base(message, inner) { }
}
