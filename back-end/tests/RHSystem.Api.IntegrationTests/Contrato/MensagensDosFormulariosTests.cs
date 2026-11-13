using System.Text.Json;
using System.Text.Json.Nodes;
using RHSystem.Application.Formularios.Mensagens;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Os textos e os códigos de erro dos formulários vêm dos EXEMPLOS do contrato (<c>50-fatia-5-formularios.yaml</c>), copiados e nunca reescritos (o front mostra o <c>detail</c> como vem). Este teste lê o contrato de verdade e confere o status, o título, o texto e o
/// código de cada mensagem. As exceções declaradas são as que o contrato cita e não exemplifica (decisão 10 do plano: o executor escreve o texto) e o código novo <c>FORMULARIO_JA_RESPONDIDO</c>, que a decisão do plano para a lacuna da matriz de edição criou.
/// </summary>
public class MensagensDosFormulariosTests
{
    private sealed record Exemplo(int Status, string Titulo, string Detalhe, string? Codigo);

    /// <summary>Todo objeto do contrato que parece um problema (<c>title</c>, <c>status</c> e <c>detail</c>), em qualquer profundidade.</summary>
    private static List<Exemplo> Exemplos()
    {
        var achados = new List<Exemplo>();

        void Varrer(JsonNode? no)
        {
            switch (no)
            {
                case JsonObject objeto:
                    // O leitor do contrato converte todo valor simples em texto: o status vem como "409".
                    if (objeto["title"]?.GetValueKind() == JsonValueKind.String && objeto["detail"]?.GetValueKind() == JsonValueKind.String
                        && int.TryParse(objeto["status"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, out var status))
                    {
                        achados.Add(new Exemplo(status, objeto["title"]!.GetValue<string>(), objeto["detail"]!.GetValue<string>(), objeto["codigo"]?.GetValue<string>()));
                    }

                    foreach (var (_, filho) in objeto)
                    {
                        Varrer(filho);
                    }

                    break;
                case JsonArray lista:
                    foreach (var filho in lista)
                    {
                        Varrer(filho);
                    }

                    break;
            }
        }

        Varrer(CarregadorDeContrato.Contrato());
        return achados;
    }

    /// <summary>Os textos que o contrato NÃO traz em exemplo (o código é citado na descrição, mas sem <c>detail</c>), escritos pelo executor.</summary>
    private static readonly string[] SemExemplo =
    [
        "Só um formulário publicado pode ser encerrado.",
        "Este formulário já está publicado ou agendado.",
        "Esta pergunta já foi respondida e não pode ser excluída.",
        "Uma pessoa inativa não pode responder formulários.",
        "Este formulário é respondido por link: não há pessoas do cadastro para lembrar.",
        "Este formulário não é respondido por link. Veja a lista de funcionários.",
        "Este formulário não é uma avaliação.",
        "Na caixa de sugestões não há respostas suas para rever: cada envio é uma sugestão nova.",
        "Este formulário já tem respostas. Só o título e a descrição podem mudar.",
    ];

    [Fact]
    public void Cada_mensagem_com_exemplo_tem_status_titulo_texto_e_codigo_de_um_exemplo_do_contrato()
    {
        var exemplos = Exemplos();

        foreach (var mensagem in MensagensDosFormularios.Todas.Where(m => !SemExemplo.Contains(m.Detalhe)))
        {
            var esperado = new Exemplo(mensagem.Status, mensagem.Titulo, mensagem.Detalhe, mensagem.Codigo);
            Assert.True(exemplos.Contains(esperado), $"O contrato não tem um exemplo igual a {mensagem.Codigo}: \"{mensagem.Titulo}\" / \"{mensagem.Detalhe}\".");
        }
    }

    [Fact]
    public void As_mensagens_sem_exemplo_estao_na_lista_de_excecoes_o_contrato_realmente_nao_as_traz_e_cita_o_codigo()
    {
        var exemplos = Exemplos();
        var contrato = CarregadorDeContrato.Contrato().ToJsonString();

        foreach (var mensagem in MensagensDosFormularios.Todas.Where(m => SemExemplo.Contains(m.Detalhe)))
        {
            Assert.DoesNotContain(exemplos, e => e.Detalhe == mensagem.Detalhe); // a lista de exceções não esconde um texto que o contrato já tem
            if (mensagem.Codigo != "FORMULARIO_JA_RESPONDIDO")
            {
                Assert.Contains(mensagem.Codigo, contrato, StringComparison.Ordinal); // o código existe no contrato; só falta o texto
            }
        }

        Assert.DoesNotContain("FORMULARIO_JA_RESPONDIDO", contrato, StringComparison.Ordinal); // o código novo: o contrato não o conhece
        Assert.Equal(SemExemplo.Length, MensagensDosFormularios.Todas.Count(m => SemExemplo.Contains(m.Detalhe)));
    }

    [Fact]
    public void Todo_exemplo_de_erro_dos_formularios_do_contrato_esta_no_catalogo()
    {
        var catalogo = MensagensDosFormularios.Todas.Select(m => new Exemplo(m.Status, m.Titulo, m.Detalhe, m.Codigo)).ToHashSet();
        var codigos = MensagensDosFormularios.Todas.Select(m => m.Codigo).Distinct().ToHashSet();
        var doContrato = Exemplos().Where(e => e.Codigo is not null && codigos.Contains(e.Codigo) && !EhDeOutraFatia(e)).ToList();

        Assert.NotEmpty(doContrato);
        Assert.All(doContrato, e => Assert.True(catalogo.Contains(e) || EhConfirmacao(e), $"O exemplo {e.Codigo} (\"{e.Detalhe}\") do contrato não está em MensagensDosFormularios."));
    }

    private static bool EhConfirmacao(Exemplo e) => e.Codigo == "CONFIRMACAO_NECESSARIA";

    /// <summary>
    /// <c>FUNCIONARIO_INATIVO</c> e <c>EMPRESA_NAO_CONFIGURADA</c> têm textos de outras fatias (o ponto, a 1c, a 4, o pedido de férias da 7): são do catálogo delas. Os dois textos desta fatia começam por "Uma das pessoas escolhidas" e "Este formulário ainda não está disponível".
    /// </summary>
    private static bool EhDeOutraFatia(Exemplo e) =>
        (e.Codigo == "FUNCIONARIO_INATIVO" && !e.Detalhe.StartsWith("Uma das pessoas escolhidas", StringComparison.Ordinal))
        || (e.Codigo == "EMPRESA_NAO_CONFIGURADA" && !e.Detalhe.StartsWith("Este formulário ainda não está disponível", StringComparison.Ordinal));

    [Fact]
    public void Os_codigos_do_catalogo_sao_exatamente_os_da_fatia_e_nenhum_texto_se_repete()
    {
        string[] esperados =
        [
            "ANONIMATO_FIXO_APOS_PUBLICAR", "ANONIMO_PERGUNTAS_FIXAS", "ANONIMO_SEM_NOVOS_DESTINATARIOS", "RESULTADO_ANONIMO_INDISPONIVEL", "JA_RESPONDIDO", "FORMULARIO_ENCERRADO", "FORMULARIO_NAO_PUBLICADO", "FORMULARIO_JA_PUBLICADO",
            "FORMULARIO_AINDA_NAO_ABERTO", "FORMULARIO_JA_RESPONDIDO", "EMPRESA_NAO_CONFIGURADA", "PERGUNTA_JA_RESPONDIDA", "ANONIMO_SEM_ARQUIVO", "ANONIMO_SEM_LINK", "LINK_SO_PARA_ENTREVISTA", "FORMULARIO_ANONIMO_PUBLICO_PEQUENO",
            "VERIFICACAO_COM_BLOQUEIO", "FUNCIONARIO_INATIVO", "FORMULARIO_POR_LINK", "FORMULARIO_SEM_LINK", "FORMULARIO_NAO_E_POR_PESSOAS", "FORMULARIO_NAO_E_AVALIACAO", "FORMULARIO_ANONIMO", "FORMULARIO_SUGESTAO",
            "RESULTADO_SEM_RESPOSTAS_SUFICIENTES", "AVALIADO_INVALIDO",
        ];

        Assert.Equal(esperados.Order(), MensagensDosFormularios.Todas.Select(m => m.Codigo).Distinct().Order());
        Assert.Equal(MensagensDosFormularios.Todas.Count, MensagensDosFormularios.Todas.Select(m => (m.Codigo, m.Detalhe)).Distinct().Count());
    }

    [Fact]
    public void O_status_de_cada_mensagem_e_409_ou_422_com_o_titulo_generico_do_contrato()
    {
        Assert.All(MensagensDosFormularios.Todas, m => Assert.Equal(m.Status == 409 ? "Conflito." : "Operação recusada.", m.Titulo));
        Assert.All(MensagensDosFormularios.Todas, m => Assert.Contains(m.Status, new[] { 409, 422 }));
    }

    [Fact]
    public void A_confirmacao_necessaria_tem_titulo_proprio_e_os_dois_textos_do_contrato_e_pendentes_nas_extensoes()
    {
        var exemplos = Exemplos().Where(e => e.Codigo == "CONFIRMACAO_NECESSARIA").ToList();
        var comNome = MensagensDosFormularios.ConfirmacaoNecessaria(63);
        var anonimo = MensagensDosFormularios.ConfirmacaoNecessaria(null);

        Assert.Contains(new Exemplo(409, "Confirmação necessária.", "63 pessoas ainda não responderam. Confirme para continuar.", "CONFIRMACAO_NECESSARIA"), exemplos);
        Assert.Contains(new Exemplo(409, "Confirmação necessária.", "Num formulário anônimo o sistema não diz quantas pessoas ainda não responderam. Confirme para continuar.", "CONFIRMACAO_NECESSARIA"), exemplos);
        Assert.Equal(new Exemplo(409, "Confirmação necessária.", "63 pessoas ainda não responderam. Confirme para continuar.", "CONFIRMACAO_NECESSARIA"), new Exemplo(comNome.Status, comNome.Titulo, comNome.Detalhe, comNome.Codigo));
        Assert.Equal(new Exemplo(409, "Confirmação necessária.", "Num formulário anônimo o sistema não diz quantas pessoas ainda não responderam. Confirme para continuar.", "CONFIRMACAO_NECESSARIA"), new Exemplo(anonimo.Status, anonimo.Titulo, anonimo.Detalhe, anonimo.Codigo));
        Assert.Equal(63, comNome.Extensoes!["pendentes"]);
        Assert.True(anonimo.Extensoes!.ContainsKey("pendentes"));
        Assert.Null(anonimo.Extensoes["pendentes"]);
    }

    [Fact]
    public void A_confirmacao_com_uma_so_pessoa_fica_no_singular()
    {
        Assert.Equal("1 pessoa ainda não respondeu. Confirme para continuar.", MensagensDosFormularios.ConfirmacaoNecessaria(1).Detalhe);
        Assert.Equal("2 pessoas ainda não responderam. Confirme para continuar.", MensagensDosFormularios.ConfirmacaoNecessaria(2).Detalhe);
    }

    [Fact]
    public void O_anonimo_pequeno_diz_quantas_pessoas_o_formulario_tem_e_o_minimo_e_5()
    {
        Assert.Equal("Um formulário anônimo precisa ir para pelo menos 5 pessoas. Este vai para 4.", MensagensDosFormularios.AnonimoPequeno(4).Detalhe); // o exemplo do contrato
        Assert.Equal("Um formulário anônimo precisa ir para pelo menos 5 pessoas. Este vai para 0.", MensagensDosFormularios.AnonimoPequeno(0).Detalhe);
        Assert.Equal(5, MensagensDosFormularios.MinimoDePessoasNoAnonimo);
    }

    [Fact]
    public void O_mesmo_codigo_tem_um_texto_por_rota_quando_o_contrato_diz_textos_diferentes()
    {
        Assert.Equal(["Você já respondeu este formulário.", "Já recebemos uma candidatura com este e-mail."], new[] { MensagensDosFormularios.JaRespondidoPeloFuncionario.Detalhe, MensagensDosFormularios.JaRespondidoPeloCandidato.Detalhe });
        Assert.Equal(
            ["O formulário está encerrado e não pode mais ser alterado.", "Este formulário não está mais aceitando respostas.", "O formulário de admissão escolhido já foi encerrado. Escolha outro."],
            new[] { MensagensDosFormularios.FormularioEncerradoParaEditar.Detalhe, MensagensDosFormularios.FormularioEncerradoParaResponder.Detalhe, MensagensDosFormularios.FormularioDeAdmissaoEncerrado.Detalhe });
        Assert.All(new[] { MensagensDosFormularios.JaRespondidoPeloFuncionario, MensagensDosFormularios.JaRespondidoPeloCandidato }, m => Assert.Equal("JA_RESPONDIDO", m.Codigo));
        Assert.All(new[] { MensagensDosFormularios.FormularioEncerradoParaEditar, MensagensDosFormularios.FormularioEncerradoParaResponder, MensagensDosFormularios.FormularioDeAdmissaoEncerrado }, m => Assert.Equal("FORMULARIO_ENCERRADO", m.Codigo));
    }

    [Fact]
    public void ComoProblema_leva_status_titulo_texto_codigo_e_a_regra_do_banco_so_quando_vem()
    {
        var semRegra = MensagensDosFormularios.FormularioJaPublicado.ComoProblema();
        var comRegra = MensagensDosFormularios.AnonimoSemLink.ComoProblema(RegrasDoBancoDosFormularios.AnonimoSemLink);

        Assert.Equal((422, "Operação recusada.", "Este formulário já está publicado ou agendado.", "FORMULARIO_JA_PUBLICADO"), (semRegra.Status, semRegra.Titulo, semRegra.Detalhe, semRegra.Codigo));
        Assert.Null(semRegra.Extensoes);
        Assert.Equal("CHK_FORMULARIOS_ANONIMO_SEM_LINK", comRegra.Extensoes!["regraDoBanco"]);
    }

    [Fact]
    public void A_operacao_recusada_nao_tem_codigo_e_leva_a_mensagem_original_do_banco()
    {
        var problema = MensagensDosFormularios.OperacaoRecusada(RegrasDoBancoDosFormularios.PerguntaObrigatoriaSemResposta);

        Assert.Equal((422, "Operação recusada.", "A operação foi recusada por uma regra do banco de dados."), (problema.Status, problema.Titulo, problema.Detalhe));
        Assert.Null(problema.Codigo);
        Assert.Equal("Pergunta obrigatoria: informe a resposta.", problema.Extensoes!["regraDoBanco"]);
    }

    [Fact]
    public void As_corridas_de_ordem_de_pergunta_viram_409_sem_codigo()
    {
        var problema = MensagensDosFormularios.PerguntasMudaramEmOutraTela;

        Assert.Equal((409, "Conflito.", (string?)null), (problema.Status, problema.Titulo, problema.Codigo));
        Assert.Equal("As perguntas deste formulário mudaram em outra tela. Atualize a página e tente de novo.", problema.Detalhe);
    }

    [Fact]
    public void As_regras_do_banco_sao_as_mensagens_originais_dos_triggers_sem_acento_e_os_nomes_das_chaves()
    {
        Assert.Equal("Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario.", RegrasDoBancoDosFormularios.PerguntaJaRespondidaAoAlterar);
        Assert.Equal("Pergunta ja respondida: nao e possivel exclui-la.", RegrasDoBancoDosFormularios.PerguntaJaRespondidaAoExcluir);
        Assert.Equal("Formulario inativo nao aceita respostas.", RegrasDoBancoDosFormularios.FormularioInativoNaoAceitaRespostas);
        Assert.Equal("Funcionario inativo nao pode responder formularios.", RegrasDoBancoDosFormularios.FuncionarioInativoNaoRespondeFormularios);
        Assert.Equal("Pergunta obrigatoria: informe a resposta.", RegrasDoBancoDosFormularios.PerguntaObrigatoriaSemResposta);
        Assert.Equal("CHK_FORMULARIOS_ANONIMO_SEM_LINK", RegrasDoBancoDosFormularios.AnonimoSemLink);
        Assert.Equal("UQ_FORMULARIOS_CANDIDATOS_FORMULARIO_EMAIL", RegrasDoBancoDosFormularios.ChaveDoEmailDoCandidato);
        Assert.Equal("UQ_FORMULARIOS_PARTICIPACOES_PESSOA", RegrasDoBancoDosFormularios.ChaveDaParticipacao);
        Assert.Equal("UQ_FORMULARIOS_PERGUNTAS_FORMULARIO_ORDEM", RegrasDoBancoDosFormularios.ChaveDaOrdemDaPergunta);
        Assert.Equal("UQ_FORMULARIOS_ALTERNATIVAS_PERGUNTA_ORDEM", RegrasDoBancoDosFormularios.ChaveDaOrdemDaAlternativa);
        Assert.Equal("UQ_FORMULARIOS_DESTINATARIOS_DESTINO", RegrasDoBancoDosFormularios.ChaveDoDestinatario);
        Assert.Equal("UQ_FORMULARIOS_LINK_PUBLICO_CODIGO", RegrasDoBancoDosFormularios.ChaveDoTokenDoLink);
        Assert.Equal("FK_FORMULARIOS_RESPOSTAS_FORMULARIOS_ANONIMO", RegrasDoBancoDosFormularios.ChaveEstrangeiraDoAnonimato);
    }
}
