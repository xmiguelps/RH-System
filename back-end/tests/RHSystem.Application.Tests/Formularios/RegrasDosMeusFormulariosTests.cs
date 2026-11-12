using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// O cartão de "meus formulários" sem banco: admissão e avaliação são "para responder", pesquisa é "disponível", a Sugestão fica sempre disponível (0 de 1, sem data); o já respondido continua na lista mesmo encerrado ou sem a pessoa no público; o anônimo nunca tem data nem "ver minhas respostas";
/// na avaliação da equipe o total é o número de pessoas a avaliar. As abas ordenam por situação, prazo (sem prazo por último), título e id; as contagens não olham a situação escolhida.
/// </summary>
public class RegrasDosMeusFormulariosTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);
    private static readonly DateTime Publicado = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Respondeu = new(2026, 10, 5, 15, 30, 0, DateTimeKind.Utc);

    private static ParDeResposta P(int pessoa, int? avaliado = null) => new(pessoa, avaliado);

    private static CartaoDoFormulario Cartao(
        TipoDeFormulario tipo = TipoDeFormulario.Pesquisa,
        bool deve = true,
        bool fez = false,
        bool anonimo = false,
        bool ativo = true,
        DateTime? publicado = null,
        bool publicadoNulo = false,
        DateTime? encerrado = null,
        DateOnly? prazo = null,
        SentidoDaAvaliacao? sentido = null,
        IReadOnlyList<ParDeResposta>? meusPares = null,
        IReadOnlyList<ParDeResposta>? feitos = null,
        IReadOnlyList<TipoDePergunta>? perguntas = null,
        int id = 1,
        string titulo = "Clima") =>
        new(id, titulo, null, tipo, sentido, anonimo, ativo, publicadoNulo ? null : publicado ?? Publicado, encerrado, prazo, perguntas ?? [TipoDePergunta.RespostaCurta, TipoDePergunta.MultiplaEscolha],
            meusPares ?? (deve ? [P(7)] : []), feitos ?? (fez ? [P(7)] : []), fez && !anonimo ? Respondeu : null);

    // ---------------------------------------------------------------- quem aparece e em qual aba

    [Fact]
    public void Pesquisa_publicada_que_a_pessoa_deve_responder_fica_disponivel_zero_de_um_sem_ver_respostas()
    {
        var item = RegrasDosMeusFormularios.Montar(Cartao(prazo: new DateOnly(2026, 10, 20)), Agora)!;

        Assert.Equal((SituacaoMinhaDoFormulario.Disponivel, 0, 1, false, null), (item.Situacao, item.Respondidos, item.Total, item.PodeVerMinhasRespostas, item.RespondidoEmUtc));
        Assert.Equal((2, new DateOnly(2026, 10, 20)), (item.TotalPerguntas, item.Prazo));
    }

    [Theory]
    [InlineData(TipoDeFormulario.Admissao)]
    [InlineData(TipoDeFormulario.Avaliacao)]
    public void Admissao_e_avaliacao_publicadas_sao_para_responder(TipoDeFormulario tipo)
    {
        var item = RegrasDosMeusFormularios.Montar(Cartao(tipo, sentido: tipo == TipoDeFormulario.Avaliacao ? SentidoDaAvaliacao.EquipeAvaliaGestor : null), Agora)!;

        Assert.Equal(SituacaoMinhaDoFormulario.ParaResponder, item.Situacao);
    }

    [Fact]
    public void Formulario_respondido_vira_respondido_com_a_data_e_deixa_ver_as_respostas()
    {
        var item = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Admissao, fez: true), Agora)!;

        Assert.Equal((SituacaoMinhaDoFormulario.Respondido, 1, 1, Respondeu, true), (item.Situacao, item.Respondidos, item.Total, item.RespondidoEmUtc, item.PodeVerMinhasRespostas));
    }

    [Fact]
    public void Anonimo_respondido_nao_tem_data_nem_ver_minhas_respostas()
    {
        var item = RegrasDosMeusFormularios.Montar(Cartao(anonimo: true, fez: true), Agora)!;

        Assert.Equal((SituacaoMinhaDoFormulario.Respondido, null, false), (item.Situacao, item.RespondidoEmUtc, item.PodeVerMinhasRespostas));
    }

    [Fact]
    public void O_respondido_continua_na_lista_depois_de_encerrado_ou_de_sair_do_publico_mas_o_nao_respondido_some()
    {
        var encerrado = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc);

        Assert.Equal(SituacaoMinhaDoFormulario.Respondido, RegrasDosMeusFormularios.Montar(Cartao(fez: true, encerrado: encerrado), Agora)!.Situacao);
        Assert.Equal(SituacaoMinhaDoFormulario.Respondido, RegrasDosMeusFormularios.Montar(Cartao(fez: true, deve: false), Agora)!.Situacao);
        Assert.Null(RegrasDosMeusFormularios.Montar(Cartao(encerrado: encerrado), Agora));
        Assert.Null(RegrasDosMeusFormularios.Montar(Cartao(deve: false), Agora));
    }

    [Fact]
    public void Rascunho_agendado_e_prazo_vencido_nao_aparecem_para_quem_nao_respondeu()
    {
        Assert.Null(RegrasDosMeusFormularios.Montar(Cartao(ativo: false, publicadoNulo: true), Agora)); // rascunho
        Assert.Null(RegrasDosMeusFormularios.Montar(Cartao(ativo: false, publicado: Agora.AddHours(5)), Agora)); // agendado
        Assert.Null(RegrasDosMeusFormularios.Montar(Cartao(prazo: new DateOnly(2026, 10, 6)), Agora)); // prazo de ontem: encerrado
        Assert.NotNull(RegrasDosMeusFormularios.Montar(Cartao(prazo: new DateOnly(2026, 10, 7)), Agora)); // o prazo vale até o fim do dia
    }

    // ---------------------------------------------------------------- Sugestão

    [Fact]
    public void A_Sugestao_fica_sempre_disponivel_zero_de_um_sem_data_prazo_nem_ver_respostas_mesmo_depois_de_enviar()
    {
        var nova = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Outro, prazo: new DateOnly(2026, 10, 20)), Agora)!;
        var enviada = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Outro, fez: true, feitos: [P(7), P(7)]), Agora)!;

        Assert.Equal((SituacaoMinhaDoFormulario.Disponivel, 0, 1, null, null, false), (nova.Situacao, nova.Respondidos, nova.Total, nova.RespondidoEmUtc, nova.Prazo, nova.PodeVerMinhasRespostas));
        Assert.Equal((SituacaoMinhaDoFormulario.Disponivel, 0, 1, null, false), (enviada.Situacao, enviada.Respondidos, enviada.Total, enviada.RespondidoEmUtc, enviada.PodeVerMinhasRespostas));
    }

    [Fact]
    public void A_Sugestao_encerrada_ou_fora_do_publico_some_mesmo_para_quem_ja_enviou()
    {
        Assert.Null(RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Outro, fez: true, encerrado: new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc)), Agora));
        Assert.Null(RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Outro, fez: true, deve: false), Agora));
    }

    // ---------------------------------------------------------------- avaliação da equipe

    [Fact]
    public void Na_avaliacao_da_equipe_o_total_e_o_numero_de_pessoas_a_avaliar_e_continua_para_responder_ate_avaliar_todas()
    {
        var parcial = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Avaliacao, sentido: SentidoDaAvaliacao.GestorAvaliaEquipe, meusPares: [P(7, 1), P(7, 2)], feitos: [P(7, 3)]), Agora)!;
        var completa = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Avaliacao, sentido: SentidoDaAvaliacao.GestorAvaliaEquipe, meusPares: [], feitos: [P(7, 1), P(7, 2)]), Agora)!;

        Assert.Equal((SituacaoMinhaDoFormulario.ParaResponder, 1, 3, true), (parcial.Situacao, parcial.Respondidos, parcial.Total, parcial.PodeVerMinhasRespostas)); // 2 a fazer + 1 feito que saiu da equipe
        Assert.Equal((SituacaoMinhaDoFormulario.Respondido, 2, 2), (completa.Situacao, completa.Respondidos, completa.Total));
    }

    [Fact]
    public void Na_avaliacao_do_gestor_o_total_e_um_e_ao_responder_vira_respondido()
    {
        var antes = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Avaliacao, sentido: SentidoDaAvaliacao.EquipeAvaliaGestor, meusPares: [P(7, 99)]), Agora)!;
        var depois = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Avaliacao, sentido: SentidoDaAvaliacao.EquipeAvaliaGestor, meusPares: [P(7, 99)], feitos: [P(7, 99)]), Agora)!;

        Assert.Equal((SituacaoMinhaDoFormulario.ParaResponder, 0, 1), (antes.Situacao, antes.Respondidos, antes.Total));
        Assert.Equal((SituacaoMinhaDoFormulario.Respondido, 1, 1), (depois.Situacao, depois.Respondidos, depois.Total));
    }

    [Fact]
    public void Os_minutos_estimados_somam_o_peso_das_perguntas_e_na_avaliacao_da_equipe_multiplicam_pelas_pessoas()
    {
        var pesquisa = RegrasDosMeusFormularios.Montar(Cartao(perguntas: [TipoDePergunta.RespostaLonga, TipoDePergunta.RespostaLonga, TipoDePergunta.Arquivo]), Agora)!;
        var equipe = RegrasDosMeusFormularios.Montar(Cartao(TipoDeFormulario.Avaliacao, sentido: SentidoDaAvaliacao.GestorAvaliaEquipe, meusPares: [P(7, 1), P(7, 2), P(7, 3)], perguntas: [TipoDePergunta.RespostaCurta]), Agora)!;

        Assert.Equal(4, pesquisa.MinutosEstimados); // 1,5 + 1,5 + 1 = 4
        Assert.Equal(3, equipe.MinutosEstimados); // 1 pergunta curta x 3 pessoas
    }

    // ---------------------------------------------------------------- ordem e contagens

    private static FormularioMeuItem Item(int id, string titulo, SituacaoMinhaDoFormulario situacao, DateOnly? prazo = null) =>
        new(id, titulo, null, TipoDeFormulario.Pesquisa, null, false, situacao, 1, 1, prazo, 0, 1, null, false);

    [Fact]
    public void A_ordem_e_situacao_depois_prazo_mais_proximo_sem_prazo_por_ultimo_depois_titulo_e_id()
    {
        var itens = new[]
        {
            Item(1, "B", SituacaoMinhaDoFormulario.Respondido),
            Item(2, "Z sem prazo", SituacaoMinhaDoFormulario.Disponivel),
            Item(3, "Y longe", SituacaoMinhaDoFormulario.Disponivel, new DateOnly(2026, 12, 1)),
            Item(4, "X perto", SituacaoMinhaDoFormulario.Disponivel, new DateOnly(2026, 10, 10)),
            Item(5, "a", SituacaoMinhaDoFormulario.ParaResponder),
            Item(7, "mesmo", SituacaoMinhaDoFormulario.ParaResponder, new DateOnly(2026, 11, 1)),
            Item(6, "Mesmo", SituacaoMinhaDoFormulario.ParaResponder, new DateOnly(2026, 11, 1)),
        };

        var ordem = RegrasDosMeusFormularios.Ordenar(itens).Select(i => i.Id).ToList();

        Assert.Equal([6, 7, 5, 4, 3, 2, 1], ordem); // com prazo (6 e 7 empatam no título: id), sem prazo (5), depois as disponíveis, depois a respondida
    }

    [Fact]
    public void As_contagens_somam_cada_situacao_e_todos_e_para_responder_mais_disponiveis()
    {
        var itens = new[]
        {
            Item(1, "a", SituacaoMinhaDoFormulario.ParaResponder),
            Item(2, "b", SituacaoMinhaDoFormulario.ParaResponder),
            Item(3, "c", SituacaoMinhaDoFormulario.Disponivel),
            Item(4, "d", SituacaoMinhaDoFormulario.Respondido),
            Item(5, "e", SituacaoMinhaDoFormulario.Respondido),
            Item(6, "f", SituacaoMinhaDoFormulario.Respondido),
        };

        var c = RegrasDosMeusFormularios.Contar(itens);

        Assert.Equal((3, 2, 1, 3), (c.Todos, c.ParaResponder, c.Disponiveis, c.Respondidos));
        Assert.Equal((0, 0, 0, 0), (RegrasDosMeusFormularios.Contar([]).Todos, RegrasDosMeusFormularios.Contar([]).ParaResponder, RegrasDosMeusFormularios.Contar([]).Disponiveis, RegrasDosMeusFormularios.Contar([]).Respondidos));
    }
}
