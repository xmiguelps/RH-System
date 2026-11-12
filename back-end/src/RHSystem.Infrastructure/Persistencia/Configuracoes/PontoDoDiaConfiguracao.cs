using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class PontoDoDiaConfiguracao : IEntityTypeConfiguration<PontoDoDia>
{
    public void Configure(EntityTypeBuilder<PontoDoDia> builder)
    {
        builder.ToTable("PONTOS");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID_PONTO");
        builder.Property(p => p.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(p => p.Dia).HasColumnName("DATA");

        // A carga do dia é o cargo ÷ 5 e o TRIGGER a preenche quando o INSERT leva NULL (o EF envia NULL). HORAS_TRABALHADAS não é mais preenchida por ninguém.
        builder.Property(p => p.HorasTrabalhadas).HasColumnName("HORAS_TRABALHADAS").HasColumnType("decimal(5,2)");
        builder.Property(p => p.CargaHoraria).HasColumnName("CARGA_HORARIA").HasColumnType("decimal(5,2)");

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(p => p.FuncionarioId);
    }
}
