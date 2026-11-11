using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Documentos;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class DocumentoDoColaboradorConfiguracao : IEntityTypeConfiguration<DocumentoDoColaborador>
{
    public void Configure(EntityTypeBuilder<DocumentoDoColaborador> builder)
    {
        builder.ToTable("FUNCIONARIOS_DOCUMENTOS");
        builder.Ignore(d => d.NomeParaAuditoria);
        builder.Ignore(d => d.RegistroAfetado);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID_FUNCIONARIO_DOCUMENTO");
        builder.Property(d => d.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.HasOne(d => d.Funcionario).WithMany().HasForeignKey(d => d.FuncionarioId);

        // Texto livre no banco, sem CHECK: valor desconhecido lido do banco lança, e só a aplicação grava.
        builder.Property(d => d.Tipo).HasColumnName("TIPO_DOCUMENTO").HasConversion(new EnumComoTextoMaiusculo<TipoDeDocumento>());
        builder.Property(d => d.Descricao).HasColumnName("DESCRICAO");
        builder.Property(d => d.DataValidade).HasColumnName("DATA_VALIDADE");

        // O padrão do banco ('PENDENTE') não é declarado: o EF sempre envia o valor. É também o token de concorrência: o UPDATE do envio leva WHERE STATUS_DOCUMENTO = 'PENDENTE', o que fecha a
        // corrida de dois envios para o mesmo pedido (quem chegar depois não acha a linha).
        builder.Property(d => d.Situacao).HasColumnName("STATUS_DOCUMENTO").HasConversion(new EnumComoTextoMaiusculo<SituacaoDoDocumento>()).IsConcurrencyToken();
        builder.Property(d => d.CaminhoArquivo).HasColumnName("CAMINHO_ARQUIVO");
        builder.Property(d => d.NomeArquivoOriginal).HasColumnName("NOME_ARQUIVO_ORIGINAL");

        // Tabela do grupo: -03:00 no banco, UTC na aplicação. A aplicação grava o pedido e o envio (nenhum trigger os preenche); a data do cadastro é do banco (DEFAULT CURRENT_TIMESTAMP).
        builder.Property(d => d.PedidoEmUtc).HasColumnName("DATA_PEDIDO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(d => d.EnviadoEmUtc).HasColumnName("DATA_ENVIO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(d => d.CadastradoEm).HasColumnName("DATA_CADASTRO").HasConversion(ConversoresDeTempo.Brasil).ValueGeneratedOnAdd();
    }
}
