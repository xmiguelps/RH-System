using RHSystem.Domain.Beneficios;

namespace RHSystem.Domain.Tests.Colaboradores;

public class BeneficioDoFuncionarioTests
{
    private static BeneficioDoFuncionario ComStatus(StatusDoPedidoDeBeneficio status)
    {
        // Linhas que já existem no banco em outro estado (pedido do colaborador, recusa, cancelamento): o EF as materializa assim.
        var linha = BeneficioDoFuncionario.Conceder(1, 2);
        typeof(BeneficioDoFuncionario).GetProperty(nameof(BeneficioDoFuncionario.Status))!.SetValue(linha, status);
        typeof(BeneficioDoFuncionario).GetProperty(nameof(BeneficioDoFuncionario.Ativo))!.SetValue(linha, false);
        return linha;
    }

    [Fact]
    public void BeneficioDoFuncionario_Conceder_nasce_ativo_e_aprovado_e_Reativar_de_recusado_lanca()
    {
        var concedido = BeneficioDoFuncionario.Conceder(7, 3);

        Assert.True(concedido.Ativo);
        Assert.Equal(StatusDoPedidoDeBeneficio.Aprovado, concedido.Status);
        Assert.Equal((7, 3), (concedido.FuncionarioId, concedido.BeneficioId));

        // O CHECK do banco só aceita ativo com APROVADO: reativar uma linha recusada não pode nem tentar.
        Assert.Throws<InvalidOperationException>(() => ComStatus(StatusDoPedidoDeBeneficio.Recusado).Reativar());
        Assert.Throws<InvalidOperationException>(() => ComStatus(StatusDoPedidoDeBeneficio.Pendente).Reativar());
        Assert.Throws<InvalidOperationException>(() => ComStatus(StatusDoPedidoDeBeneficio.Cancelado).Reativar());
    }

    [Fact]
    public void BeneficioDoFuncionario_Desativar_e_Reativar_valem_para_aprovado()
    {
        var linha = BeneficioDoFuncionario.Conceder(1, 2);

        linha.Desativar();
        Assert.False(linha.Ativo);
        Assert.Equal(StatusDoPedidoDeBeneficio.Aprovado, linha.Status);

        linha.Reativar();
        Assert.True(linha.Ativo);
    }

    [Theory]
    [InlineData(StatusDoPedidoDeBeneficio.Pendente)]
    [InlineData(StatusDoPedidoDeBeneficio.Recusado)]
    [InlineData(StatusDoPedidoDeBeneficio.Cancelado)]
    [InlineData(StatusDoPedidoDeBeneficio.Aprovado)]
    public void BeneficioDoFuncionario_Reconceder_leva_PENDENTE_RECUSADO_e_CANCELADO_a_ativo_e_aprovado(StatusDoPedidoDeBeneficio origem)
    {
        var linha = ComStatus(origem);

        linha.Reconceder();

        Assert.True(linha.Ativo);
        Assert.Equal(StatusDoPedidoDeBeneficio.Aprovado, linha.Status);
    }
}
