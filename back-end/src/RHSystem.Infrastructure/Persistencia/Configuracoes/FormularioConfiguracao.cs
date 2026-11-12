using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Formularios;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class FormularioConfiguracao : IEntityTypeConfiguration<Formulario>
{
    public void Configure(EntityTypeBuilder<Formulario> builder)
    {
        builder.ToTable("FORMULARIOS");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("ID_FORMULARIO");
        builder.Property(f => f.Titulo).HasColumnName("TITULO");
        builder.Property(f => f.Descricao).HasColumnName("DESCRICAO");
        builder.Property(f => f.AutorId).HasColumnName("ID_FUNCIONARIO_AUTOR");

        // Quase toda coluna aceita NULL no banco e por isso a propriedade também; a aplicação sempre preenche o tipo e a data de criação (o trigger poria o NOW() real).
        builder.Property(f => f.Tipo).HasColumnName("TIPO_FORMULARIO").HasConversion(new EnumComoTextoMaiusculo<TipoDeFormulario>());
        builder.Property(f => f.SentidoAvaliacao).HasColumnName("SENTIDO_AVALIACAO").HasConversion(new EnumComoTextoMaiusculo<SentidoDaAvaliacao>());
        builder.Property(f => f.Alvo).HasColumnName("PUBLICO").HasConversion(new EnumComoTextoMaiusculo<AlvoDoFormulario>());
        builder.Property(f => f.RecorrenciaUnidade).HasColumnName("RECORRENCIA_UNIDADE").HasConversion(new EnumComoTextoMaiusculo<UnidadeDeRecorrencia>());
        builder.Property(f => f.RecorrenciaIntervalo).HasColumnName("RECORRENCIA_INTERVALO").HasColumnType("smallint");
        builder.Property(f => f.Prazo).HasColumnName("DATA_PRAZO");
        builder.Property(f => f.RecorrenciaAte).HasColumnName("RECORRENCIA_ATE");
        builder.Property(f => f.TokenDoLink).HasColumnName("LINK_PUBLICO_CODIGO");

        // Tabela do grupo: -03:00 no banco, UTC na aplicação.
        builder.Property(f => f.CriadoEmUtc).HasColumnName("DATA_CRIACAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.PublicadoEmUtc).HasColumnName("DATA_PUBLICACAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.EncerradoEmUtc).HasColumnName("DATA_ENCERRAMENTO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.ProximaReaberturaEmUtc).HasColumnName("PROXIMA_REABERTURA").HasConversion(ConversoresDeTempo.Brasil);

        // Os padrões do banco NÃO são declarados: ATIVO (TRUE) mentiria (um rascunho nasceria aceitando resposta), ANONIMO e LEMBRETE_AUTOMATICO são enviados sempre, inclusive o "falso".
        builder.Property(f => f.Ativo).HasColumnName("ATIVO");
        builder.Property(f => f.Anonimo).HasColumnName("ANONIMO");
        builder.Property(f => f.LembreteAutomatico).HasColumnName("LEMBRETE_AUTOMATICO");

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(f => f.AutorId);
    }
}
