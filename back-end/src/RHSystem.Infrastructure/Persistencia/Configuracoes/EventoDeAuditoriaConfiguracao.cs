using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class EventoDeAuditoriaConfiguracao : IEntityTypeConfiguration<EventoDeAuditoria>
{
    public void Configure(EntityTypeBuilder<EventoDeAuditoria> builder)
    {
        builder.ToTable("AUDITORIA_EVENTOS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID_AUDITORIA_EVENTO");
        builder.Property(e => e.Quando).HasColumnName("QUANDO").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(e => e.AtorTipo).HasColumnName("ATOR_TIPO").HasConversion(new EnumComoTextoMaiusculo<TipoDeAtor>());
        builder.Property(e => e.FuncionarioAtorId).HasColumnName("ID_FUNCIONARIO_ATOR");
        builder.Property(e => e.AtorNome).HasColumnName("ATOR_NOME");

        // O CHECK do banco compara com 'Funcionario', 'Gestor' e 'RH' (o nome do membro, sem converter para maiúsculas).
        builder.Property(e => e.AtorPerfil).HasColumnName("ATOR_PERFIL").HasConversion<string>();
        builder.Property(e => e.Area).HasColumnName("AREA").HasConversion(new EnumComoTextoMaiusculo<AuditoriaArea>());
        builder.Property(e => e.Acao).HasColumnName("ACAO").HasConversion(new EnumComoTextoMaiusculo<AuditoriaAcao>());
        builder.Property(e => e.Descricao).HasColumnName("DESCRICAO");
        builder.Property(e => e.RegistroAfetado).HasColumnName("REGISTRO_AFETADO");
        builder.Property(e => e.Entidade).HasColumnName("ENTIDADE");
        builder.Property(e => e.IdRegistro).HasColumnName("ID_REGISTRO");
        builder.Property(e => e.Canal).HasColumnName("CANAL").HasConversion(new EnumComoTextoMaiusculo<CanalDeOrigem>());
        builder.Property(e => e.Ip).HasColumnName("IP");

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(e => e.FuncionarioAtorId).IsRequired(false);
        builder.HasMany(e => e.Mudancas).WithOne().HasForeignKey(m => m.EventoId);
    }
}

public class MudancaDeAuditoriaConfiguracao : IEntityTypeConfiguration<MudancaDeAuditoria>
{
    public void Configure(EntityTypeBuilder<MudancaDeAuditoria> builder)
    {
        builder.ToTable("AUDITORIA_MUDANCAS");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID_AUDITORIA_MUDANCA");
        builder.Property(m => m.EventoId).HasColumnName("ID_AUDITORIA_EVENTO");
        builder.Property(m => m.Ordem).HasColumnName("ORDEM").HasColumnType("smallint");
        builder.Property(m => m.Campo).HasColumnName("CAMPO");
        builder.Property(m => m.ValorAntes).HasColumnName("VALOR_ANTES");
        builder.Property(m => m.ValorDepois).HasColumnName("VALOR_DEPOIS");

        // DEFAULT FALSE no banco, não declarado: o EF sempre envia a marca (um padrão declarado faria o EF omitir o false).
        builder.Property(m => m.ValorOculto).HasColumnName("VALOR_OCULTO");
    }
}
