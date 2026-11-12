using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// Os pares (quem responde, quem é avaliado) e os números que saem deles, sem banco: o gestor avalia cada pessoa da equipe dele (menos ele), a equipe avalia o gestor do próprio setor (quem é de setor sem gestor fica fora, e o gestor não se avalia), sem par repetido
/// e em ordem estável; "esperados" conta avaliações nas avaliações e pessoas nos outros formulários; "quem falta" tira os pares que já responderam.
/// </summary>
public class QuemDeveResponderTests
{
    private static ParDeResposta P(int pessoa, int? avaliado = null) => new(pessoa, avaliado);

    // ---------------------------------------------------------------- GESTOR_AVALIA_EQUIPE

    [Fact]
    public void O_gestor_avalia_cada_pessoa_da_equipe_e_nunca_a_si_mesmo()
    {
        var pares = ParesDeAvaliacao.GestorAvaliaEquipe([(10, [1, 2, 10]), (20, [3])]); // o 10 aparece na própria equipe por engano: sai

        Assert.Equal([P(10, 1), P(10, 2), P(20, 3)], pares);
    }

    [Fact]
    public void Gestor_sem_equipe_nao_gera_par_e_a_mesma_pessoa_em_duas_equipes_gera_dois_pares_sem_repetir_o_mesmo()
    {
        var pares = ParesDeAvaliacao.GestorAvaliaEquipe([(10, []), (20, [5, 5, 6]), (30, [5])]);

        Assert.Equal([P(20, 5), P(20, 6), P(30, 5)], pares);
    }

    [Fact]
    public void O_mesmo_gestor_escolhido_duas_vezes_conta_uma_vez()
    {
        var pares = ParesDeAvaliacao.GestorAvaliaEquipe([(10, [1, 2]), (10, [1, 2])]);

        Assert.Equal([P(10, 1), P(10, 2)], pares);
    }

    // ---------------------------------------------------------------- EQUIPE_AVALIA_GESTOR

    [Fact]
    public void Cada_pessoa_avalia_o_gestor_do_proprio_setor_e_quem_e_de_setor_sem_gestor_fica_fora()
    {
        var pares = ParesDeAvaliacao.EquipeAvaliaGestor([(1, 10), (2, 10), (3, null), (4, 20)]);

        Assert.Equal([P(1, 10), P(2, 10), P(4, 20)], pares);
    }

    [Fact]
    public void O_gestor_nao_avalia_a_si_mesmo_mesmo_que_o_escolham_junto_com_a_equipe()
    {
        var pares = ParesDeAvaliacao.EquipeAvaliaGestor([(10, 10), (1, 10)]);

        Assert.Equal([P(1, 10)], pares);
    }

    [Fact]
    public void A_mesma_pessoa_escolhida_duas_vezes_gera_um_par_so_e_a_ordem_e_por_pessoa()
    {
        var pares = ParesDeAvaliacao.EquipeAvaliaGestor([(7, 10), (3, 10), (7, 10)]);

        Assert.Equal([P(3, 10), P(7, 10)], pares);
    }

    // ---------------------------------------------------------------- os números

    private static QuemDeveResponder Quem(AlvoDoFormulario? alvo, SentidoDaAvaliacao? sentido, bool avaliacao, ParDeResposta[] pares, int pessoasEscolhidas = 0, int setores = 0, bool todos = false, int gestores = 0) =>
        new(alvo, sentido, avaliacao, pares, pessoasEscolhidas, setores, todos, gestores);

    [Fact]
    public void Esperados_conta_pessoas_nos_formularios_comuns_e_avaliacoes_nas_avaliacoes()
    {
        var comum = Quem(AlvoDoFormulario.Pessoas, null, false, [P(1), P(2), P(3)], pessoasEscolhidas: 3);
        var avaliacao = Quem(AlvoDoFormulario.Pessoas, SentidoDaAvaliacao.GestorAvaliaEquipe, true, [P(10, 1), P(10, 2), P(20, 3)], gestores: 2);

        Assert.Equal((3, 3), (comum.Esperados, comum.Pessoas));
        Assert.Equal((3, 2), (avaliacao.Esperados, avaliacao.PessoasQueRespondem));
    }

    [Fact]
    public void Por_link_e_formulario_ainda_sem_publico_nao_tem_esperados()
    {
        Assert.Equal(0, Quem(AlvoDoFormulario.Link, null, false, []).Esperados);
        Assert.Equal(0, Quem(null, null, false, []).Esperados);
    }

    [Fact]
    public void O_resumo_de_quem_responde_sai_dos_numeros_certos_de_cada_caso()
    {
        Assert.Equal("3 pessoas escolhidas", QuemRespondeResumo.Texto(Quem(AlvoDoFormulario.Pessoas, null, false, [P(1), P(2), P(3)], pessoasEscolhidas: 3).Resumo));
        Assert.Equal("2 setores (3 pessoas)", QuemRespondeResumo.Texto(Quem(AlvoDoFormulario.Setores, null, false, [P(1), P(2), P(3)], setores: 2).Resumo));
        Assert.Equal("Todos os setores (3 pessoas)", QuemRespondeResumo.Texto(Quem(AlvoDoFormulario.Setores, null, false, [P(1), P(2), P(3)], setores: 5, todos: true).Resumo));
        Assert.Equal("2 gestores (3 avaliações)", QuemRespondeResumo.Texto(Quem(AlvoDoFormulario.Pessoas, SentidoDaAvaliacao.GestorAvaliaEquipe, true, [P(10, 1), P(10, 2), P(20, 3)], gestores: 2).Resumo));
        Assert.Equal("2 setores (2 avaliações)", QuemRespondeResumo.Texto(Quem(AlvoDoFormulario.Setores, SentidoDaAvaliacao.EquipeAvaliaGestor, true, [P(1, 10), P(2, 10)], setores: 2).Resumo));
        Assert.Equal("Candidatos, por link", QuemRespondeResumo.Texto(Quem(AlvoDoFormulario.Link, null, false, []).Resumo));
        Assert.Equal("Ainda não definido", QuemRespondeResumo.Texto(Quem(null, null, false, []).Resumo));
    }

    [Fact]
    public void Quem_falta_tira_os_pares_que_ja_responderam_e_mantem_a_ordem()
    {
        var quem = Quem(AlvoDoFormulario.Pessoas, SentidoDaAvaliacao.GestorAvaliaEquipe, true, [P(10, 1), P(10, 2), P(20, 3)]);

        var faltam = quem.QuemFalta([P(10, 2), P(99, 99)]);

        Assert.Equal([P(10, 1), P(20, 3)], faltam);
        Assert.Equal(quem.Pares, quem.QuemFalta([]));
        Assert.Empty(quem.QuemFalta(quem.Pares));
    }

    [Fact]
    public void Quem_falta_em_formulario_comum_olha_so_a_pessoa()
    {
        var quem = Quem(AlvoDoFormulario.Pessoas, null, false, [P(1), P(2)]);

        Assert.Equal([P(2)], quem.QuemFalta([P(1)]));
    }
}
