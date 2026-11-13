using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Application.Formularios.Destinatarios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// As contas da lista de avaliados, sem banco: entram os avaliados ainda esperados e os que já têm resposta; <c>Esperadas</c> conta os pares (0 para quem saiu); com nome o resultado libera com uma resposta; no anônimo precisa de 3 participações E de uma pergunta que chegue ao mínimo.
/// </summary>
public class RegrasDosAvaliadosTests
{
    private static ParDeResposta P(int pessoa, int avaliado) => new(pessoa, avaliado);

    private static readonly Func<int, bool> Sim = _ => true;

    private static readonly Func<int, bool> Nao = _ => false;

    [Fact]
    public void Entram_os_esperados_e_os_que_ja_tem_resposta_em_ordem_de_numero_e_as_esperadas_contam_os_pares()
    {
        var esperados = new[] { P(1, 10), P(2, 10), P(3, 20) };
        var totais = new Dictionary<int, int> { [30] = 4, [10] = 1 };

        var apurados = RegrasDosAvaliados.Apurar(false, esperados, totais, Sim);

        Assert.Equal([10, 20, 30], apurados.Select(a => a.AvaliadoId));
        Assert.Equal([2, 1, 0], apurados.Select(a => a.Esperadas)); // 30 saiu do público: ninguém mais deve avaliá-lo
        Assert.Equal([1, 0, 4], apurados.Select(a => a.Total));
    }

    [Fact]
    public void Par_sem_avaliado_nao_cria_avaliado()
    {
        var apurados = RegrasDosAvaliados.Apurar(false, [new ParDeResposta(1, null)], new Dictionary<int, int>(), Sim);

        Assert.Empty(apurados);
    }

    [Fact]
    public void Com_nome_o_resultado_libera_com_uma_resposta_e_nao_olha_as_perguntas()
    {
        var apurados = RegrasDosAvaliados.Apurar(false, [P(1, 10), P(2, 20)], new Dictionary<int, int> { [10] = 1 }, Nao);

        Assert.Equal([true, false], apurados.Select(a => a.ResultadoLiberado));
    }

    [Fact]
    public void No_anonimo_precisa_de_3_participacoes_e_de_uma_pergunta_que_chegue_ao_minimo()
    {
        var esperados = new[] { P(1, 10), P(2, 20), P(3, 30) };
        var totais = new Dictionary<int, int> { [10] = 2, [20] = 3, [30] = 3 };

        var apurados = RegrasDosAvaliados.Apurar(true, esperados, totais, avaliado => avaliado != 30);

        Assert.Equal([false, true, false], apurados.Select(a => a.ResultadoLiberado)); // 10: só 2; 20: 3 e pergunta; 30: 3 mas nenhuma pergunta chega a 3
    }

    [Fact]
    public void No_anonimo_nem_pergunta_a_funcao_quando_nao_chegou_a_3_participacoes()
    {
        var chamadas = new List<int>();

        RegrasDosAvaliados.Apurar(true, [P(1, 10), P(2, 20)], new Dictionary<int, int> { [10] = 2, [20] = 3 }, avaliado =>
        {
            chamadas.Add(avaliado);
            return true;
        });

        Assert.Equal([20], chamadas);
    }
}
