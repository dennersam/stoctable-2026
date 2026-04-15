using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Infrastructure.Context.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(150).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
        builder.Property(u => u.Role).HasColumnName("role")
            .HasConversion(r => r.ToString().ToLowerInvariant(), s => Enum.Parse<UserRole>(s, true));
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(u => u.IsActive).HasColumnName("is_active");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(u => u.RefreshToken).HasColumnName("refresh_token").HasMaxLength(500);
        builder.Property(u => u.RefreshTokenExpiresAt).HasColumnName("refresh_token_expires_at");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
