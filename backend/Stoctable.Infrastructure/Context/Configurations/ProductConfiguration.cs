using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;

namespace Stoctable.Infrastructure.Context.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Barcode).HasColumnName("barcode").HasMaxLength(50);
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(p => p.Manufacturer).HasColumnName("manufacturer").HasMaxLength(100);
        builder.Property(p => p.CategoryId).HasColumnName("category_id");
        builder.Property(p => p.SupplierId).HasColumnName("supplier_id");
        builder.Property(p => p.CostPrice).HasColumnName("cost_price").HasPrecision(12, 2);
        builder.Property(p => p.SalePrice).HasColumnName("sale_price").HasPrecision(12, 2);
        builder.Property(p => p.StockQuantity).HasColumnName("stock_quantity").HasPrecision(10, 3);
        builder.Property(p => p.StockReserved).HasColumnName("stock_reserved").HasPrecision(10, 3);
        builder.Property(p => p.StockMinimum).HasColumnName("stock_minimum").HasPrecision(10, 3);
        builder.Property(p => p.Unit).HasColumnName("unit").HasMaxLength(10);
        builder.Property(p => p.IcmsRate).HasColumnName("icms_rate").HasPrecision(5, 2);
        builder.Property(p => p.IpiRate).HasColumnName("ipi_rate").HasPrecision(5, 2);
        builder.Property(p => p.Cst).HasColumnName("cst").HasMaxLength(10);
        builder.Property(p => p.Ncm).HasColumnName("ncm").HasMaxLength(10);
        builder.Property(p => p.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.Notes).HasColumnName("notes");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.Barcode);

        builder.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId);
        builder.HasOne(p => p.Supplier).WithMany(s => s.Products).HasForeignKey(p => p.SupplierId);
    }
}
