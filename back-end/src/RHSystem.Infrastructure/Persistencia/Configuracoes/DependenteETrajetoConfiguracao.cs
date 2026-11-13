using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// <c>DEPENDENTES</c> e <c>TRAJETOS_VALE_TRANSPORTE</c>: tabelas sem trigger. <c>STATUS_PEDIDO</c> tem <c>DEFAULT 'PENDENTE'</c> e <c>DATA_PEDIDO</c> <c>DEFAULT CURRENT_TIMESTAMP</c> no banco: o padrão do status não é declarado e a aplicação grava SEMPRE os dois valores (a data pelo relógio dela, em -03:00). Sem <c>[Auditado]</c>: os eventos de
/// benefício são escritos à mão e nunca levam dado de dependente nem de trajeto.
/// </summary>
public class DependenteConfiguracao : IEntityTypeConfiguration<Dependente>
{
    public void Configure(EntityTypeBuilder<Dependente> builder)
    {
        builder.ToTable("DEPENDENTES");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID_DEPENDENTE");
        builder.Property(d => d.AdesaoId).HasColumnName("ID_BENEFICIO_FUNCIONARIOS");
        builder.Property(d => d.Nome).HasColumnName("NOME");
        builder.Property(d => d.Parentesco).HasColumnName("PARENTESCO");
        builder.Property(d => d.DataDeNascimento).HasColumnName("DATA_NASCIMENTO");
        builder.Property(d => d.Status).HasColumnName("STATUS_PEDIDO").ValueGeneratedNever().HasConversion(new EnumComoTextoMaiusculo<StatusDoPedidoDeBeneficio>());
        builder.Property(d => d.PedidoEmUtc).HasColumnName("DATA_PEDIDO").HasConversion(ConversoresDeTempo.Brasil).ValueGeneratedOnAdd(); // DEFAULT CURRENT_TIMESTAMP: o EF o trata como gerado na inclusão (regra da conformidade) mas ENVIA o valor quando preenchido, e a aplicação o preenche SEMPRE com o relógio dela
        builder.Property(d => d.FuncionarioRhId).HasColumnName("ID_FUNCIONARIO_RH");
        builder.Property(d => d.RespondidoEmUtc).HasColumnName("DATA_RESPOSTA").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(d => d.MotivoDaRecusa).HasColumnName("MOTIVO_RECUSA");

        builder.HasOne<BeneficioDoFuncionario>().WithMany().HasForeignKey(d => d.AdesaoId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(d => d.FuncionarioRhId);
    }
}

public class TrajetoDeValeTransporteConfiguracao : IEntityTypeConfiguration<TrajetoDeValeTransporte>
{
    public void Configure(EntityTypeBuilder<TrajetoDeValeTransporte> builder)
    {
        builder.ToTable("TRAJETOS_VALE_TRANSPORTE");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID_TRAJETO");
        builder.Property(t => t.AdesaoId).HasColumnName("ID_BENEFICIO_FUNCIONARIOS");
        builder.Property(t => t.Origem).HasColumnName("ORIGEM");
        builder.Property(t => t.Destino).HasColumnName("DESTINO");
        builder.Property(t => t.LinhasEMeios).HasColumnName("LINHAS_MEIOS");
        builder.Property(t => t.Status).HasColumnName("STATUS_PEDIDO").ValueGeneratedNever().HasConversion(new EnumComoTextoMaiusculo<StatusDoPedidoDeBeneficio>());
        builder.Property(t => t.PedidoEmUtc).HasColumnName("DATA_PEDIDO").HasConversion(ConversoresDeTempo.Brasil).ValueGeneratedOnAdd(); // DEFAULT CURRENT_TIMESTAMP: o EF o trata como gerado na inclusão (regra da conformidade) mas ENVIA o valor quando preenchido, e a aplicação o preenche SEMPRE com o relógio dela
        builder.Property(t => t.FuncionarioRhId).HasColumnName("ID_FUNCIONARIO_RH");
        builder.Property(t => t.RespondidoEmUtc).HasColumnName("DATA_RESPOSTA").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(t => t.MotivoDaRecusa).HasColumnName("MOTIVO_RECUSA");

        builder.HasOne<BeneficioDoFuncionario>().WithMany().HasForeignKey(t => t.AdesaoId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(t => t.FuncionarioRhId);
    }
}
