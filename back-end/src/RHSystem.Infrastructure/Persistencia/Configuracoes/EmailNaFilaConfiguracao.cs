using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Emails;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class EmailNaFilaConfiguracao : IEntityTypeConfiguration<EmailNaFila>
{
    public void Configure(EntityTypeBuilder<EmailNaFila> builder)
    {
        builder.ToTable("FILA_EMAILS");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID_EMAIL");
        builder.Property(e => e.Tipo).HasColumnName("TIPO");
        builder.Property(e => e.ChaveDeIdempotencia).HasColumnName("CHAVE_IDEMPOTENCIA");
        builder.Property(e => e.ReferenciaId).HasColumnName("REFERENCIA_ID");
        builder.Property(e => e.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(e => e.DestinatarioEmail).HasColumnName("DESTINATARIO_EMAIL");
        builder.Property(e => e.Sigiloso).HasColumnName("SIGILOSO");

        // JSON: o MySQL normaliza (ordem das chaves, espaços); quem lê interpreta o JSON, não compara o texto.
        builder.Property(e => e.Carga).HasColumnName("CARGA").HasColumnType("json");
        builder.Property(e => e.Situacao).HasColumnName("SITUACAO").HasConversion(new EnumComoTextoMaiusculo<SituacaoDoEmail>());
        builder.Property(e => e.Tentativas).HasColumnName("TENTATIVAS").HasColumnType("smallint");
        builder.Property(e => e.ProximaTentativaEm).HasColumnName("PROXIMA_TENTATIVA_EM").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(e => e.ReservadoAte).HasColumnName("RESERVADO_ATE").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(e => e.UltimoErro).HasColumnName("ULTIMO_ERRO");
        builder.Property(e => e.CriadoEm).HasColumnName("CRIADO_EM").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(e => e.EnviadoEm).HasColumnName("ENVIADO_EM").HasConversion(ConversoresDeTempo.Utc);

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(e => e.FuncionarioId).IsRequired(false);
    }
}
