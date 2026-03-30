using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Infrastructure.Context.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SaleNumber).HasColumnName("sale_number").HasMaxLength(20).IsRequired();
        builder.Property(s => s.QuotationId).HasColumnName("quotation_id");
        builder.Property(s => s.CustomerId).HasColumnName("customer_id");
        builder.Property(s => s.SalespersonId).HasColumnName("salesperson_id");
        builder.Property(s => s.CashierId).HasColumnName("cashier_id");
        builder.Property(s => s.Status).HasColumnName("status")
            .HasConversion(
                s => ConvertStatusToString(s),
                s => ConvertStringToStatus(s));
        builder.Property(s => s.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        builder.Property(s => s.DiscountAmount).HasColumnName("discount_amount").HasPrecision(12, 2);
        builder.Property(s => s.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2);
        builder.Property(s => s.AmountPaid).HasColumnName("amount_paid").HasPrecision(12, 2);
        builder.Property(s => s.Notes).HasColumnName("notes");
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(s => s.SaleNumber).IsUnique();

        builder.HasOne(s => s.Customer).WithMany(c => c.Sales).HasForeignKey(s => s.CustomerId);
        builder.HasOne(s => s.Salesperson).WithMany().HasForeignKey(s => s.SalespersonId);
        builder.HasOne(s => s.Cashier).WithMany().HasForeignKey(s => s.CashierId);
        builder.HasMany(s => s.Items).WithOne(i => i.Sale).HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Payments).WithOne(p => p.Sale).HasForeignKey(p => p.SaleId);
    }

    private static string ConvertStatusToString(SaleStatus s) =>
        s == SaleStatus.PendingPayment ? "pending_payment" :
        s == SaleStatus.PartiallyPaid ? "partially_paid" :
        s == SaleStatus.Paid ? "paid" : "cancelled";

    private static SaleStatus ConvertStringToStatus(string s) =>
        s == "pending_payment" ? SaleStatus.PendingPayment :
        s == "partially_paid" ? SaleStatus.PartiallyPaid :
        s == "paid" ? SaleStatus.Paid : SaleStatus.Cancelled;
}
