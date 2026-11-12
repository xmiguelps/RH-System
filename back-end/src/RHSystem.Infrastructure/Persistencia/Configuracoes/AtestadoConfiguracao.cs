using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class AtestadoConfiguracao : IEntityTypeConfiguration<Atestado>
{
    public void Configure(EntityTypeBuilder<Atestado> builder)
    {
        builder.ToTable("ATESTADOS");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID_ATESTADO");
        builder.Property(a => a.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(a => a.DataInicio).HasColumnName("DATA_INICIO");
        builder.Property(a => a.DataFim).HasColumnName("DATA_FIM");
        builder.Property(a => a.HoraInicio).HasColumnName("HORA_INICIO").HasColumnType("time");
        builder.Property(a => a.HoraFim).HasColumnName("HORA_FIM").HasColumnType("time");

        // O caminho é segredo de armazenamento e o nome do arquivo pode revelar conteúdo de saúde: nenhum dos dois vai para log, auditoria ou resposta.
        builder.Property(a => a.CaminhoArquivo).HasColumnName("CAMINHO_ARQUIVO");
        builder.Property(a => a.NomeArquivoOriginal).HasColumnName("NOME_ARQUIVO_ORIGINAL");

        // O padrão do banco ('ENVIADO') não é declarado: o EF sempre envia o valor. É também o token de concorrência: validar e recusar fazem UPDATE ... WHERE STATUS_ATESTADO = 'ENVIADO' (a segunda decisão não acha a linha).
        builder.Property(a => a.Situacao).HasColumnName("STATUS_ATESTADO").HasConversion(new EnumComoTextoMaiusculo<SituacaoDoAtestado>()).IsConcurrencyToken();
        builder.Property(a => a.FuncionarioRhId).HasColumnName("ID_FUNCIONARIO_RH");

        // Tabela do grupo: -03:00 no banco, UTC na aplicação. A data de envio vem do relógio da aplicação (o padrão do banco, CURRENT_TIMESTAMP, é o relógio do servidor; marcada como gerada na inclusão porque o banco tem o padrão, e o EF envia o valor quando ele foi preenchido).
        builder.Property(a => a.DecididoEmUtc).HasColumnName("DATA_DECISAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(a => a.MotivoRecusa).HasColumnName("MOTIVO_RECUSA");
        builder.Property(a => a.EnviadoEmUtc).HasColumnName("DATA_ENVIO").HasConversion(ConversoresDeTempo.Brasil).ValueGeneratedOnAdd();

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(a => a.FuncionarioId);
    }
}
