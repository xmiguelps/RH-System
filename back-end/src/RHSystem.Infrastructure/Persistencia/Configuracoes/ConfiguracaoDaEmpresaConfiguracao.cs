using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Empresa;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class ConfiguracaoDaEmpresaConfiguracao : IEntityTypeConfiguration<ConfiguracaoDaEmpresa>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoDaEmpresa> builder)
    {
        builder.ToTable("CONFIGURACOES_EMPRESA");
        builder.Ignore(c => c.NomeParaAuditoria);
        builder.Ignore(c => c.RegistroAfetado);
        builder.HasKey(c => c.Id);

        // Linha única: a chave é sempre 1 (o CHECK do banco confere) e a entidade a define; o EF nunca a gera nem manda o padrão do banco.
        builder.Property(c => c.Id).HasColumnName("ID_CONFIGURACAO_EMPRESA").HasColumnType("tinyint").ValueGeneratedNever();
        builder.Property(c => c.NomeEmpresa).HasColumnName("NOME_EMPRESA");
        builder.Property(c => c.Uf).HasColumnName("UF");
        builder.Property(c => c.Municipio).HasColumnName("MUNICIPIO");
        builder.Property(c => c.HorarioEntrada).HasColumnName("HORARIO_ENTRADA");
        builder.Property(c => c.HorarioSaida).HasColumnName("HORARIO_SAIDA");

        // SMALLINT e TINYINT lidos como int; os padrões do banco (tolerância 0) não são declarados: o EF sempre envia o valor que a entidade tem.
        builder.Property(c => c.ToleranciaAtrasoMinutos).HasColumnName("TOLERANCIA_ATRASO_MINUTOS").HasColumnType("smallint");
        builder.Property(c => c.DiaPagamento).HasColumnName("DIA_PAGAMENTO").HasColumnType("tinyint");
    }
}
