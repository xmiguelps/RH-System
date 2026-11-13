using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Formularios;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class FormularioPerguntaConfiguracao : IEntityTypeConfiguration<FormularioPergunta>
{
    public void Configure(EntityTypeBuilder<FormularioPergunta> builder)
    {
        builder.ToTable("FORMULARIOS_PERGUNTAS");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID_FORMULARIO_PERGUNTA");
        builder.Property(p => p.FormularioId).HasColumnName("ID_FORMULARIO");
        builder.Property(p => p.Texto).HasColumnName("TEXTO_PERGUNTA");

        // TIPO_RESPOSTA e ORDEM aceitam NULL no banco; a aplicação sempre os preenche. A ordem é única por formulário.
        builder.Property(p => p.Tipo).HasColumnName("TIPO_RESPOSTA").HasConversion(new EnumComoTextoMaiusculo<TipoDePergunta>());
        builder.Property(p => p.Ordem).HasColumnName("ORDEM");
        builder.Property(p => p.Obrigatoria).HasColumnName("OBRIGATORIA");

        builder.HasOne<Formulario>().WithMany().HasForeignKey(p => p.FormularioId);
    }
}

public class FormularioAlternativaConfiguracao : IEntityTypeConfiguration<FormularioAlternativa>
{
    public void Configure(EntityTypeBuilder<FormularioAlternativa> builder)
    {
        builder.ToTable("FORMULARIOS_PERGUNTAS_ALTERNATIVAS");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID_ALTERNATIVA");
        builder.Property(a => a.PerguntaId).HasColumnName("ID_FORMULARIO_PERGUNTA");
        builder.Property(a => a.Texto).HasColumnName("TEXTO_ALTERNATIVA");
        builder.Property(a => a.Ordem).HasColumnName("ORDEM");

        builder.HasOne<FormularioPergunta>().WithMany().HasForeignKey(a => a.PerguntaId);
    }
}
