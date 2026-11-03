using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class BeneficioConfiguracao : IEntityTypeConfiguration<Beneficio>
{
    public void Configure(EntityTypeBuilder<Beneficio> builder)
    {
        builder.ToTable("BENEFICIOS");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID_BENEFICIO");
        builder.Property(b => b.Nome).HasColumnName("NOME_BENEFICIO");
        builder.Property(b => b.Descricao).HasColumnName("DESCRICAO");

        // As três marcas têm DEFAULT FALSE no banco; o padrão não é declarado aqui para o EF sempre enviar o valor que a entidade tem.
        builder.Property(b => b.Opcional).HasColumnName("OPCIONAL");
        builder.Property(b => b.AceitaDependentes).HasColumnName("ACEITA_DEPENDENTES");
        builder.Property(b => b.AceitaTrajeto).HasColumnName("ACEITA_TRAJETO");
    }
}
