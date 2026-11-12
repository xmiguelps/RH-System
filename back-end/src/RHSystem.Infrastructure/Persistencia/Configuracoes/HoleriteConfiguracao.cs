using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class HoleriteConfiguracao : IEntityTypeConfiguration<Holerite>
{
    public void Configure(EntityTypeBuilder<Holerite> builder)
    {
        builder.ToTable("HOLERITES");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID_HOLERITE");
        builder.Property(h => h.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(h => h.Mes).HasColumnName("MES");
        builder.Property(h => h.Ano).HasColumnName("ANO");

        // Tabela do grupo: -03:00 no banco, UTC na aplicação. A data de publicação vem da aplicação (o trigger poria o NOW() real).
        builder.Property(h => h.PublicacaoEmUtc).HasColumnName("DATA_PUBLICACAO").HasConversion(ConversoresDeTempo.Brasil);

        // O caminho é segredo de armazenamento: nunca vai para log, auditoria nem resposta.
        builder.Property(h => h.CaminhoArquivo).HasColumnName("CAMINHO_ARQUIVO");

        // Os padrões do banco (TIPO_HOLERITE = 'MENSAL', VERSAO = 1) não são declarados: o EF sempre envia o valor.
        builder.Property(h => h.Tipo).HasColumnName("TIPO_HOLERITE").HasConversion(new EnumComoTextoMaiusculo<TipoDeHolerite>());
        builder.Property(h => h.Versao).HasColumnName("VERSAO");

        builder.HasIndex(h => new { h.FuncionarioId, h.Ano, h.Mes, h.Tipo, h.Versao }).IsUnique().HasDatabaseName("UQ_HOLERITES_COMPETENCIA_TIPO_VERSAO");
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(h => h.FuncionarioId);
    }
}
