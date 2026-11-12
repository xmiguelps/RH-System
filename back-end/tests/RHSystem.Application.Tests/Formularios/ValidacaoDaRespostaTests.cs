using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// A parte <c>respostas</c> do envio contra as perguntas do formulário, sem banco: cada item usa só o campo do tipo da pergunta, com os limites do contrato (curta 200, longa 4000, escala de 1 a 5, uma alternativa DA pergunta, arquivo por posição válida e usada uma vez); a opcional em branco não gera
/// resposta; a obrigatória em branco ou fora da lista é erro; ao menos uma resposta; todos os erros juntos, em chaves como <c>respostas[2].texto</c>.
/// </summary>
public class ValidacaoDaRespostaTests
{
    private static readonly PerguntaLida Curta = new(1, 1, "Nome", TipoDePergunta.RespostaCurta, true, []);
    private static readonly PerguntaLida Longa = new(2, 2, "Comentário", TipoDePergunta.RespostaLonga, false, []);
    private static readonly PerguntaLida Multipla = new(3, 3, "Gostou?", TipoDePergunta.MultiplaEscolha, true, [new AlternativaLida(31, 1, "Sim"), new AlternativaLida(32, 2, "Não")]);
    private static readonly PerguntaLida Escala = new(4, 4, "Nota", TipoDePergunta.Escala, true, []);
    private static readonly PerguntaLida Anexo = new(5, 5, "Comprovante", TipoDePergunta.Arquivo, false, []);
    private static readonly PerguntaLida[] Todas = [Curta, Longa, Multipla, Escala, Anexo];

    private static (IReadOnlyList<RespostaValidada> Linhas, IReadOnlyDictionary<string, string[]> Erros) Validar(string? json, int arquivos = 0, params PerguntaLida[] perguntas)
    {
        var coletor = new ColetorDeErros();
        var linhas = ValidacaoDaResposta.Validar(coletor, perguntas.Length == 0 ? Todas : perguntas, json, arquivos);
        try
        {
            coletor.LancarSeHouver();
            return (linhas, new Dictionary<string, string[]>());
        }
        catch (ErroDeValidacaoException erro)
        {
            return (linhas, erro.Erros);
        }
    }

    private const string Basicas = """{"perguntaId":1,"texto":"Ana"},{"perguntaId":3,"alternativaIds":[31]},{"perguntaId":4,"valorEscala":4}""";

    [Fact]
    public void Respostas_validas_viram_linhas_na_ordem_das_perguntas_com_o_texto_cortado_a_alternativa_e_a_escala()
    {
        var (linhas, erros) = Validar($$"""[{"perguntaId":4,"valorEscala":5},{"perguntaId":1,"texto":"  Ana Souza  "},{"perguntaId":3,"alternativaIds":[32]},{"perguntaId":5,"arquivoIndice":0}]""", arquivos: 1);

        Assert.Empty(erros);
        Assert.Equal([1, 3, 4, 5], linhas.Select(l => l.Pergunta.Id));
        Assert.Equal(["Ana Souza", "Não", "5", ""], linhas.Select(l => l.Resposta));
        Assert.Equal([null, 32, null, null], linhas.Select(l => l.AlternativaId));
        Assert.Equal([null, null, null, 0], linhas.Select(l => l.ArquivoIndice));
    }

    [Fact]
    public void A_opcional_em_branco_ou_ausente_nao_gera_linha_nem_erro()
    {
        var (linhas, erros) = Validar($$"""[{{Basicas}},{"perguntaId":2,"texto":"   "},{"perguntaId":5}]""");

        Assert.Empty(erros);
        Assert.Equal([1, 3, 4], linhas.Select(l => l.Pergunta.Id));
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void A_resposta_curta_vai_ate_200_caracteres(int tamanho, bool aceita)
    {
        var (_, erros) = Validar($$"""[{"perguntaId":1,"texto":"{{new string('a', tamanho)}}"},{"perguntaId":3,"alternativaIds":[31]},{"perguntaId":4,"valorEscala":4}]""");

        Assert.Equal(aceita, erros.Count == 0);
        Assert.Equal(!aceita, erros.ContainsKey("respostas[0].texto"));
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void A_resposta_longa_vai_ate_4000_caracteres(int tamanho, bool aceita)
    {
        var (_, erros) = Validar($$"""[{"perguntaId":2,"texto":"{{new string('b', tamanho)}}"},{{Basicas}}]""");

        Assert.Equal(aceita, erros.Count == 0);
        Assert.Equal(!aceita, erros.ContainsKey("respostas[0].texto"));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("5", true)]
    [InlineData("0", false)]
    [InlineData("6", false)]
    [InlineData("4.5", false)]
    [InlineData("\"4\"", false)]
    [InlineData("true", false)]
    [InlineData("[4]", false)]
    public void A_escala_e_um_inteiro_de_1_a_5(string valor, bool aceita)
    {
        var (_, erros) = Validar($$"""[{"perguntaId":4,"valorEscala":{{valor}}},{"perguntaId":1,"texto":"Ana"},{"perguntaId":3,"alternativaIds":[31]}]""");

        Assert.Equal(aceita, erros.Count == 0);
        Assert.Equal(!aceita, erros.ContainsKey("respostas[0].valorEscala"));
    }

    [Theory]
    [InlineData("[31,32]")]
    [InlineData("[99]")]
    [InlineData("[\"31\"]")]
    [InlineData("31")]
    public void A_multipla_escolha_aceita_uma_so_alternativa_da_propria_pergunta(string alternativas)
    {
        var (_, erros) = Validar($$"""[{"perguntaId":3,"alternativaIds":{{alternativas}}},{"perguntaId":1,"texto":"Ana"},{"perguntaId":4,"valorEscala":4}]""");

        Assert.True(erros.ContainsKey("respostas[0].alternativaIds"));
    }

    [Fact]
    public void Lista_de_alternativas_vazia_vale_como_em_branco()
    {
        var (_, obrigatoria) = Validar("""[{"perguntaId":3,"alternativaIds":[]},{"perguntaId":1,"texto":"Ana"},{"perguntaId":4,"valorEscala":4}]""");
        var opcional = new PerguntaLida(6, 6, "Opção", TipoDePergunta.MultiplaEscolha, false, [new AlternativaLida(61, 1, "A")]);
        var (linhas, semErro) = Validar("""[{"perguntaId":1,"texto":"Ana"},{"perguntaId":6,"alternativaIds":[]}]""", 0, Curta, opcional);

        Assert.True(obrigatoria.ContainsKey("respostas[0].alternativaIds"));
        Assert.Empty(semErro);
        Assert.Equal([1], linhas.Select(l => l.Pergunta.Id));
    }

    [Theory]
    [InlineData("""{"perguntaId":1,"texto":"Ana","valorEscala":3}""", "respostas[0].valorEscala")]
    [InlineData("""{"perguntaId":1,"texto":"Ana","alternativaIds":[31]}""", "respostas[0].alternativaIds")]
    [InlineData("""{"perguntaId":1,"texto":"Ana","arquivoIndice":0}""", "respostas[0].arquivoIndice")]
    [InlineData("""{"perguntaId":4,"valorEscala":3,"texto":"x"}""", "respostas[0].texto")]
    [InlineData("""{"perguntaId":3,"alternativaIds":[31],"valorEscala":3}""", "respostas[0].valorEscala")]
    [InlineData("""{"perguntaId":5,"arquivoIndice":0,"texto":"x"}""", "respostas[0].texto")]
    public void Campo_de_outro_tipo_e_erro(string item, string chave)
    {
        var (_, erros) = Validar($"[{item},{Basicas}]", arquivos: 1);

        Assert.True(erros.ContainsKey(chave), string.Join(", ", erros.Keys));
    }

    [Fact]
    public void Campo_nulo_conta_como_ausente()
    {
        var (_, erros) = Validar("""[{"perguntaId":1,"texto":"Ana","valorEscala":null,"alternativaIds":null,"arquivoIndice":null},{"perguntaId":3,"alternativaIds":[31]},{"perguntaId":4,"valorEscala":4}]""");

        Assert.Empty(erros);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(-1, false)]
    public void O_indice_do_arquivo_aponta_para_uma_parte_enviada(int indice, bool aceita)
    {
        var (_, erros) = Validar($$"""[{"perguntaId":5,"arquivoIndice":{{indice}}},{{Basicas}}]""", arquivos: 2);

        Assert.Equal(aceita, !erros.ContainsKey("respostas[0].arquivoIndice"));
    }

    [Fact]
    public void O_mesmo_arquivo_em_duas_respostas_e_o_arquivo_que_ninguem_usa_sao_erro()
    {
        var outro = new PerguntaLida(6, 6, "Outro anexo", TipoDePergunta.Arquivo, false, []);
        var (_, repetido) = Validar("""[{"perguntaId":5,"arquivoIndice":0},{"perguntaId":6,"arquivoIndice":0}]""", 1, Anexo, outro);
        var (_, sobrando) = Validar("""[{"perguntaId":5,"arquivoIndice":0}]""", 2, Anexo, outro);

        Assert.True(repetido.ContainsKey("respostas[1].arquivoIndice"));
        Assert.True(sobrando.ContainsKey("arquivos"));
    }

    [Fact]
    public void Pergunta_que_nao_e_do_formulario_sem_id_ou_repetida_e_erro_e_a_obrigatoria_que_faltou_diz_o_numero_dela()
    {
        var (_, desconhecida) = Validar($$"""[{"perguntaId":99,"texto":"x"},{{Basicas}}]""");
        var (_, semId) = Validar($$"""[{"texto":"x"},{{Basicas}}]""");
        var (_, repetida) = Validar($$"""[{{Basicas}},{"perguntaId":1,"texto":"de novo"}]""");
        var (_, faltou) = Validar("""[{"perguntaId":1,"texto":"Ana"},{"perguntaId":4,"valorEscala":4}]""");

        Assert.True(desconhecida.ContainsKey("respostas[0].perguntaId"));
        Assert.True(semId.ContainsKey("respostas[0].perguntaId"));
        Assert.True(repetida.ContainsKey("respostas[3].perguntaId"));
        Assert.Equal(["Responda a pergunta 3, que é obrigatória."], faltou["respostas"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("não é json")]
    [InlineData("{}")]
    [InlineData("\"texto\"")]
    [InlineData("[]")]
    public void Sem_lista_de_respostas_o_erro_e_em_respostas(string? json)
    {
        var (linhas, erros) = Validar(json);

        Assert.Empty(linhas);
        Assert.Equal(["respostas"], erros.Keys);
    }

    [Fact]
    public void Item_que_nao_e_objeto_e_erro_e_responder_so_o_que_e_opcional_em_branco_pede_ao_menos_uma()
    {
        var (_, naoObjeto) = Validar($$"""[7,{{Basicas}}]""");
        var (linhas, vazias) = Validar("""[{"perguntaId":2,"texto":""}]""", 0, Longa);

        Assert.True(naoObjeto.ContainsKey("respostas[0]"));
        Assert.Empty(linhas);
        Assert.Equal(["Responda ao menos uma pergunta."], vazias["respostas"]);
    }

    [Fact]
    public void Os_erros_de_varios_itens_vem_juntos()
    {
        var (_, erros) = Validar("""[{"perguntaId":4,"valorEscala":9},{"perguntaId":1,"texto":"Ana","valorEscala":2}]""");

        Assert.Equal(["respostas", "respostas[0].valorEscala", "respostas[1].valorEscala"], erros.Keys.Order());
    }
}
