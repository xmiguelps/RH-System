using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Ponto;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class MarcacaoDePontoConfiguracao : IEntityTypeConfiguration<MarcacaoDePonto>
{
    public void Configure(EntityTypeBuilder<MarcacaoDePonto> builder)
    {
        builder.ToTable("PONTOS_MARCACOES");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID_MARCACAO");
        builder.Property(m => m.PontoId).HasColumnName("ID_PONTO");

        // Tabela do grupo: -03:00 no banco, UTC na aplicação. A entidade já corta o instante ao segundo (a coluna não tem fração).
        builder.Property(m => m.InstanteUtc).HasColumnName("DATA_HORA_MARCACAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(m => m.Tipo).HasColumnName("TIPO_MARCACAO").HasConversion(new EnumComoTextoMaiusculo<TipoDeMarcacao>());

        // O padrão do banco ('SISTEMA') não é declarado: o EF sempre envia o valor. O banco exige ORIGEM = 'CORRIGIDA_RH' junto com DATA_CORRECAO.
        builder.Property(m => m.Origem).HasColumnName("ORIGEM").HasConversion(new EnumComoTextoMaiusculo<OrigemDaMarcacao>());
        builder.Property(m => m.CorrigidaEmUtc).HasColumnName("DATA_CORRECAO").HasConversion(ConversoresDeTempo.Brasil);

        builder.HasOne<PontoDoDia>().WithMany().HasForeignKey(m => m.PontoId);
    }
}
