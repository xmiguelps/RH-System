using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Tests.Colaboradores;

/// <summary>
/// A porta dos pedidos parados no gestor: o composto padrão junta as fatias registradas (3: ponto; 7: férias) e, sem nenhuma, não faz nada. As fatias aqui são
/// falsas, em memória, e anotam em uma lista comum quem foi chamado, com que método e com qual gestor.
/// </summary>
public class PedidosParadosNoGestorCompostoTests
{
    private sealed class FatiaFalsa(string nome, List<string> chamadas, PedidosAprovados aprova, Exception? lanca = null) : IPedidosParadosNoGestorDaFatia
    {
        public Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)
        {
            chamadas.Add($"{nome}.Aprovar:{gestorId}");
            return lanca is null ? Task.FromResult(aprova) : throw lanca;
        }

        public Task PassarParaORhAsync(int gestorId, CancellationToken ct)
        {
            chamadas.Add($"{nome}.Passar:{gestorId}");
            return lanca is null ? Task.CompletedTask : throw lanca;
        }
    }

    [Fact]
    public async Task Sem_nenhuma_fatia_registrada_as_duas_acoes_terminam_sem_fazer_nada_e_sem_lancar()
    {
        var composto = new PedidosParadosNoGestorComposto([]);

        await composto.PassarParaORhAsync(7, CancellationToken.None);
        var aprovados = await composto.AprovarEtapaDoGestorAsync(7, CancellationToken.None);

        Assert.Equal(PedidosAprovados.Zero, aprovados);
    }

    [Fact]
    public async Task Cada_acao_chama_todas_as_fatias_registradas_com_o_mesmo_gestorId_na_ordem_do_registro()
    {
        var chamadas = new List<string>();
        var composto = new PedidosParadosNoGestorComposto(
        [
            new FatiaFalsa("ponto", chamadas, PedidosAprovados.Zero),
            new FatiaFalsa("ferias", chamadas, PedidosAprovados.Zero),
            new FatiaFalsa("outra", chamadas, PedidosAprovados.Zero),
        ]);

        await composto.AprovarEtapaDoGestorAsync(42, CancellationToken.None);
        await composto.PassarParaORhAsync(42, CancellationToken.None);

        Assert.Equal(
            ["ponto.Aprovar:42", "ferias.Aprovar:42", "outra.Aprovar:42", "ponto.Passar:42", "ferias.Passar:42", "outra.Passar:42"],
            chamadas);
    }

    [Fact]
    public async Task Aprovar_devolve_a_soma_dos_valores_das_fatias_registradas_e_Zero_sem_nenhuma()
    {
        var chamadas = new List<string>();
        var composto = new PedidosParadosNoGestorComposto([new FatiaFalsa("ponto", chamadas, new PedidosAprovados(1, 0)), new FatiaFalsa("ferias", chamadas, new PedidosAprovados(0, 4))]);

        var aprovados = await composto.AprovarEtapaDoGestorAsync(9, CancellationToken.None);

        Assert.Equal(new PedidosAprovados(1, 4), aprovados);
        Assert.Equal(new PedidosAprovados(0, 0), await new PedidosParadosNoGestorComposto([]).AprovarEtapaDoGestorAsync(9, CancellationToken.None));
    }

    [Fact]
    public void PedidosAprovados_Somar_soma_campo_a_campo_e_Zero_e_0_e_0()
    {
        Assert.Equal(new PedidosAprovados(0, 0), PedidosAprovados.Zero);
        Assert.Equal(new PedidosAprovados(11, 22), new PedidosAprovados(1, 2).Somar(new PedidosAprovados(10, 20)));
        Assert.Equal(new PedidosAprovados(5, 6), PedidosAprovados.Zero.Somar(new PedidosAprovados(5, 6))); // Zero é o neutro da soma
        Assert.Equal(new PedidosAprovados(3, 0), new PedidosAprovados(3, 0).Somar(PedidosAprovados.Zero));
    }

    [Fact]
    public async Task Excecao_de_uma_fatia_sobe_para_quem_chamou_para_a_transacao_dele_desfazer()
    {
        // As ações são idempotentes: tentar de novo é seguro. O composto NÃO engole nem embrulha a exceção: o chamador (que está numa transação) a vê como veio.
        var chamadas = new List<string>();
        var erro = new InvalidOperationException("a fatia de ferias falhou");
        var composto = new PedidosParadosNoGestorComposto([new FatiaFalsa("ponto", chamadas, new PedidosAprovados(2, 0)), new FatiaFalsa("ferias", chamadas, PedidosAprovados.Zero, erro)]);

        var daAprovacao = await Assert.ThrowsAsync<InvalidOperationException>(() => composto.AprovarEtapaDoGestorAsync(5, CancellationToken.None));
        var daPassagem = await Assert.ThrowsAsync<InvalidOperationException>(() => composto.PassarParaORhAsync(5, CancellationToken.None));

        Assert.Same(erro, daAprovacao);
        Assert.Same(erro, daPassagem);
    }
}
