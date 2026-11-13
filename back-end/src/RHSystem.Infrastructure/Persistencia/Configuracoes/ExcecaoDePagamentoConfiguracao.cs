using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Pagamento;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// <c>PAGAMENTOS_EXCECOES</c>: <c>ANO</c> é do tipo <c>YEAR</c> e <c>MES</c> é <c>TINYINT</c>; as duas viram <c>int</c> na entidade. A chave única <c>(ANO, MES)</c> e os <c>CHECK</c> (mês de 1 a 12 e data dentro do mês) ficam no banco.
/// </summary>
public class ExcecaoDePagamentoConfiguracao : IEntityTypeConfiguration<ExcecaoDePagamento>
{
    public void Configure(EntityTypeBuilder<ExcecaoDePagamento> builder)
    {
        builder.ToTable("PAGAMENTOS_EXCECOES");
        builder.Ignore(e => e.NomeParaAuditoria);
        builder.Ignore(e => e.RegistroAfetado);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID_PAGAMENTO_EXCECAO");
        builder.Property(e => e.Ano).HasColumnName("ANO").HasColumnType("year");
        builder.Property(e => e.Mes).HasColumnName("MES");
        builder.Property(e => e.DataDoPagamento).HasColumnName("DATA_PAGAMENTO");
    }
}
