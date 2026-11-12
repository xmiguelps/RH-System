using RHSystem.Application.Comum;
using RHSystem.Application.Formularios;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// A conferência dos campos do formulário, das perguntas e da publicação, <b>todos os erros juntos</b> (o 400 traz um por campo): título de 1 a 200 caracteres, descrição até 1000, a Sugestão e a avaliação da equipe nunca anônimas, o sentido só na avaliação, a pergunta de 1 a 500
/// com 2 a 10 alternativas na múltipla escolha, e o alvo coerente com as listas, o prazo e a recorrência.
/// </summary>
public class ValidacaoDoFormularioTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);

    private static (T? Valor, IReadOnlyDictionary<string, string[]> Erros) Ler<T>(Func<ColetorDeErros, T?> leitura)
        where T : class
    {
        var coletor = new ColetorDeErros();
        var valor = leitura(coletor);
        if (!coletor.TemErros)
        {
            return (valor, new Dictionary<string, string[]>());
        }

        Assert.Null(valor); // com erro, nunca devolve o valor
        return (valor, Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver).Erros);
    }

    private static FormularioDetalhesComando Detalhes(string titulo = "Pesquisa de clima", TipoDeFormulario tipo = TipoDeFormulario.Pesquisa, bool anonimo = false, string? descricao = null, SentidoDaAvaliacao? sentido = null) =>
        new(titulo, tipo, anonimo) { Descricao = descricao, SentidoAvaliacao = sentido };

    // ---------------------------------------------------------------- os detalhes

    [Fact]
    public void Detalhes_validos_devolvem_o_titulo_e_a_descricao_aparados_e_descricao_vazia_vira_nula()
    {
        var (a, erros) = Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes("  Clima  ", descricao: "  Conte como foi.  ")));
        var (b, _) = Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(descricao: "   ")));

        Assert.Empty(erros);
        Assert.Equal(new DetalhesValidados("Clima", "Conte como foi."), a);
        Assert.Null(b!.Descricao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Titulo_vazio_e_erro_de_campo(string titulo) => Assert.Equal(["Informe o título do formulário, com até 200 caracteres."], Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(titulo))).Erros["titulo"]);

    [Fact]
    public void O_titulo_vai_ate_200_e_a_descricao_ate_1000_caracteres_contados_depois_de_aparar()
    {
        Assert.Empty(Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(new string('t', 200), descricao: new string('d', 1000)))).Erros);
        Assert.Empty(Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes("  " + new string('t', 200) + "  "))).Erros);
        var (_, erros) = Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(new string('t', 201), descricao: new string('d', 1001))));
        Assert.Equal(["descricao", "titulo"], erros.Keys.Order().ToArray());
    }

    [Fact]
    public void A_sugestao_nunca_e_anonima_e_a_avaliacao_da_equipe_tambem_nao()
    {
        Assert.Equal(["A caixa de sugestões não pode ser anônima."], Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(tipo: TipoDeFormulario.Outro, anonimo: true))).Erros["anonimo"]);
        Assert.Equal(["A avaliação do gestor sobre a equipe não pode ser anônima: cada pessoa recebe uma só avaliação e o resultado nunca apareceria."], Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(tipo: TipoDeFormulario.Avaliacao, anonimo: true, sentido: SentidoDaAvaliacao.GestorAvaliaEquipe))).Erros["anonimo"]);
        Assert.Empty(Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(tipo: TipoDeFormulario.Avaliacao, anonimo: true, sentido: SentidoDaAvaliacao.EquipeAvaliaGestor))).Erros); // a avaliação DO gestor pode
        Assert.Empty(Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(tipo: TipoDeFormulario.Outro, anonimo: false))).Erros);
    }

    [Fact]
    public void O_sentido_e_obrigatorio_na_avaliacao_e_proibido_nos_outros_tipos()
    {
        Assert.Equal(["Informe quem avalia quem: GESTOR_AVALIA_EQUIPE ou EQUIPE_AVALIA_GESTOR."], Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(tipo: TipoDeFormulario.Avaliacao))).Erros["sentidoAvaliacao"]);
        Assert.Equal(["O sentido só vale para a avaliação."], Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(tipo: TipoDeFormulario.Pesquisa, sentido: SentidoDaAvaliacao.GestorAvaliaEquipe))).Erros["sentidoAvaliacao"]);
        Assert.Empty(Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes(tipo: TipoDeFormulario.Avaliacao, sentido: SentidoDaAvaliacao.GestorAvaliaEquipe))).Erros);
    }

    [Fact]
    public void Os_erros_dos_detalhes_vem_juntos()
    {
        var (_, erros) = Ler(e => ValidacaoDoFormulario.Validar(e, Detalhes("", tipo: TipoDeFormulario.Outro, anonimo: true, descricao: new string('d', 1001))));

        Assert.Equal(["anonimo", "descricao", "titulo"], erros.Keys.Order().ToArray());
    }

    // ---------------------------------------------------------------- as perguntas

    private static PerguntaComando Pergunta(string texto = "Como foi?", TipoDePergunta tipo = TipoDePergunta.RespostaCurta, bool obrigatoria = false, IReadOnlyList<PerguntaAlternativaComando>? alternativas = null, int? ordem = null) =>
        new(texto, tipo, obrigatoria) { Alternativas = alternativas ?? [], Ordem = ordem };

    private static IReadOnlyList<PerguntaAlternativaComando> Alternativas(int quantas) => [.. Enumerable.Range(1, quantas).Select(i => new PerguntaAlternativaComando($"Opção {i}"))];

    [Fact]
    public void Pergunta_valida_devolve_o_texto_aparado_e_as_alternativas_com_ou_sem_id()
    {
        var (valida, erros) = Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta("  Como foi?  ", TipoDePergunta.MultiplaEscolha, true, [new PerguntaAlternativaComando("  Ótimo  ") { Id = 7 }, new PerguntaAlternativaComando("Bom")], ordem: 3)));

        Assert.Empty(erros);
        Assert.Equal(("Como foi?", 3, true), (valida!.Texto, valida.Ordem, valida.Obrigatoria));
        Assert.Equal([(7, "Ótimo"), ((int?)null, "Bom")], valida.Alternativas.Select(a => (a.Id, a.Texto)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(12)]
    public void Multipla_escolha_fora_de_2_a_10_alternativas_e_erro_em_alternativas(int quantas) =>
        Assert.Equal(["A múltipla escolha precisa de 2 a 10 alternativas."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: TipoDePergunta.MultiplaEscolha, alternativas: Alternativas(quantas)))).Erros["alternativas"]);

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void Multipla_escolha_de_2_ou_10_alternativas_vale(int quantas) => Assert.Empty(Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: TipoDePergunta.MultiplaEscolha, alternativas: Alternativas(quantas)))).Erros);

    [Theory]
    [InlineData(TipoDePergunta.RespostaCurta)]
    [InlineData(TipoDePergunta.RespostaLonga)]
    [InlineData(TipoDePergunta.Escala)]
    [InlineData(TipoDePergunta.Arquivo)]
    public void Os_outros_tipos_nao_levam_alternativas(TipoDePergunta tipo)
    {
        Assert.Equal(["Só a múltipla escolha tem alternativas."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: tipo, alternativas: Alternativas(2)))).Erros["alternativas"]);
        Assert.Equal(["Só a múltipla escolha tem alternativas."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: tipo, alternativas: Alternativas(1)))).Erros["alternativas"]); // uma só também
        Assert.Empty(Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: tipo))).Erros);
    }

    [Fact]
    public void O_texto_da_pergunta_vai_de_1_a_500_a_alternativa_de_1_a_255_e_a_ordem_comeca_em_1()
    {
        Assert.Empty(Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(new string('p', 500)))).Erros);
        Assert.Empty(Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: TipoDePergunta.MultiplaEscolha, alternativas: [new PerguntaAlternativaComando("A"), new PerguntaAlternativaComando(new string('a', 255))]))).Erros); // 255 vale
        Assert.Equal(["Informe o texto da pergunta, com até 500 caracteres."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(new string('p', 501)))).Erros["texto"]);
        Assert.Equal(["Informe o texto da pergunta, com até 500 caracteres."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta("   "))).Erros["texto"]);
        Assert.Equal(["Cada alternativa tem de 1 a 255 caracteres."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: TipoDePergunta.MultiplaEscolha, alternativas: [new PerguntaAlternativaComando("A"), new PerguntaAlternativaComando(new string('a', 256))]))).Erros["alternativas"]);
        Assert.Equal(["Cada alternativa tem de 1 a 255 caracteres."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(tipo: TipoDePergunta.MultiplaEscolha, alternativas: [new PerguntaAlternativaComando("A"), new PerguntaAlternativaComando("  ")]))).Erros["alternativas"]);
        Assert.Equal(["A posição começa em 1."], Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(ordem: 0))).Erros["ordem"]);
        Assert.Empty(Ler(e => ValidacaoDaPergunta.Validar(e, Pergunta(ordem: 1))).Erros);
    }

    // ---------------------------------------------------------------- a publicação

    private static FormularioPublicacaoComando Publicacao(AlvoDoFormulario alvo, int[]? setores = null, int[]? pessoas = null, DateOnly? agendarPara = null, DateOnly? prazo = null, FormularioRecorrenciaComando? recorrencia = null, bool lembrete = true) =>
        new(alvo) { SetorIds = setores ?? [], FuncionarioIds = pessoas ?? [], AgendarPara = agendarPara, Prazo = prazo, Recorrencia = recorrencia, LembreteAutomatico = lembrete };

    private static (PublicacaoValidada? Valor, IReadOnlyDictionary<string, string[]> Erros) LerPublicacao(FormularioPublicacaoComando comando, TipoDeFormulario tipo = TipoDeFormulario.Pesquisa) => Ler(e => ValidacaoDaPublicacao.Validar(e, comando, Hoje, tipo));

    [Fact]
    public void Cada_alvo_exige_as_listas_certas()
    {
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Link), TipoDeFormulario.Entrevista).Erros);
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Setores, setores: [1, 2])).Erros);
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Setores, setores: [7])).Erros); // um setor só vale
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [5])).Erros);
        Assert.Equal(["Por link, não se escolhem setores nem pessoas."], LerPublicacao(Publicacao(AlvoDoFormulario.Link, setores: [1])).Erros["setorIds"]);
        Assert.Equal(["Por link, não se escolhem setores nem pessoas."], LerPublicacao(Publicacao(AlvoDoFormulario.Link, pessoas: [1])).Erros["funcionarioIds"]);
        Assert.Equal(["Escolha pelo menos um setor."], LerPublicacao(Publicacao(AlvoDoFormulario.Setores)).Erros["setorIds"]);
        Assert.Equal(["Para setores, não se escolhem pessoas."], LerPublicacao(Publicacao(AlvoDoFormulario.Setores, setores: [1], pessoas: [2])).Erros["funcionarioIds"]);
        Assert.Equal(["Escolha pelo menos uma pessoa."], LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas)).Erros["funcionarioIds"]);
        Assert.Equal(["Para pessoas, não se escolhem setores."], LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, setores: [1], pessoas: [2])).Erros["setorIds"]);
    }

    [Fact]
    public void As_listas_saem_sem_repeticao_e_o_lembrete_padrao_e_ligado()
    {
        var (valida, _) = LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [5, 7, 5, 7, 9]));

        Assert.Equal([5, 7, 9], valida!.FuncionarioIds);
        Assert.True(valida.LembreteAutomatico);
        Assert.False(LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [5], lembrete: false)).Valor!.LembreteAutomatico);
        Assert.Equal([3, 4], LerPublicacao(Publicacao(AlvoDoFormulario.Setores, setores: [3, 3, 4])).Valor!.SetorIds);
    }

    [Fact]
    public void A_publicacao_valida_devolve_o_alvo_o_dia_agendado_e_o_prazo_como_vieram()
    {
        var (valida, erros) = LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [5], agendarPara: Hoje.AddDays(3), prazo: Hoje.AddDays(8)));

        Assert.Empty(erros);
        Assert.Equal((AlvoDoFormulario.Pessoas, (DateOnly?)Hoje.AddDays(3), (DateOnly?)Hoje.AddDays(8), null), (valida!.Alvo, valida.AgendarPara, valida.Prazo, valida.Recorrencia));
    }

    [Fact]
    public void O_prazo_nao_pode_ser_anterior_a_hoje_nem_ao_dia_agendado_e_o_agendamento_nao_pode_ser_no_passado()
    {
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], prazo: Hoje)).Erros); // hoje vale
        Assert.Equal(["O prazo não pode ser anterior a hoje."], LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], prazo: Hoje.AddDays(-1))).Erros["prazo"]);
        Assert.Equal(["O prazo não pode ser anterior ao dia agendado."], LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], agendarPara: Hoje.AddDays(10), prazo: Hoje.AddDays(9))).Erros["prazo"]);
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], agendarPara: Hoje.AddDays(10), prazo: Hoje.AddDays(10))).Erros);
        Assert.Equal(["O dia agendado não pode ser anterior a hoje."], LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], agendarPara: Hoje.AddDays(-1))).Erros["agendarPara"]);
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], agendarPara: Hoje)).Erros);
    }

    [Fact]
    public void A_sugestao_nao_tem_prazo_nem_recorrencia()
    {
        var (_, erros) = LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], prazo: Hoje.AddDays(5), recorrencia: new FormularioRecorrenciaComando(1, UnidadeDeRecorrencia.Mes)), TipoDeFormulario.Outro);

        Assert.Equal(["A caixa de sugestões é sempre aberta: não tem prazo."], erros["prazo"]);
        Assert.Equal(["A caixa de sugestões é sempre aberta: não se repete."], erros["recorrencia"]);
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1]), TipoDeFormulario.Outro).Erros);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(0, false)]
    [InlineData(366, false)]
    [InlineData(-3, false)]
    public void O_intervalo_da_recorrencia_vai_de_1_a_365(int intervalo, bool vale)
    {
        var (valida, erros) = LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], prazo: Hoje.AddDays(10), recorrencia: new FormularioRecorrenciaComando(intervalo, UnidadeDeRecorrencia.Dia)));

        Assert.Equal(vale, valida is not null);
        if (!vale)
        {
            Assert.Equal(["O intervalo vai de 1 a 365."], erros["recorrencia"]);
        }
    }

    [Fact]
    public void A_recorrencia_exige_prazo_nao_vale_por_link_e_o_fim_nao_pode_ser_no_passado()
    {
        Assert.Equal(["Informe o prazo: a rodada nova avança o mesmo número de dias da anterior."], LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], recorrencia: new FormularioRecorrenciaComando(1, UnidadeDoMes()))).Erros["recorrencia"]);
        Assert.Equal(["O formulário por link não pode ser recorrente: reabrir apagaria as candidaturas."], LerPublicacao(Publicacao(AlvoDoFormulario.Link, prazo: Hoje.AddDays(5), recorrencia: new FormularioRecorrenciaComando(1, UnidadeDoMes())), TipoDeFormulario.Entrevista).Erros["recorrencia"]);
        Assert.Equal(["A data final da recorrência não pode ser anterior a hoje."], LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], prazo: Hoje.AddDays(5), recorrencia: new FormularioRecorrenciaComando(1, UnidadeDoMes()) { Ate = Hoje.AddDays(-1) })).Erros["recorrencia"]);
        Assert.Empty(LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], prazo: Hoje.AddDays(5), recorrencia: new FormularioRecorrenciaComando(1, UnidadeDoMes()) { Ate = Hoje })).Erros); // o último dia vale
        var (valida, erros) = LerPublicacao(Publicacao(AlvoDoFormulario.Pessoas, pessoas: [1], prazo: Hoje.AddDays(5), recorrencia: new FormularioRecorrenciaComando(2, UnidadeDeRecorrencia.Semana) { Ate = Hoje.AddDays(90) }));
        Assert.Empty(erros);
        Assert.Equal((2, UnidadeDeRecorrencia.Semana, (DateOnly?)Hoje.AddDays(90)), (valida!.Recorrencia!.Intervalo, valida.Recorrencia.Unidade, valida.Recorrencia.Ate));
    }

    private static UnidadeDeRecorrencia UnidadeDoMes() => UnidadeDeRecorrencia.Mes;

    [Fact]
    public void Os_erros_da_publicacao_vem_juntos()
    {
        var (_, erros) = LerPublicacao(Publicacao(AlvoDoFormulario.Setores, pessoas: [1], prazo: Hoje.AddDays(-2), agendarPara: Hoje.AddDays(-1)));

        Assert.Equal(["agendarPara", "funcionarioIds", "prazo", "setorIds"], erros.Keys.Order().ToArray());
    }

    // ---------------------------------------------------------------- minutos estimados e o resumo de quem responde

    [Fact]
    public void Os_minutos_estimados_somam_o_peso_de_cada_tipo_multiplicam_pelos_avaliados_e_o_minimo_e_1()
    {
        Assert.Equal(1, MinutosEstimados.De([], 0));
        Assert.Equal(1, MinutosEstimados.De([TipoDePergunta.RespostaCurta], 1));
        Assert.Equal(6, MinutosEstimados.De([TipoDePergunta.RespostaCurta, TipoDePergunta.RespostaLonga, TipoDePergunta.MultiplaEscolha, TipoDePergunta.Escala, TipoDePergunta.Arquivo, TipoDePergunta.RespostaLonga], 0));
        Assert.Equal(18, MinutosEstimados.De([TipoDePergunta.RespostaCurta, TipoDePergunta.RespostaLonga, TipoDePergunta.MultiplaEscolha, TipoDePergunta.Escala, TipoDePergunta.Arquivo, TipoDePergunta.RespostaLonga], 3));
        Assert.Equal(2, MinutosEstimados.De([TipoDePergunta.Escala, TipoDePergunta.Escala, TipoDePergunta.Escala], 1)); // 1,5 minuto arredonda para cima
    }

    [Theory]
    [InlineData(AlvoDoFormulario.Link, null, 0, 0, false, 0, 0, "Candidatos, por link")]
    [InlineData(null, null, 0, 0, false, 0, 0, "Ainda não definido")]
    [InlineData(AlvoDoFormulario.Pessoas, null, 3, 0, false, 0, 0, "3 pessoas escolhidas")]
    [InlineData(AlvoDoFormulario.Pessoas, null, 1, 0, false, 0, 0, "1 pessoa escolhida")]
    [InlineData(AlvoDoFormulario.Setores, null, 101, 5, true, 0, 0, "Todos os setores (101 pessoas)")]
    [InlineData(AlvoDoFormulario.Setores, null, 12, 2, false, 0, 0, "2 setores (12 pessoas)")]
    [InlineData(AlvoDoFormulario.Setores, null, 4, 1, false, 0, 0, "1 setor (4 pessoas)")]
    [InlineData(AlvoDoFormulario.Setores, null, 1, 1, false, 0, 0, "1 setor (1 pessoa)")]
    [InlineData(AlvoDoFormulario.Setores, null, 0, 2, false, 0, 0, "2 setores (0 pessoas)")]
    [InlineData(AlvoDoFormulario.Pessoas, null, 0, 0, false, 0, 0, "0 pessoas escolhidas")]
    public void O_resumo_de_quem_responde_para_formularios_comuns(AlvoDoFormulario? alvo, SentidoDaAvaliacao? sentido, int pessoas, int setores, bool todos, int gestores, int avaliacoes, string esperado) =>
        Assert.Equal(esperado, QuemRespondeResumo.Texto(new ResumoDoPublico(alvo, sentido, false, pessoas, setores, todos, gestores, avaliacoes)));

    [Fact]
    public void O_resumo_de_quem_responde_nas_avaliacoes_conta_avaliacoes()
    {
        Assert.Equal("3 gestores (24 avaliações)", QuemRespondeResumo.Texto(new ResumoDoPublico(AlvoDoFormulario.Pessoas, SentidoDaAvaliacao.GestorAvaliaEquipe, true, 3, 0, false, 3, 24)));
        Assert.Equal("1 gestor (1 avaliação)", QuemRespondeResumo.Texto(new ResumoDoPublico(AlvoDoFormulario.Pessoas, SentidoDaAvaliacao.GestorAvaliaEquipe, true, 1, 0, false, 1, 1)));
        Assert.Equal("2 gestores (10 avaliações)", QuemRespondeResumo.Texto(new ResumoDoPublico(AlvoDoFormulario.Setores, SentidoDaAvaliacao.GestorAvaliaEquipe, true, 12, 2, false, 2, 10))); // conta os gestores, não as pessoas do setor
        Assert.Equal("Todos os setores (24 avaliações)", QuemRespondeResumo.Texto(new ResumoDoPublico(AlvoDoFormulario.Setores, SentidoDaAvaliacao.EquipeAvaliaGestor, true, 24, 4, true, 0, 24)));
        Assert.Equal("2 setores (9 avaliações)", QuemRespondeResumo.Texto(new ResumoDoPublico(AlvoDoFormulario.Setores, SentidoDaAvaliacao.EquipeAvaliaGestor, true, 9, 2, false, 0, 9)));
        Assert.Equal("5 pessoas escolhidas (5 avaliações)", QuemRespondeResumo.Texto(new ResumoDoPublico(AlvoDoFormulario.Pessoas, SentidoDaAvaliacao.EquipeAvaliaGestor, true, 5, 0, false, 0, 5)));
    }
}
