using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;

namespace Stoctable.Infrastructure.Context.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
        builder.Property(c => c.DocumentType).HasColumnName("document_type").HasMaxLength(5);
        builder.Property(c => c.DocumentNumber).HasColumnName("document_number").HasMaxLength(18);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(150);
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(c => c.Mobile).HasColumnName("mobile").HasMaxLength(20);
        builder.Property(c => c.Address).HasColumnName("address").HasMaxLength(255);
        builder.Property(c => c.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(c => c.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(c => c.ZipCode).HasColumnName("zip_code").HasMaxLength(10);
        builder.Property(c => c.CustomerTypeId).HasColumnName("customer_type_id");
        builder.Property(c => c.CreditLimit).HasColumnName("credit_limit").HasPrecision(12, 2);
        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(c => c.DocumentNumber).IsUnique();

        builder.HasOne(c => c.CustomerType).WithMany(ct => ct.Customers).HasForeignKey(c => c.CustomerTypeId);
        builder.HasMany(c => c.CrmNotes).WithOne(n => n.Customer).HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Quotations).WithOne(q => q.Customer).HasForeignKey(q => q.CustomerId);
        builder.HasMany(c => c.Sales).WithOne(s => s.Customer).HasForeignKey(s => s.CustomerId);
    }
}
