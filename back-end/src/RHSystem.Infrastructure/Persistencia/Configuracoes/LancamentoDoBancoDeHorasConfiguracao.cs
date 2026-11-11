using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class LancamentoDoBancoDeHorasConfiguracao : IEntityTypeConfiguration<LancamentoDoBancoDeHoras>
{
    public void Configure(EntityTypeBuilder<LancamentoDoBancoDeHoras> builder)
    {
        builder.ToTable("BANCOS_HORAS");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID_BANCO_HORAS");
        builder.Property(l => l.FuncionarioId).HasColumnName("ID_FUNCIONARIO");

        // O trigger preenche DATA_LANCAMENTO com CURDATE() quando vem nula e recusa data futura: a aplicação grava a dela. DIA_REFERENCIA aceita NULL no banco, mas é SEMPRE preenchido (a chave única não protege NULL).
        builder.Property(l => l.DataDoLancamento).HasColumnName("DATA_LANCAMENTO");
        builder.Property(l => l.Tipo).HasColumnName("TIPO_LANCAMENTO").HasConversion(new EnumComoTextoMaiusculo<TipoDeLancamentoDoBanco>());
        builder.Property(l => l.Quantidade).HasColumnName("QUANTIDADE").HasColumnType("decimal(8,2)");
        builder.Property(l => l.Observacao).HasColumnName("OBSERVACAO");
        builder.Property(l => l.DiaDeReferencia).HasColumnName("DIA_REFERENCIA");

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(l => l.FuncionarioId);
    }
}
