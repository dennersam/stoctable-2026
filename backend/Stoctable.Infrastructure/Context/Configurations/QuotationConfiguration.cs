using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Infrastructure.Context.Configurations;

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("quotations");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.QuotationNumber).HasColumnName("quotation_number").HasMaxLength(20).IsRequired();
        builder.Property(q => q.CustomerId).HasColumnName("customer_id");
        builder.Property(q => q.SalespersonId).HasColumnName("salesperson_id");
        builder.Property(q => q.Status).HasColumnName("status")
            .HasConversion(
                s => ConvertStatusToString(s),
                s => ConvertStringToStatus(s));
        builder.Property(q => q.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        builder.Property(q => q.DiscountPct).HasColumnName("discount_pct").HasPrecision(5, 2);
        builder.Property(q => q.DiscountAmount).HasColumnName("discount_amount").HasPrecision(12, 2);
        builder.Property(q => q.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2);
        builder.Property(q => q.Notes).HasColumnName("notes");
        builder.Property(q => q.ValidUntil).HasColumnName("valid_until");
        builder.Property(q => q.FinalizedAt).HasColumnName("finalized_at");
        builder.Property(q => q.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(q => q.CancellationReason).HasColumnName("cancellation_reason");
        builder.Property(q => q.ConvertedToSaleId).HasColumnName("converted_to_sale_id");
        builder.Property(q => q.CreatedAt).HasColumnName("created_at");
        builder.Property(q => q.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at");
        builder.Property(q => q.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(q => q.QuotationNumber).IsUnique();

        builder.HasOne(q => q.Customer).WithMany(c => c.Quotations).HasForeignKey(q => q.CustomerId);
        builder.HasOne(q => q.Salesperson).WithMany().HasForeignKey(q => q.SalespersonId);
        builder.HasMany(q => q.Items).WithOne(i => i.Quotation).HasForeignKey(i => i.QuotationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(q => q.StockReservations).WithOne(r => r.Quotation).HasForeignKey(r => r.QuotationId);
    }

    private static string ConvertStatusToString(QuotationStatus s) =>
        s == QuotationStatus.Draft ? "draft" :
        s == QuotationStatus.Finalized ? "finalized" :
        s == QuotationStatus.Converted ? "converted" : "cancelled";

    private static QuotationStatus ConvertStringToStatus(string s) =>
        s == "draft" ? QuotationStatus.Draft :
        s == "finalized" ? QuotationStatus.Finalized :
        s == "converted" ? QuotationStatus.Converted : QuotationStatus.Cancelled;
}
