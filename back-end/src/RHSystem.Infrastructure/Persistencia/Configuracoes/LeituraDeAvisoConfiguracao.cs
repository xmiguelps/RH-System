using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class LeituraDeAvisoConfiguracao : IEntityTypeConfiguration<LeituraDeAviso>
{
    public void Configure(EntityTypeBuilder<LeituraDeAviso> builder)
    {
        builder.ToTable("AVISOS_VISUALIZACOES");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID_AVISO_VISUALIZACAO");
        builder.Property(l => l.AvisoId).HasColumnName("ID_AVISO");
        builder.Property(l => l.FuncionarioId).HasColumnName("ID_FUNCIONARIO");

        // O padrão do banco (VISUALIZADO = FALSE) não é declarado: o EF sempre envia o valor. A data de leitura vem da aplicação (o trigger poria o NOW() real).
        builder.Property(l => l.Visualizado).HasColumnName("VISUALIZADO");
        builder.Property(l => l.VisualizadoEmUtc).HasColumnName("DATA_VISUALIZACAO").HasConversion(ConversoresDeTempo.Brasil);

        builder.HasIndex(l => new { l.AvisoId, l.FuncionarioId }).IsUnique().HasDatabaseName("UQ_AVISOS_VISUALIZACOES_AVISO_FUNCIONARIO");
        builder.HasOne<Aviso>().WithMany().HasForeignKey(l => l.AvisoId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(l => l.FuncionarioId);
    }
}
