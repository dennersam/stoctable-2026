using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stoctable.Domain.Entities;

namespace Stoctable.Infrastructure.Context.Configurations;

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("number_sequences");

        // Chave composta (filial, prefixo): cada loja tem a própria contagem.
        // É também o alvo do ON CONFLICT no gerador — mudar aqui exige mudar lá.
        builder.HasKey(x => new { x.BranchId, x.Prefix });
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.Prefix).HasColumnName("prefix").HasMaxLength(40);
        builder.Property(x => x.CurrentValue).HasColumnName("current_value");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
