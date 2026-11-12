using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Os formulários de quem está logado (<c>listarMeusFormularios</c> e <c>obterFormularioParaResponder</c>), qualquer perfil, no banco REAL com o relógio em 2026-10-07T12:42Z. Entram os PUBLICADOS em que a pessoa deve responder (por pessoa ou por setor) e TODOS os que já respondeu (mesmo encerrados);
/// por link nunca. Admissão e avaliação são "para responder"; pesquisa e Sugestão são "disponíveis"; a Sugestão fica sempre disponível. A bolinha do menu é o <c>contagens.paraResponder</c>. Abrir: 404 para quem não deve responder, 422 para agendado e encerrado (inclusive o prazo vencido com a tarefa parada).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MeusFormulariosTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/formularios";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MeusFormulariosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(HttpClient Cliente, int Id, SetorDeAdvertencia Setor)> PessoaDoSetorAsync(ApiDeAcesso api)
    {
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var (id, _) = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        return (await AdvertenciasDeTeste.ClienteDeAsync(api, id), id, setor);
    }

    private static Task EscolherPessoasAsync(ApiDeAcesso api, int formularioId, params int[] pessoas) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Select(p => $"({formularioId}, {p})"))}");

    private static Task EscolherSetorAsync(ApiDeAcesso api, int formularioId, int setor) => Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({formularioId}, {setor})");

    private static async Task<JsonElement> ListarAsync(HttpClient cliente, string consulta = "") => await CorpoAsync(await cliente.GetAsync(consulta.Length == 0 ? Rota : $"{Rota}?{consulta}"));

    private static List<string> Titulos(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()!)];

    private static JsonElement Item(JsonElement lista, string titulo) => lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("titulo").GetString() == titulo);

    private static string Nulo(JsonElement e, string campo) => e.GetProperty(campo).ValueKind == JsonValueKind.Null ? "null" : e.GetProperty(campo).ToString();

    private static (int, int, int, int) Contagens(JsonElement lista)
    {
        var c = lista.GetProperty("contagens");
        return (c.GetProperty("todos").GetInt32(), c.GetProperty("paraResponder").GetInt32(), c.GetProperty("disponiveis").GetInt32(), c.GetProperty("respondidos").GetInt32());
    }

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_padrao_traz_para_responder_e_disponiveis_com_os_campos_do_cartao_e_deixa_de_fora_o_que_nao_e_da_pessoa()
    {
        var api = await ApiAsync();
        var (cliente, eu, setor) = await PessoaDoSetorAsync(api);
        var outro = await AdvertenciasDeTeste.NovoMembroAsync(api, await AdvertenciasDeTeste.NovoSetorAsync(api));
        var admissao = await SemearAsync(api, "Zmeus1 admissão", "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS", prazo: "2026-10-16", descricao: "Dados da admissão.");
        await PerguntaAsync(api, admissao, "Nome", ordem: 1, obrigatoria: true);
        await PerguntaAsync(api, admissao, "Envie o documento", "ARQUIVO", 2);
        await EscolherPessoasAsync(api, admissao, eu);
        var pesquisa = await SemearAsync(api, "Zmeus1 pesquisa", "PUBLICADO", alvo: "SETORES");
        await PerguntaAsync(api, pesquisa, "Como foi?");
        await EscolherSetorAsync(api, pesquisa, setor.SetorId);
        var sugestao = await SemearAsync(api, "Zmeus1 sugestões", "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        await PerguntaAsync(api, sugestao, "Sua ideia");
        await EscolherPessoasAsync(api, sugestao, eu);
        var deOutro = await SemearAsync(api, "Zmeus1 de outro", "PUBLICADO", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, deOutro, outro.Id);
        var rascunho = await SemearAsync(api, "Zmeus1 rascunho", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, rascunho, eu);
        var agendado = await SemearAsync(api, "Zmeus1 agendado", "AGENDADO", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, agendado, eu);
        var porLink = await SemearAsync(api, "Zmeus1 entrevista", "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('z', 43));
        var vencido = await SemearAsync(api, "Zmeus1 vencido", "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");
        await EscolherPessoasAsync(api, vencido, eu);

        var resposta = await cliente.GetAsync($"{Rota}?busca=Zmeus1");
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarMeusFormularios");
        Assert.Equal(["Zmeus1 admissão", "Zmeus1 pesquisa", "Zmeus1 sugestões"], Titulos(lista).Order());
        Assert.Equal((3, 1, 2, 0), Contagens(lista));
        var a = Item(lista, "Zmeus1 admissão");
        Assert.Equal(("PARA_RESPONDER", "ADMISSAO", 2, "2026-10-16", 0, 1, "Dados da admissão.", false), (a.GetProperty("situacao").GetString(), a.GetProperty("tipo").GetString(), a.GetProperty("totalPerguntas").GetInt32(), a.GetProperty("prazo").GetString(), a.GetProperty("respondidos").GetInt32(), a.GetProperty("total").GetInt32(), a.GetProperty("descricao").GetString(), a.GetProperty("podeVerMinhasRespostas").GetBoolean()));
        Assert.True(a.GetProperty("minutosEstimados").GetInt32() >= 1);
        Assert.Equal("DISPONIVEL", Item(lista, "Zmeus1 pesquisa").GetProperty("situacao").GetString());
        var s = Item(lista, "Zmeus1 sugestões");
        Assert.Equal(("DISPONIVEL", "null", "null", 0, 1, false), (s.GetProperty("situacao").GetString(), Nulo(s, "prazo"), Nulo(s, "respondidoEmUtc"), s.GetProperty("respondidos").GetInt32(), s.GetProperty("total").GetInt32(), s.GetProperty("podeVerMinhasRespostas").GetBoolean()));
    }

    [Fact]
    public async Task Quem_ja_respondeu_ve_o_formulario_na_aba_Respondidos_mesmo_depois_de_encerrado_e_ele_some_da_aba_Todos()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        var aberto = await SemearAsync(api, "Zmeus2 aberto", "PUBLICADO", alvo: "PESSOAS");
        var perguntaAberta = await PerguntaAsync(api, aberto, "Como foi?");
        await EscolherPessoasAsync(api, aberto, eu);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({perguntaAberta}, {aberto}, {eu}, 'Foi bom.', '2026-10-05 10:00:00', 0)");
        var encerrado = await SemearAsync(api, "Zmeus2 encerrado", "PUBLICADO", alvo: "PESSOAS");
        var perguntaEncerrada = await PerguntaAsync(api, encerrado, "Como foi?");
        await EscolherPessoasAsync(api, encerrado, eu);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({perguntaEncerrada}, {encerrado}, {eu}, 'Foi ótimo.', '2026-10-03 08:15:00', 0)");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-04 00:00:00' WHERE ID_FORMULARIO = {encerrado}");

        var todos = await ListarAsync(cliente, "busca=Zmeus2");
        var respondidos = await ListarAsync(cliente, "busca=Zmeus2&situacao=RESPONDIDO");

        Assert.Empty(Titulos(todos));
        Assert.Equal(["Zmeus2 aberto", "Zmeus2 encerrado"], Titulos(respondidos).Order());
        Assert.Equal((0, 0, 0, 2), Contagens(respondidos));
        var item = Item(respondidos, "Zmeus2 encerrado");
        Assert.Equal(("RESPONDIDO", "2026-10-03T11:15:00Z", true, 1, 1), (item.GetProperty("situacao").GetString(), item.GetProperty("respondidoEmUtc").GetString(), item.GetProperty("podeVerMinhasRespostas").GetBoolean(), item.GetProperty("respondidos").GetInt32(), item.GetProperty("total").GetInt32()));
    }

    [Fact]
    public async Task As_contagens_respeitam_busca_e_tipo_e_ignoram_a_situacao_e_o_todos_e_paraResponder_mais_disponiveis()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        foreach (var (titulo, tipo) in new[] { ("Zmeus3 admissão", "ADMISSAO"), ("Zmeus3 pesquisa A", "PESQUISA"), ("Zmeus3 pesquisa B", "PESQUISA") })
        {
            var id = await SemearAsync(api, titulo, "PUBLICADO", tipo: tipo, alvo: "PESSOAS");
            await EscolherPessoasAsync(api, id, eu);
        }

        var tudo = await ListarAsync(cliente, "busca=Zmeus3");
        var soPesquisas = await ListarAsync(cliente, "busca=Zmeus3&tipo=PESQUISA");
        var soParaResponder = await ListarAsync(cliente, "busca=Zmeus3&situacao=PARA_RESPONDER");
        var outraBusca = await ListarAsync(cliente, "busca=Zmeus3 pesquisa A");

        Assert.Equal((3, 1, 2, 0), Contagens(tudo));
        Assert.Equal((2, 0, 2, 0), Contagens(soPesquisas)); // o tipo restringe a contagem
        Assert.Equal((3, 1, 2, 0), Contagens(soParaResponder)); // a situação NÃO restringe
        Assert.Equal(["Zmeus3 admissão"], Titulos(soParaResponder));
        Assert.Equal((1, 0, 1, 0), Contagens(outraBusca)); // a busca restringe
    }

    [Fact]
    public async Task A_Sugestao_fica_sempre_disponivel_mesmo_depois_de_a_pessoa_mandar_e_nunca_vai_para_Respondidos()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        var sugestao = await SemearAsync(api, "Zmeus4 sugestões", "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, sugestao, "Sua ideia");
        await EscolherPessoasAsync(api, sugestao, eu);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {sugestao}, {eu}, 'Uma ideia', '2026-10-05 10:00:00', 0)");

        var todos = await ListarAsync(cliente, "busca=Zmeus4");
        var respondidos = await ListarAsync(cliente, "busca=Zmeus4&situacao=RESPONDIDO");

        var item = Item(todos, "Zmeus4 sugestões");
        Assert.Equal(("DISPONIVEL", 0, 1, false, "null"), (item.GetProperty("situacao").GetString(), item.GetProperty("respondidos").GetInt32(), item.GetProperty("total").GetInt32(), item.GetProperty("podeVerMinhasRespostas").GetBoolean(), Nulo(item, "respondidoEmUtc")));
        Assert.Empty(Titulos(respondidos));
    }

    [Fact]
    public async Task No_anonimo_o_respondido_vem_sem_data_e_sem_Ver_minhas_respostas()
    {
        var api = await ApiAsync();
        var (cliente, eu, setor) = await PessoaDoSetorAsync(api);
        var colegas = new List<int> { eu };
        for (var i = 0; i < 4; i++)
        {
            colegas.Add((await AdvertenciasDeTeste.NovoMembroAsync(api, setor)).Id);
        }

        var id = await SemearAsync(api, "Zmeus5 anônimo", "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        await EscolherPessoasAsync(api, id, [.. colegas]);
        var antes = await ListarAsync(cliente, "busca=Zmeus5");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid():D}', {id}, {eu})");

        var depois = await ListarAsync(cliente, "busca=Zmeus5&situacao=RESPONDIDO");

        Assert.Equal("DISPONIVEL", Item(antes, "Zmeus5 anônimo").GetProperty("situacao").GetString());
        var item = Item(depois, "Zmeus5 anônimo");
        Assert.Equal(("RESPONDIDO", "null", false, true), (item.GetProperty("situacao").GetString(), Nulo(item, "respondidoEmUtc"), item.GetProperty("podeVerMinhasRespostas").GetBoolean(), item.GetProperty("anonimo").GetBoolean()));
    }

    [Fact]
    public async Task Na_avaliacao_da_equipe_o_total_e_o_numero_de_pessoas_a_avaliar_e_continua_para_responder_ate_avaliar_todas()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var m3 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var id = await SemearAsync(api, "Zmeus6 avaliação da equipe", "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var pergunta = await PerguntaAsync(api, id, "Nota");
        await EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);
        async Task AvaliarAsync(int avaliado) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {id}, {setor.GestorFuncionarioId}, {avaliado}, '4', '2026-10-05 10:00:00', 0)");

        await AvaliarAsync(m1.Id);
        var parcial = Item(await ListarAsync(gestor, "busca=Zmeus6"), "Zmeus6 avaliação da equipe");
        await AvaliarAsync(m2.Id);
        await AvaliarAsync(m3.Id);
        var completa = await ListarAsync(gestor, "busca=Zmeus6&situacao=RESPONDIDO");

        Assert.Equal(("PARA_RESPONDER", 1, 3, "GESTOR_AVALIA_EQUIPE"), (parcial.GetProperty("situacao").GetString(), parcial.GetProperty("respondidos").GetInt32(), parcial.GetProperty("total").GetInt32(), parcial.GetProperty("sentidoAvaliacao").GetString()));
        var feita = Item(completa, "Zmeus6 avaliação da equipe");
        Assert.Equal(("RESPONDIDO", 3, 3), (feita.GetProperty("situacao").GetString(), feita.GetProperty("respondidos").GetInt32(), feita.GetProperty("total").GetInt32()));
    }

    [Fact]
    public async Task Na_avaliacao_do_gestor_cada_pessoa_da_equipe_ve_o_cartao_com_total_1_e_o_gestor_nao_ve_o_proprio()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, "Zmeus7 avaliação do gestor", "PUBLICADO", tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        await PerguntaAsync(api, id, "Nota");
        await EscolherSetorAsync(api, id, setor.SetorId);

        var doMembro = await ListarAsync(await AdvertenciasDeTeste.ClienteDeAsync(api, membro.Id), "busca=Zmeus7");
        var doGestor = await ListarAsync(await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId), "busca=Zmeus7");

        var item = Item(doMembro, "Zmeus7 avaliação do gestor");
        Assert.Equal(("PARA_RESPONDER", 0, 1), (item.GetProperty("situacao").GetString(), item.GetProperty("respondidos").GetInt32(), item.GetProperty("total").GetInt32()));
        Assert.Empty(Titulos(doGestor)); // o gestor não avalia a si mesmo: não é dele
    }

    [Fact]
    public async Task Pessoa_de_outro_setor_e_quem_ainda_nao_comecou_a_trabalhar_nao_veem_o_formulario_de_setor()
    {
        var api = await ApiAsync();
        var (_, _, setor) = await PessoaDoSetorAsync(api);
        var deFora = await AdvertenciasDeTeste.ClienteDeAsync(api, (await AdvertenciasDeTeste.NovoMembroAsync(api, await AdvertenciasDeTeste.NovoSetorAsync(api))).Id);
        var futuro = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Admitido amanhã"), RHSystem.Domain.Autenticacao.Perfil.Funcionario, setor.SetorId, true, new DateOnly(2026, 10, 8));
        var doFuturo = await AdvertenciasDeTeste.ClienteDeAsync(api, futuro);
        var id = await SemearAsync(api, "Zmeus8 do setor", "PUBLICADO", alvo: "SETORES");
        await EscolherSetorAsync(api, id, setor.SetorId);

        Assert.Empty(Titulos(await ListarAsync(deFora, "busca=Zmeus8")));
        Assert.Empty(Titulos(await ListarAsync(doFuturo, "busca=Zmeus8")));
    }

    [Fact]
    public async Task A_paginacao_e_os_parametros_invalidos_e_o_gestor_e_o_RH_tambem_respondem()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        for (var i = 1; i <= 3; i++)
        {
            var id = await SemearAsync(api, $"Zmeus9 item {i}", "PUBLICADO", alvo: "PESSOAS", prazo: $"2026-10-{10 + i}");
            await EscolherPessoasAsync(api, id, eu);
        }

        var segunda = await ListarAsync(cliente, "busca=Zmeus9&tamanhoPagina=2&pagina=2");
        var invalidos = new List<int>();
        foreach (var consulta in new[] { "situacao=ABERTO", "tipo=QUIZ", "pagina=0", "tamanhoPagina=101" })
        {
            invalidos.Add((int)(await cliente.GetAsync($"{Rota}?{consulta}")).StatusCode);
        }

        Assert.Equal((3, 2, 2, 2), (segunda.GetProperty("total").GetInt32(), segunda.GetProperty("totalPaginas").GetInt32(), segunda.GetProperty("pagina").GetInt32(), segunda.GetProperty("tamanhoPagina").GetInt32()));
        Assert.Single(Titulos(segunda)); // a terceira (a de prazo mais longe)
        Assert.All(invalidos, status => Assert.Equal(400, status));
        Assert.Equal(200, (int)(await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota)).StatusCode);
        Assert.Equal(200, (int)(await api.Api.ClienteComo(api.Elenco.Rh).GetAsync(Rota)).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(Rota)).StatusCode);
    }

    // ---------------------------------------------------------------- a bolinha do menu

    [Fact]
    public async Task A_bolinha_Formularios_do_menu_e_o_contagens_paraResponder_da_lista()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        var admissao = await SemearAsync(api, "Zmeus10 admissão", "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        await SemearAsync(api, "Zmeus10 avaliação", "PUBLICADO", tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR"); // publicada, mas sem setor escolhido: não chega a ninguém
        var pesquisa = await SemearAsync(api, "Zmeus10 pesquisa", "PUBLICADO", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, admissao, eu);
        await EscolherPessoasAsync(api, pesquisa, eu);
        var pessoa = await ListarAsync(cliente, "busca=Zmeus10");

        var menu = await CorpoAsync(await cliente.GetAsync("/api/eu/contadores-do-menu"));

        Assert.Equal(1, pessoa.GetProperty("contagens").GetProperty("paraResponder").GetInt32()); // a admissão; a pesquisa é só "disponível" e a avaliação é de outro setor
        Assert.Equal(await ContarMeusParaResponderAsync(cliente), menu.GetProperty("formulariosPendentes").GetInt32());
    }

    private static async Task<int> ContarMeusParaResponderAsync(HttpClient cliente) => (await ListarAsync(cliente)).GetProperty("contagens").GetProperty("paraResponder").GetInt32();

    // ---------------------------------------------------------------- abrir para responder

    [Fact]
    public async Task Abrir_devolve_as_perguntas_em_ordem_com_as_alternativas_o_prazo_e_os_minutos_e_nao_traz_avaliados_fora_da_avaliacao()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        var id = await SemearAsync(api, Titulo("Pesquisa para responder"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-31", descricao: "Conte como foi.");
        await PerguntaAsync(api, id, "Segunda", "MULTIPLA_ESCOLHA", 2, true, "Sim", "Não");
        await PerguntaAsync(api, id, "Primeira", "ESCALA", 1, true);
        await EscolherPessoasAsync(api, id, eu);

        var resposta = await cliente.GetAsync($"{Rota}/{id}");
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterFormularioParaResponder");
        Assert.Equal(["Primeira", "Segunda"], formulario.GetProperty("perguntas").EnumerateArray().Select(p => p.GetProperty("texto").GetString()));
        Assert.Equal(["Sim", "Não"], formulario.GetProperty("perguntas")[1].GetProperty("alternativas").EnumerateArray().Select(a => a.GetProperty("texto").GetString()));
        Assert.Equal(("Conte como foi.", "2026-10-31", "PESQUISA", 0, false), (formulario.GetProperty("descricao").GetString(), formulario.GetProperty("prazo").GetString(), formulario.GetProperty("tipo").GetString(), formulario.GetProperty("avaliados").GetArrayLength(), formulario.GetProperty("anonimo").GetBoolean()));
        Assert.True(formulario.GetProperty("minutosEstimados").GetInt32() >= 1);
        Assert.False(formulario.TryGetProperty("publicacao", out _)); // nada do RH: nem público, nem token
    }

    [Fact]
    public async Task Abrir_o_que_a_pessoa_nao_deve_responder_rascunho_por_link_e_inexistente_e_404_sem_dizer_qual_dos_casos()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        var outro = (await AdvertenciasDeTeste.NovoMembroAsync(api, await AdvertenciasDeTeste.NovoSetorAsync(api))).Id;
        var deOutro = await SemearAsync(api, Titulo("De outro"), "PUBLICADO", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, deOutro, outro);
        var rascunho = await SemearAsync(api, Titulo("Rascunho"), alvo: "PESSOAS");
        await EscolherPessoasAsync(api, rascunho, eu);
        var porLink = await SemearAsync(api, Titulo("Por link"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('y', 43));

        var respostas = new[] { await cliente.GetAsync($"{Rota}/{deOutro}"), await cliente.GetAsync($"{Rota}/{rascunho}"), await cliente.GetAsync($"{Rota}/{porLink}"), await cliente.GetAsync($"{Rota}/2147483000") };

        Assert.All(respostas, r => Assert.Equal(404, (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("obterFormularioParaResponder");
    }

    [Fact]
    public async Task Abrir_o_agendado_e_422_FORMULARIO_AINDA_NAO_ABERTO_e_o_encerrado_inclusive_o_prazo_vencido_e_422_FORMULARIO_ENCERRADO()
    {
        var api = await ApiAsync();
        var (cliente, eu, _) = await PessoaDoSetorAsync(api);
        var agendado = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");
        var vencido = await SemearAsync(api, Titulo("Prazo vencido"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");
        foreach (var id in new[] { agendado, encerrado, vencido })
        {
            await EscolherPessoasAsync(api, id, eu);
        }

        var a = await cliente.GetAsync($"{Rota}/{agendado}");
        var b = await cliente.GetAsync($"{Rota}/{encerrado}");
        var c = await cliente.GetAsync($"{Rota}/{vencido}");

        Assert.Equal((422, "FORMULARIO_AINDA_NAO_ABERTO", 422, "FORMULARIO_ENCERRADO", 422, "FORMULARIO_ENCERRADO"), ((int)a.StatusCode, Codigo(await CorpoAsync(a)), (int)b.StatusCode, Codigo(await CorpoAsync(b)), (int)c.StatusCode, Codigo(await CorpoAsync(c))));
        Assert.Equal("Este formulário ainda não está aberto. Tente de novo a partir da data de publicação.", (await CorpoAsync(a)).GetProperty("detail").GetString());
        Assert.Equal("Este formulário não está mais aceitando respostas.", (await CorpoAsync(b)).GetProperty("detail").GetString());
        await a.AfirmarConformeAoContratoAsync("obterFormularioParaResponder");
    }

    [Fact]
    public async Task A_Sugestao_abre_sempre_e_a_avaliacao_traz_os_avaliados_com_o_respondido_primeiro_os_que_faltam_depois_os_feitos()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var ana = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "Ana Avaliada");
        var bia = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "Bia Avaliada");
        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var avaliacao = await SemearAsync(api, Titulo("Avaliação"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var pergunta = await PerguntaAsync(api, avaliacao, "Nota");
        await EscolherPessoasAsync(api, avaliacao, setor.GestorFuncionarioId);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {avaliacao}, {setor.GestorFuncionarioId}, {ana.Id}, '5', '2026-10-05 10:00:00', 0)");
        var sugestao = await SemearAsync(api, Titulo("Sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, sugestao, setor.GestorFuncionarioId);

        var daAvaliacao = await CorpoAsync(await gestor.GetAsync($"{Rota}/{avaliacao}"));
        var daSugestao = await gestor.GetAsync($"{Rota}/{sugestao}");

        Assert.Equal(200, (int)daSugestao.StatusCode);
        Assert.Equal([(bia.Id, false), (ana.Id, true)], daAvaliacao.GetProperty("avaliados").EnumerateArray().Select(a => (a.GetProperty("funcionario").GetProperty("id").GetInt32(), a.GetProperty("respondido").GetBoolean())));
        Assert.Equal("GESTOR_AVALIA_EQUIPE", daAvaliacao.GetProperty("sentidoAvaliacao").GetString());
    }

    [Fact]
    public async Task Na_avaliacao_do_gestor_o_avaliado_e_uma_so_pessoa_o_gestor_do_setor()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("Avaliação do gestor"), "PUBLICADO", tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        await PerguntaAsync(api, id, "Nota");
        await EscolherSetorAsync(api, id, setor.SetorId);

        var formulario = await CorpoAsync(await (await AdvertenciasDeTeste.ClienteDeAsync(api, membro.Id)).GetAsync($"{Rota}/{id}"));

        var avaliados = formulario.GetProperty("avaliados");
        Assert.Equal(1, avaliados.GetArrayLength());
        Assert.Equal((setor.GestorFuncionarioId, false), (avaliados[0].GetProperty("funcionario").GetProperty("id").GetInt32(), avaliados[0].GetProperty("respondido").GetBoolean()));
    }
}
