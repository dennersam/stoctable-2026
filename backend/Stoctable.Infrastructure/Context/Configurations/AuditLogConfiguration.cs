using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Infrastructure.Context.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EntityName).HasColumnName("entity_name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(100).IsRequired();
        builder.Property(a => a.Action).HasColumnName("action")
            .HasConversion(a => a.ToString().ToLowerInvariant(), s => Enum.Parse<AuditAction>(s, true));
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Username).HasColumnName("username").HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(a => a.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at");

        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.OccurredAt);
    }
}
