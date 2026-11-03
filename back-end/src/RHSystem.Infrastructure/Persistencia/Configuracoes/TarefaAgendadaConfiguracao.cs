using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Agendamento;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class TarefaAgendadaConfiguracao : IEntityTypeConfiguration<TarefaAgendada>
{
    public void Configure(EntityTypeBuilder<TarefaAgendada> builder)
    {
        builder.ToTable("TAREFAS_AGENDADAS");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID_TAREFA");
        builder.Property(t => t.Tipo).HasColumnName("TIPO");
        builder.Property(t => t.ChaveDeIdempotencia).HasColumnName("CHAVE_IDEMPOTENCIA");
        builder.Property(t => t.ReferenciaId).HasColumnName("REFERENCIA_ID");
        builder.Property(t => t.DiaReferencia).HasColumnName("DIA_REFERENCIA");

        // JSON: o MySQL normaliza (ordem das chaves, espaços); quem lê interpreta o JSON, não compara o texto.
        builder.Property(t => t.Parametros).HasColumnName("PARAMETROS").HasColumnType("json");
        builder.Property(t => t.Situacao).HasColumnName("SITUACAO").HasConversion(new EnumComoTextoMaiusculo<SituacaoDaTarefa>());
        builder.Property(t => t.ExecutarApos).HasColumnName("EXECUTAR_APOS").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(t => t.Tentativas).HasColumnName("TENTATIVAS").HasColumnType("smallint");
        builder.Property(t => t.ReservadaAte).HasColumnName("RESERVADA_ATE").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(t => t.UltimaTentativaEm).HasColumnName("ULTIMA_TENTATIVA_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(t => t.ConcluidaEm).HasColumnName("CONCLUIDA_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(t => t.UltimoErro).HasColumnName("ULTIMO_ERRO");
        builder.Property(t => t.CriadaEm).HasColumnName("CRIADA_EM").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
    }
}
