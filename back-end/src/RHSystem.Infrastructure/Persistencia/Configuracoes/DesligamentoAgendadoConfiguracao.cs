using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class DesligamentoAgendadoConfiguracao : IEntityTypeConfiguration<DesligamentoAgendado>
{
    public void Configure(EntityTypeBuilder<DesligamentoAgendado> builder)
    {
        builder.ToTable("DESLIGAMENTOS_AGENDADOS");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID_DESLIGAMENTO_AGENDADO");
        builder.Property(d => d.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(d => d.UltimoDiaTrabalho).HasColumnName("ULTIMO_DIA_TRABALHO");
        builder.Property(d => d.Situacao).HasColumnName("SITUACAO").HasConversion(new EnumComoTextoMaiusculo<SituacaoDoDesligamento>());
        builder.Property(d => d.CriadoEm).HasColumnName("CRIADO_EM").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(d => d.EfetivadoEm).HasColumnName("EFETIVADO_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(d => d.CanceladoEm).HasColumnName("CANCELADO_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(d => d.RevertidoEm).HasColumnName("REVERTIDO_EM").HasConversion(ConversoresDeTempo.Utc);

        // 1 enquanto o desligamento está AGENDADO ou EFETIVADO, NULL nos outros: é o que deixa só um desligamento vigente por pessoa.
        builder.Property<sbyte?>("Vigente").HasColumnName("VIGENTE").CalculadaNoBanco();

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(d => d.FuncionarioId);
    }
}
