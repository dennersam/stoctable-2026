namespace Stoctable.Domain.Entities;

/// <summary>
/// Sequência de números por prefixo (ex: ORC202605, VDA202605). Usada para
/// gerar identificadores de documento atomicamente via INSERT...ON CONFLICT
/// DO UPDATE no PostgreSQL, sem race condition.
/// </summary>
public class NumberSequence
{
    public string Prefix { get; set; } = string.Empty;
    public long CurrentValue { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
