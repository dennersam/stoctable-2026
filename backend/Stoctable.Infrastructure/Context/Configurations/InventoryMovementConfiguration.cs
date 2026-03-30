using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Infrastructure.Context.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ProductId).HasColumnName("product_id");
        builder.Property(m => m.MovementType).HasColumnName("movement_type")
            .HasConversion(
                t => ConvertTypeToString(t),
                s => ConvertStringToType(s));
        builder.Property(m => m.Quantity).HasColumnName("quantity").HasPrecision(10, 3);
        builder.Property(m => m.QuantityBefore).HasColumnName("quantity_before").HasPrecision(10, 3);
        builder.Property(m => m.QuantityAfter).HasColumnName("quantity_after").HasPrecision(10, 3);
        builder.Property(m => m.ReferenceType).HasColumnName("reference_type").HasMaxLength(50);
        builder.Property(m => m.ReferenceId).HasColumnName("reference_id");
        builder.Property(m => m.Notes).HasColumnName("notes");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").HasMaxLength(100);

        builder.HasOne(m => m.Product).WithMany(p => p.InventoryMovements).HasForeignKey(m => m.ProductId);
    }

    private static string ConvertTypeToString(MovementType t) =>
        t == MovementType.AdjustmentIn ? "adjustment_in" :
        t == MovementType.AdjustmentOut ? "adjustment_out" :
        t == MovementType.ReservationRelease ? "reservation_release" :
        t == MovementType.TransferIn ? "transfer_in" :
        t == MovementType.TransferOut ? "transfer_out" :
        t.ToString().ToLowerInvariant();

    private static MovementType ConvertStringToType(string s) =>
        s == "adjustment_in" ? MovementType.AdjustmentIn :
        s == "adjustment_out" ? MovementType.AdjustmentOut :
        s == "reservation_release" ? MovementType.ReservationRelease :
        s == "transfer_in" ? MovementType.TransferIn :
        s == "transfer_out" ? MovementType.TransferOut :
        Enum.Parse<MovementType>(s, true);
}
