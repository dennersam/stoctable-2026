using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;

namespace Stoctable.Infrastructure.Context.Configurations;

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.ProductId).HasColumnName("product_id");
        builder.Property(r => r.QuotationId).HasColumnName("quotation_id");
        builder.Property(r => r.Quantity).HasColumnName("quantity").HasPrecision(10, 3);
        builder.Property(r => r.ReservedAt).HasColumnName("reserved_at");
        builder.Property(r => r.ReleasedAt).HasColumnName("released_at");
        builder.Property(r => r.IsActive).HasColumnName("is_active");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(100);

        builder.HasOne(r => r.Product).WithMany(p => p.StockReservations).HasForeignKey(r => r.ProductId);
        builder.HasOne(r => r.Quotation).WithMany(q => q.StockReservations).HasForeignKey(r => r.QuotationId);
    }
}
