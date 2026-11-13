using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// As contas da tela "Quem respondeu", sem banco. Com nome: uma linha por par esperado mais quem respondeu e saiu do público; <c>respondidos</c> e <c>faltam</c> só dos esperados (a taxa nunca passa de 100, o arredondamento é para cima na metade). Sugestão: uma linha por envio e só os envios contados.
/// Anônimo: sem número antes de encerrar (só os esperados) e exato depois. Sem público: sem esperados.
/// </summary>
public class RegrasDosRespondentesTests
{
    private static readonly DateTime T1 = new(2026, 10, 5, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 10, 6, 14, 20, 41, DateTimeKind.Utc);

    private static ParDeResposta P(int pessoa, int? avaliado = null) => new(pessoa, avaliado);

    private static QuemDeveResponder Quem(params ParDeResposta[] pares) => new(AlvoDoFormulario.Pessoas, null, false, pares, pares.Length, 0, false, 0);

    private static QuemDeveResponder QuemAvaliacao(params ParDeResposta[] pares) => new(AlvoDoFormulario.Pessoas, SentidoDaAvaliacao.GestorAvaliaEquipe, true, pares, 1, 0, false, 1);

    private static QuemDeveResponder SemPublico() => new(null, null, false, [], 0, 0, false, 0);

    private static EnvioLido E(int pessoa, DateTime instante, int? avaliado = null) => new(pessoa, avaliado, instante);

    // ---------------------------------------------------------------- linhas

    [Fact]
    public void Uma_linha_por_par_esperado_com_o_instante_de_quem_respondeu()
    {
        var linhas = RegrasDosRespondentes.Linhas(false, Quem(P(1), P(2), P(3)), [E(2, T1)]);

        Assert.Equal([new LinhaDeRespondente(1, null, null), new LinhaDeRespondente(2, null, T1), new LinhaDeRespondente(3, null, null)], linhas);
    }

    [Fact]
    public void Quem_respondeu_e_nao_e_mais_esperado_continua_como_respondeu()
    {
        var linhas = RegrasDosRespondentes.Linhas(false, Quem(P(1)), [E(9, T2), E(1, T1)]);

        Assert.Equal([new LinhaDeRespondente(1, null, T1), new LinhaDeRespondente(9, null, T2)], linhas.OrderBy(l => l.PessoaId));
    }

    [Fact]
    public void Na_avaliacao_cada_par_e_uma_linha()
    {
        var linhas = RegrasDosRespondentes.Linhas(false, QuemAvaliacao(P(10, 1), P(10, 2)), [E(10, T1, 2)]);

        Assert.Equal([new LinhaDeRespondente(10, 1, null), new LinhaDeRespondente(10, 2, T1)], linhas);
    }

    [Fact]
    public void Na_Sugestao_cada_envio_e_uma_linha_mesmo_da_mesma_pessoa()
    {
        var linhas = RegrasDosRespondentes.Linhas(true, Quem(P(1), P(2)), [E(1, T1), E(1, T2), E(2, T1)]);

        Assert.Equal([new LinhaDeRespondente(1, null, T1), new LinhaDeRespondente(1, null, T2), new LinhaDeRespondente(2, null, T1)], linhas);
    }

    // ---------------------------------------------------------------- resumo

    [Theory]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 2, 50)]
    [InlineData(0, 3, 0)]
    [InlineData(3, 3, 100)]
    public void O_resumo_com_nome_conta_so_os_esperados_e_arredonda_a_taxa(int responderam, int esperados, int taxa)
    {
        var pares = Enumerable.Range(1, esperados).Select(i => P(i)).ToArray();
        var envios = Enumerable.Range(1, responderam).Select(i => E(i, T1)).ToList();

        var resumo = RegrasDosRespondentes.Resumo(false, false, false, Quem(pares), envios, 0);

        Assert.Equal(new FormularioParticipacaoResumo(responderam, esperados - responderam, esperados, taxa), resumo);
    }

    [Fact]
    public void Quem_respondeu_e_saiu_do_publico_nao_entra_nas_contas_nem_faz_a_taxa_passar_de_100()
    {
        var resumo = RegrasDosRespondentes.Resumo(false, false, false, Quem(P(1)), [E(1, T1), E(9, T1), E(8, T1)], 0);

        Assert.Equal(new FormularioParticipacaoResumo(1, 0, 1, 100), resumo);
    }

    [Fact]
    public void Na_avaliacao_o_esperado_e_o_numero_de_avaliacoes()
    {
        var resumo = RegrasDosRespondentes.Resumo(false, false, false, QuemAvaliacao(P(10, 1), P(10, 2), P(10, 3), P(10, 4)), [E(10, T1, 1)], 0);

        Assert.Equal(new FormularioParticipacaoResumo(1, 3, 4, 25), resumo);
    }

    [Fact]
    public void Na_Sugestao_so_os_envios_contam()
    {
        var resumo = RegrasDosRespondentes.Resumo(true, false, false, Quem(P(1), P(2)), [E(1, T1), E(1, T2), E(2, T1)], 0);

        Assert.Equal(new FormularioParticipacaoResumo(3, null, null, null), resumo);
    }

    [Fact]
    public void O_anonimo_aberto_so_mostra_os_esperados_e_encerrado_mostra_os_numeros_das_participacoes()
    {
        var quem = Quem(P(1), P(2), P(3), P(4));

        var aberto = RegrasDosRespondentes.Resumo(false, true, false, quem, [], 3);
        var encerrado = RegrasDosRespondentes.Resumo(false, true, true, quem, [], 3);

        Assert.Equal(new FormularioParticipacaoResumo(null, null, 4, null), aberto); // nem número, nem faixa, mesmo com participação
        Assert.Equal(new FormularioParticipacaoResumo(3, 1, 4, 75), encerrado);
    }

    [Fact]
    public void Sem_publico_nao_ha_esperados_nem_taxa()
    {
        var comNome = RegrasDosRespondentes.Resumo(false, false, false, SemPublico(), [], 0);
        var anonimoEncerrado = RegrasDosRespondentes.Resumo(false, true, true, SemPublico(), [], 2);
        var semNinguem = RegrasDosRespondentes.Resumo(false, false, false, Quem(), [], 0);

        Assert.Equal(new FormularioParticipacaoResumo(0, null, null, null), comNome);
        Assert.Equal(new FormularioParticipacaoResumo(2, null, null, null), anonimoEncerrado);
        Assert.Equal(new FormularioParticipacaoResumo(0, 0, 0, 0), semNinguem); // 0 esperados: taxa 0 e não divisão por zero
    }
}
