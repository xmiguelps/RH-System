using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class BeneficioDoFuncionarioConfiguracao : IEntityTypeConfiguration<BeneficioDoFuncionario>
{
    public void Configure(EntityTypeBuilder<BeneficioDoFuncionario> builder)
    {
        builder.ToTable("BENEFICIOS_FUNCIONARIOS");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID_BENEFICIO_FUNCIONARIOS");
        builder.Property(b => b.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(b => b.BeneficioId).HasColumnName("ID_BENEFICIO");

        // BENEFICIO_ATIVO tem DEFAULT TRUE no banco e STATUS_PEDIDO não tem padrão (V001): o padrão NÃO é declarado aqui e o valor nunca é gerado pelo banco, para o
        // EF mandar sempre o que a entidade tem (sem isto, "false" seria trocado pelo padrão "true"). O CHECK do banco recusa ativo sem APROVADO.
        builder.Property(b => b.Ativo).HasColumnName("BENEFICIO_ATIVO").ValueGeneratedNever();
        builder.Property(b => b.Status).HasColumnName("STATUS_PEDIDO").ValueGeneratedNever().HasConversion(new EnumComoTextoMaiusculo<StatusDoPedidoDeBeneficio>());

        // As colunas do pedido (DATA_PEDIDO, DATA_INICIO_DESEJADA, AUTORIZA_DESCONTO_FOLHA, ID_FUNCIONARIO_RH, DATA_RESPOSTA, MOTIVO_RECUSA) ficam fora do modelo: aceitam
        // NULL ou têm padrão. A fatia 7 as acrescenta a esta entidade.
        builder.HasOne(b => b.Beneficio).WithMany().HasForeignKey(b => b.BeneficioId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(b => b.FuncionarioId);
    }
}
