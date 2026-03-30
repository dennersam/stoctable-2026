namespace Stoctable.Domain.Entities;

/// <summary>
/// Reserva de estoque criada quando um orçamento é finalizado (Opção B).
/// Liberada quando o orçamento é cancelado ou convertido em venda.
/// </summary>
public class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid QuotationId { get; set; }
    public decimal Quantity { get; set; }
    public DateTimeOffset ReservedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";

    public Product? Product { get; set; }
    public Quotation? Quotation { get; set; }
}
