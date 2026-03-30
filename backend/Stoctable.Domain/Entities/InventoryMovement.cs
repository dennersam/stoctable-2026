using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities;

public class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public MovementType MovementType { get; set; }

    /// <summary>Positivo = entrada, negativo = saída</summary>
    public decimal Quantity { get; set; }

    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }

    /// <summary>Tipo da referência: 'sale', 'quotation', 'adjustment'</summary>
    public string? ReferenceType { get; set; }

    /// <summary>ID da venda, orçamento ou ajuste relacionado</summary>
    public Guid? ReferenceId { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";

    public Product? Product { get; set; }
}
