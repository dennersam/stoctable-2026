using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;

namespace Stoctable.Infrastructure.Context.Configurations;

public class ManufacturerConfiguration : IEntityTypeConfiguration<Manufacturer>
{
    public void Configure(EntityTypeBuilder<Manufacturer> builder)
    {
        builder.ToTable("manufacturers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(m => m.Notes).HasColumnName("notes");
        builder.Property(m => m.IsActive).HasColumnName("is_active");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(m => m.Name).IsUnique();
    }
}
