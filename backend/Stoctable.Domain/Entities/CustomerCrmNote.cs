namespace Stoctable.Domain.Entities;

public class CustomerCrmNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Note { get; set; } = string.Empty;

    /// <summary>general | complaint | followup</summary>
    public string NoteType { get; set; } = "general";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";

    public Customer? Customer { get; set; }
}
