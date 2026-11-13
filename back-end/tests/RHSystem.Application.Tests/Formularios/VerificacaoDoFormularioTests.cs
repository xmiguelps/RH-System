using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// Os itens da etapa Verificação, na ordem do contrato (<c>TITULO_E_DESCRICAO</c>, <c>PERGUNTAS</c>, <c>ALTERNATIVAS</c>, <c>ANONIMO_SEM_ARQUIVO</c>, <c>ANONIMO_RESULTADO_MINIMO</c>, <c>ANONIMO_PUBLICO_MINIMO</c>, <c>DADO_SENSIVEL</c>), com os textos dos dois exemplos do contrato
/// ao pé da letra. <c>BLOQUEIO</c> impede publicar, <c>AVISO</c> não. A verificação do dado sensível é por palavra-chave, sem diferenciar caixa nem acento, e só avisa.
/// </summary>
public class VerificacaoDoFormularioTests
{
    private static PerguntaLida Pergunta(int id, int ordem, string texto, TipoDePergunta tipo = TipoDePergunta.RespostaCurta, bool obrigatoria = false, int alternativas = 0) =>
        new(id, ordem, texto, tipo, obrigatoria, [.. Enumerable.Range(1, alternativas).Select(i => new AlternativaLida(id * 100 + i, i, $"Opção {i}"))]);

    private static QuemDeveResponder Quem(int pessoas, AlvoDoFormulario? alvo = AlvoDoFormulario.Pessoas, bool avaliacao = false) =>
        new(alvo, avaliacao ? SentidoDaAvaliacao.EquipeAvaliaGestor : null, avaliacao, [.. Enumerable.Range(1, pessoas).Select(i => new ParDeResposta(i, avaliacao ? 999 : null))], pessoas, 0, false, 0);

    private static FormularioVerificacao Verificar(string titulo = "Pesquisa", string? descricao = "Conte como foi.", bool anonimo = false, TipoDeFormulario tipo = TipoDeFormulario.Pesquisa, PerguntaLida[]? perguntas = null, QuemDeveResponder? quem = null) =>
        RegrasDaVerificacao.Montar(new EntradaDaVerificacao(titulo, descricao, tipo, anonimo, perguntas ?? [Pergunta(1, 1, "Como foi?")], quem ?? Quem(3)));

    private static (string Codigo, SituacaoDoItem Situacao, string Mensagem, int? PerguntaId) Item(ItemDeVerificacao i) => (i.Codigo, i.Situacao, i.Mensagem, i.PerguntaId);

    // ---------------------------------------------------------------- o exemplo "tudo certo" do contrato

    [Fact]
    public void O_exemplo_tudo_certo_do_contrato_sai_com_os_quatro_itens_e_pode_publicar()
    {
        var v = Verificar(perguntas:
        [
            Pergunta(1, 1, "Nome", obrigatoria: true),
            Pergunta(2, 2, "Escolha", TipoDePergunta.MultiplaEscolha, true, alternativas: 2),
            Pergunta(3, 3, "Nota", TipoDePergunta.Escala, true),
            Pergunta(4, 4, "Comentário", TipoDePergunta.RespostaLonga),
        ]);

        Assert.True(v.PodePublicar);
        Assert.Equal(
            [
                ("TITULO_E_DESCRICAO", SituacaoDoItem.Ok, "Título e descrição preenchidos.", (int?)null),
                ("PERGUNTAS", SituacaoDoItem.Ok, "4 perguntas, 3 obrigatórias.", null),
                ("ALTERNATIVAS", SituacaoDoItem.Ok, "Toda pergunta de múltipla escolha tem pelo menos 2 alternativas.", null),
                ("DADO_SENSIVEL", SituacaoDoItem.Ok, "Nenhuma pergunta pede dado sensível, como saúde, religião ou orientação sexual.", null),
            ],
            v.Itens.Select(Item));
    }

    // ---------------------------------------------------------------- o exemplo anônimo com avisos e bloqueios

    [Fact]
    public void O_exemplo_anonimo_com_avisos_e_dois_bloqueios_do_contrato_sai_ao_pe_da_letra()
    {
        var v = Verificar(
            descricao: null,
            anonimo: true,
            perguntas: [Pergunta(601, 1, "Escolha uma", TipoDePergunta.MultiplaEscolha, true, alternativas: 1), Pergunta(602, 2, "Você tem algum problema de saúde?")],
            quem: Quem(4));

        Assert.False(v.PodePublicar);
        Assert.Equal(
            [
                ("TITULO_E_DESCRICAO", SituacaoDoItem.Aviso, "O formulário não tem descrição.", (int?)null),
                ("PERGUNTAS", SituacaoDoItem.Ok, "2 perguntas, 1 obrigatória.", null),
                ("ALTERNATIVAS", SituacaoDoItem.Bloqueio, "A pergunta 1 é de múltipla escolha e precisa de pelo menos 2 alternativas.", 601),
                ("ANONIMO_RESULTADO_MINIMO", SituacaoDoItem.Aviso, "O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas com 3 respostas ou mais.", null),
                ("ANONIMO_PUBLICO_MINIMO", SituacaoDoItem.Bloqueio, "O formulário anônimo está para 4 pessoas e precisa de pelo menos 5.", null),
                ("DADO_SENSIVEL", SituacaoDoItem.Aviso, "A pergunta 2 parece pedir dado de saúde. Confira se ela é necessária.", 602),
            ],
            v.Itens.Select(Item));
    }

    // ---------------------------------------------------------------- título, perguntas e alternativas

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_descricao_e_aviso_e_nao_impede_publicar(string? descricao)
    {
        var v = Verificar(descricao: descricao);

        Assert.True(v.PodePublicar);
        Assert.Equal(("TITULO_E_DESCRICAO", SituacaoDoItem.Aviso, "O formulário não tem descrição."), (v.Itens[0].Codigo, v.Itens[0].Situacao, v.Itens[0].Mensagem));
    }

    [Fact]
    public void Sem_nenhuma_pergunta_e_bloqueio_e_o_singular_vale_para_uma_so()
    {
        var vazio = Verificar(perguntas: []);
        var uma = Verificar(perguntas: [Pergunta(1, 1, "Única", obrigatoria: true)]);
        var nenhumaObrigatoria = Verificar(perguntas: [Pergunta(1, 1, "A"), Pergunta(2, 2, "B")]);

        Assert.False(vazio.PodePublicar);
        Assert.Equal(("PERGUNTAS", SituacaoDoItem.Bloqueio, "O formulário não tem nenhuma pergunta."), (vazio.Itens[1].Codigo, vazio.Itens[1].Situacao, vazio.Itens[1].Mensagem));
        Assert.Equal("1 pergunta, 1 obrigatória.", uma.Itens[1].Mensagem);
        Assert.Equal("2 perguntas, 0 obrigatórias.", nenhumaObrigatoria.Itens[1].Mensagem);
        Assert.DoesNotContain(vazio.Itens, i => i.Codigo == "ALTERNATIVAS" && i.Situacao == SituacaoDoItem.Bloqueio);
    }

    [Fact]
    public void Cada_multipla_escolha_com_menos_de_2_alternativas_e_um_bloqueio_com_a_posicao_dela_no_texto_e_o_id_no_item()
    {
        var v = Verificar(perguntas: [Pergunta(11, 1, "A", TipoDePergunta.MultiplaEscolha, alternativas: 0), Pergunta(12, 2, "B"), Pergunta(13, 3, "C", TipoDePergunta.MultiplaEscolha, alternativas: 1), Pergunta(14, 4, "D", TipoDePergunta.MultiplaEscolha, alternativas: 2)]);

        var bloqueios = v.Itens.Where(i => i.Codigo == "ALTERNATIVAS").ToList();

        Assert.False(v.PodePublicar);
        Assert.Equal(
            [("ALTERNATIVAS", SituacaoDoItem.Bloqueio, "A pergunta 1 é de múltipla escolha e precisa de pelo menos 2 alternativas.", (int?)11), ("ALTERNATIVAS", SituacaoDoItem.Bloqueio, "A pergunta 3 é de múltipla escolha e precisa de pelo menos 2 alternativas.", 13)],
            bloqueios.Select(Item));
    }

    // ---------------------------------------------------------------- o anônimo

    [Fact]
    public void Formulario_com_nome_nao_tem_os_itens_do_anonimo()
    {
        var v = Verificar(anonimo: false, perguntas: [Pergunta(1, 1, "Envie", TipoDePergunta.Arquivo)]);

        Assert.DoesNotContain(v.Itens, i => i.Codigo.StartsWith("ANONIMO", StringComparison.Ordinal));
        Assert.True(v.PodePublicar);
    }

    [Fact]
    public void Anonimo_com_pergunta_de_arquivo_e_bloqueio_e_o_item_so_aparece_quando_bloqueia()
    {
        var com = Verificar(anonimo: true, perguntas: [Pergunta(1, 1, "Como foi?"), Pergunta(2, 2, "Envie o comprovante", TipoDePergunta.Arquivo)], quem: Quem(5));
        var sem = Verificar(anonimo: true, quem: Quem(5));

        Assert.False(com.PodePublicar);
        Assert.Contains(("ANONIMO_SEM_ARQUIVO", SituacaoDoItem.Bloqueio, "Formulário anônimo não pode ter pergunta de envio de arquivo.", (int?)2), com.Itens.Select(Item));
        Assert.DoesNotContain(sem.Itens, i => i.Codigo == "ANONIMO_SEM_ARQUIVO");
        Assert.True(sem.PodePublicar);
    }

    [Theory]
    [InlineData(5, SituacaoDoItem.Ok, "5 pessoas.")]
    [InlineData(101, SituacaoDoItem.Ok, "101 pessoas.")]
    [InlineData(4, SituacaoDoItem.Bloqueio, "O formulário anônimo está para 4 pessoas e precisa de pelo menos 5.")]
    [InlineData(1, SituacaoDoItem.Bloqueio, "O formulário anônimo está para 1 pessoa e precisa de pelo menos 5.")]
    [InlineData(0, SituacaoDoItem.Bloqueio, "O formulário anônimo está para 0 pessoas e precisa de pelo menos 5.")]
    public void O_publico_minimo_do_anonimo_e_5_pessoas(int pessoas, SituacaoDoItem situacao, string mensagem)
    {
        var v = Verificar(anonimo: true, quem: Quem(pessoas));

        var item = v.Itens.Single(i => i.Codigo == "ANONIMO_PUBLICO_MINIMO");
        Assert.Equal((situacao, mensagem, situacao != SituacaoDoItem.Bloqueio), (item.Situacao, item.Mensagem, v.PodePublicar));
    }

    [Fact]
    public void Publico_ainda_nao_escolhido_e_aviso_para_escolher_na_etapa_Publicacao_e_nao_impede()
    {
        var v = Verificar(anonimo: true, quem: Quem(0, alvo: null));

        var item = v.Itens.Single(i => i.Codigo == "ANONIMO_PUBLICO_MINIMO");
        Assert.Equal((SituacaoDoItem.Aviso, "Escolha pelo menos 5 pessoas na etapa Publicação."), (item.Situacao, item.Mensagem));
        Assert.True(v.PodePublicar);
    }

    [Fact]
    public void O_aviso_do_resultado_minimo_cita_os_avaliados_na_avaliacao()
    {
        var comum = Verificar(anonimo: true, quem: Quem(5));
        var avaliacao = Verificar(anonimo: true, tipo: TipoDeFormulario.Avaliacao, quem: Quem(5, avaliacao: true));

        Assert.Equal("O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas com 3 respostas ou mais.", comum.Itens.Single(i => i.Codigo == "ANONIMO_RESULTADO_MINIMO").Mensagem);
        Assert.Equal("O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas e dos avaliados com 3 respostas ou mais.", avaliacao.Itens.Single(i => i.Codigo == "ANONIMO_RESULTADO_MINIMO").Mensagem);
    }

    [Fact]
    public void A_ordem_dos_itens_e_a_do_contrato_com_tudo_junto()
    {
        var v = Verificar(descricao: null, anonimo: true, perguntas: [Pergunta(1, 1, "Envie", TipoDePergunta.Arquivo), Pergunta(2, 2, "Tem alguma doença?")], quem: Quem(2));

        Assert.Equal(["TITULO_E_DESCRICAO", "PERGUNTAS", "ALTERNATIVAS", "ANONIMO_SEM_ARQUIVO", "ANONIMO_RESULTADO_MINIMO", "ANONIMO_PUBLICO_MINIMO", "DADO_SENSIVEL"], v.Itens.Select(i => i.Codigo));
    }

    // ---------------------------------------------------------------- dado sensível

    [Theory]
    [InlineData("Você tem algum problema de saúde?", "saúde")]
    [InlineData("QUAL A SUA DOENÇA CRÔNICA?", "saúde")]
    [InlineData("Toma algum medicamento controlado?", "saúde")]
    [InlineData("Possui deficiencia fisica?", "saúde")]
    [InlineData("Qual é a sua religião?", "religião")]
    [InlineData("Você frequenta alguma igreja?", "religião")]
    [InlineData("Qual a sua crença?", "religião")]
    [InlineData("Qual a sua orientação sexual?", "orientação sexual")]
    [InlineData("Você é homossexual?", "orientação sexual")]
    public void O_dado_sensivel_e_achado_sem_diferenciar_caixa_nem_acento(string pergunta, string categoria)
    {
        var v = Verificar(perguntas: [Pergunta(7, 3, pergunta)]);

        var item = v.Itens.Single(i => i.Codigo == "DADO_SENSIVEL");
        Assert.Equal((SituacaoDoItem.Aviso, $"A pergunta 3 parece pedir dado de {categoria}. Confira se ela é necessária.", (int?)7), (item.Situacao, item.Mensagem, item.PerguntaId));
        Assert.True(v.PodePublicar); // só avisa, nunca bloqueia
    }

    [Theory]
    [InlineData("Como foi o seu atendimento?")]
    [InlineData("Qual o seu setor?")]
    [InlineData("O que podemos melhorar no refeitório?")]
    [InlineData("Quantos dias de férias você prefere?")]
    [InlineData("Avalie o gayatri mantra do escritório")] // "gay" só vale como palavra inteira
    public void Pergunta_comum_nao_acusa_dado_sensivel(string pergunta)
    {
        var v = Verificar(perguntas: [Pergunta(1, 1, pergunta)]);

        Assert.Equal(SituacaoDoItem.Ok, v.Itens.Single(i => i.Codigo == "DADO_SENSIVEL").Situacao);
    }

    [Fact]
    public void Cada_pergunta_sensivel_vira_um_aviso_com_o_seu_id_e_a_categoria_dela()
    {
        var v = Verificar(perguntas: [Pergunta(1, 1, "Como foi?"), Pergunta(2, 2, "Qual a sua religião?"), Pergunta(3, 3, "Tem alguma doença?")]);

        var avisos = v.Itens.Where(i => i.Codigo == "DADO_SENSIVEL").ToList();

        Assert.Equal([("DADO_SENSIVEL", SituacaoDoItem.Aviso, "A pergunta 2 parece pedir dado de religião. Confira se ela é necessária.", (int?)2), ("DADO_SENSIVEL", SituacaoDoItem.Aviso, "A pergunta 3 parece pedir dado de saúde. Confira se ela é necessária.", 3)], avisos.Select(Item));
    }

    [Fact]
    public void O_dado_sensivel_so_olha_o_texto_da_pergunta_e_a_alternativa_tambem()
    {
        var comAlternativa = new PerguntaLida(9, 1, "Marque", TipoDePergunta.MultiplaEscolha, false, [new AlternativaLida(1, 1, "Católica"), new AlternativaLida(2, 2, "Evangélica")]);
        var comReligiaoNaAlternativa = new PerguntaLida(10, 2, "Qual?", TipoDePergunta.MultiplaEscolha, false, [new AlternativaLida(3, 1, "Minha religião é"), new AlternativaLida(4, 2, "Nenhuma")]);

        var v = Verificar(perguntas: [comAlternativa, comReligiaoNaAlternativa]);

        Assert.Equal([("DADO_SENSIVEL", SituacaoDoItem.Aviso, "A pergunta 2 parece pedir dado de religião. Confira se ela é necessária.", (int?)10)], v.Itens.Where(i => i.Codigo == "DADO_SENSIVEL").Select(Item));
    }
}
