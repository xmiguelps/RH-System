using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class AvisoConfiguracao : IEntityTypeConfiguration<Aviso>
{
    public void Configure(EntityTypeBuilder<Aviso> builder)
    {
        builder.ToTable("AVISOS");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID_AVISO");
        builder.Property(a => a.AutorId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(a => a.Titulo).HasColumnName("TITULO");
        builder.Property(a => a.Conteudo).HasColumnName("CONTEUDO");

        // Tabela do grupo: -03:00 no banco, UTC na aplicação. A data de publicação vem SEMPRE da aplicação (o trigger poria o NOW() do servidor, inclusive no rascunho).
        builder.Property(a => a.PublicacaoEmUtc).HasColumnName("DATA_PUBLICACAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(a => a.ExpiracaoEmUtc).HasColumnName("DATA_EXPIRACAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(a => a.Tipo).HasColumnName("TIPO_AVISO").HasConversion(new EnumComoTextoMaiusculo<TipoDeAviso>());

        // Os padrões do banco (FIXADO = FALSE, ENVIADO = FALSE) não são declarados: o EF sempre envia o valor, inclusive o "falso".
        builder.Property(a => a.Fixado).HasColumnName("FIXADO");
        builder.Property(a => a.Enviado).HasColumnName("ENVIADO");

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(a => a.AutorId);
    }
}
