using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;

namespace Stoctable.Infrastructure.Context.Configurations;

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("number_sequences");
        builder.HasKey(x => x.Prefix);
        builder.Property(x => x.Prefix).HasColumnName("prefix").HasMaxLength(20);
        builder.Property(x => x.CurrentValue).HasColumnName("current_value");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
