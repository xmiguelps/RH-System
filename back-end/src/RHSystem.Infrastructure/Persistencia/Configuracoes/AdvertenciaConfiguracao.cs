using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class AdvertenciaConfiguracao : IEntityTypeConfiguration<Advertencia>
{
    public void Configure(EntityTypeBuilder<Advertencia> builder)
    {
        builder.ToTable("ADVERTENCIAS");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID_ADVERTENCIA");
        builder.Property(a => a.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(a => a.GestorId).HasColumnName("ID_GESTOR");

        // Quase toda coluna aceita NULL no banco e por isso a propriedade também; a aplicação sempre preenche (o trigger só preencheria a situação e a data com o CURDATE() real).
        builder.Property(a => a.Data).HasColumnName("DATA_ADVERTENCIA");
        builder.Property(a => a.Tipo).HasColumnName("TIPO_ADVERTENCIA").HasConversion(new EnumComoTextoMaiusculo<TipoDeAdvertencia>());
        builder.Property(a => a.Motivo).HasColumnName("MOTIVO");
        builder.Property(a => a.Descricao).HasColumnName("DESCRICAO");
        builder.Property(a => a.Situacao).HasColumnName("STATUS_ADVERTENCIA").HasConversion(new EnumComoTextoMaiusculo<SituacaoDaAdvertencia>());

        // Tabela do grupo: -03:00 no banco, UTC na aplicação.
        builder.Property(a => a.CienciaEmUtc).HasColumnName("DATA_CIENCIA").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(a => a.TextoContestacao).HasColumnName("TEXTO_CONTESTACAO");
        builder.Property(a => a.ContestacaoEmUtc).HasColumnName("DATA_CONTESTACAO").HasConversion(ConversoresDeTempo.Brasil);

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(a => a.FuncionarioId);
        builder.HasOne<Gestor>().WithMany().HasForeignKey(a => a.GestorId);
    }
}
