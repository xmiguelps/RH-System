using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Importacoes;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class ImportacaoConfiguracao : IEntityTypeConfiguration<Importacao>
{
    public void Configure(EntityTypeBuilder<Importacao> builder)
    {
        builder.ToTable("IMPORTACOES");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID_IMPORTACAO");
        builder.Property(i => i.FuncionarioRhId).HasColumnName("ID_FUNCIONARIO_RH");
        builder.Property(i => i.NomeArquivo).HasColumnName("NOME_ARQUIVO");
        builder.Property(i => i.Situacao).HasColumnName("SITUACAO").HasConversion(new EnumComoTextoMaiusculo<SituacaoDaImportacao>());
        builder.Property(i => i.TotalLinhas).HasColumnName("TOTAL_LINHAS").HasColumnType("smallint");
        builder.Property(i => i.LinhasValidas).HasColumnName("LINHAS_VALIDAS").HasColumnType("smallint");
        builder.Property(i => i.LinhasComErro).HasColumnName("LINHAS_COM_ERRO").HasColumnType("smallint");
        builder.Property(i => i.CriadaEm).HasColumnName("CRIADA_EM").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(i => i.ExpiraEm).HasColumnName("EXPIRA_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(i => i.CaminhoArquivo).HasColumnName("CAMINHO_ARQUIVO");

        // JSON com dado pessoal (a prévia da planilha): o banco recusa CONFIRMADA com prévia; o MySQL normaliza o JSON ao gravar.
        builder.Property(i => i.Previa).HasColumnName("PREVIA").HasColumnType("json");
        builder.Property(i => i.ConfirmadaEm).HasColumnName("CONFIRMADA_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(i => i.TotalCriados).HasColumnName("TOTAL_CRIADOS").HasColumnType("smallint");
        builder.Property(i => i.ConvitesEnviados).HasColumnName("CONVITES_ENVIADOS").HasColumnType("smallint");
        builder.Property(i => i.IncluidosNoOnboarding).HasColumnName("INCLUIDOS_NO_ONBOARDING").HasColumnType("smallint");
        builder.Property(i => i.IncluidosNoFormularioAdmissao).HasColumnName("INCLUIDOS_NO_FORMULARIO_ADMISSAO").HasColumnType("smallint");

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(i => i.FuncionarioRhId);
    }
}
