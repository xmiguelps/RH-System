using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// "Quem respondeu" e "ver respostas" do RH (<c>listarRespondentesFormulario</c>, <c>obterRespostasDeFuncionario</c> e <c>baixarArquivoDeResposta</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. A lista sai de quem DEVE responder (calculado na hora) mais quem respondeu; o resumo não muda com os filtros; a avaliação tem uma linha por avaliação e
/// a Sugestão uma por envio; o anônimo não tem lista e só mostra números depois de ENCERRADO DE VERDADE; o RH vê as respostas de uma pessoa pelas PERGUNTAS (a opcional em branco vem vazia) e o arquivo só com o nome; o link do arquivo é temporário e grava o evento BAIXOU sem o nome do arquivo.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class QuemRespondeuTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(QuemRespondeuTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Rota(int formulario, string consulta = "") => $"/api/rh/formularios/{formulario}/respondentes{(consulta.Length == 0 ? "" : "?" + consulta)}";

    private static async Task<JsonElement> ListarAsync(ApiDeAcesso api, int formulario, string consulta = "") => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(formulario, consulta)));

    private static List<string> Nomes(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("nome").GetString()!)];

    private static (int?, int?, int?, int?) Resumo(JsonElement lista)
    {
        static int? Inteiro(JsonElement e, string campo) => e.GetProperty(campo).ValueKind == JsonValueKind.Null ? null : e.GetProperty(campo).GetInt32();
        var r = lista.GetProperty("resumo");
        return (Inteiro(r, "respondidos"), Inteiro(r, "faltam"), Inteiro(r, "esperados"), Inteiro(r, "taxaDeRespostaPercentual"));
    }

    private static Task RespostaAsync(ApiDeAcesso api, int formulario, int pergunta, int pessoa, string instante, string texto = "Foi bom.", int? avaliado = null) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {formulario}, {pessoa}, {(avaliado is null ? "NULL" : avaliado.Value.ToString())}, {Valor(texto)}, '{instante}', 0)");

    private static Task EncerrarAsync(ApiDeAcesso api, int formulario) => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {formulario}");

    private static async Task<int> NovoSetorAsync(ApiDeAcesso api, string prefixo) => await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo(prefixo));

    private static async Task<int> PessoaDoSetorAsync(ApiDeAcesso api, string nome, int setor, bool ativa = true) => await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario, setor, ativa);

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_traz_quem_respondeu_e_quem_falta_por_nome_com_o_resumo_que_nao_muda_com_os_filtros()
    {
        var api = await ApiAsync();
        var vendas = await NovoSetorAsync(api, "ZQr vendas");
        var compras = await NovoSetorAsync(api, "ZQr compras");
        var carla = await PessoaDoSetorAsync(api, "ZQr1 Carla", vendas);
        var ana = await PessoaDoSetorAsync(api, "ZQr1 Ana", compras);
        var bruno = await PessoaDoSetorAsync(api, "ZQr1 Bruno", vendas);
        var id = await SemearAsync(api, Titulo("ZQr1"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, carla, ana, bruno);
        await RespostaAsync(api, id, pergunta, bruno, "2026-10-05 10:00:00");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(id));
        var tudo = await CorpoAsync(resposta);
        var responderam = await ListarAsync(api, id, "situacao=RESPONDIDO");
        var faltam = await ListarAsync(api, id, "situacao=NAO_RESPONDEU");
        var doSetor = await ListarAsync(api, id, $"setorId={vendas}");
        var buscaAcento = await ListarAsync(api, id, "busca=ZQR1 BRUNO");
        var pagina2 = await ListarAsync(api, id, "tamanhoPagina=2&pagina=2");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarRespondentesFormulario");
        Assert.Equal(["ZQr1 Ana", "ZQr1 Bruno", "ZQr1 Carla"], Nomes(tudo));
        Assert.Equal((1, 2, 3, 33), Resumo(tudo));
        Assert.Equal((false, 1, 1, 1), (tudo.GetProperty("anonimo").GetBoolean(), tudo.GetProperty("pagina").GetInt32(), tudo.GetProperty("total").GetInt32() / 3, tudo.GetProperty("totalPaginas").GetInt32()));
        var bruno1 = tudo.GetProperty("itens")[1];
        Assert.Equal(("RESPONDIDO", "2026-10-05T13:00:00Z", true, JsonValueKind.Null), (bruno1.GetProperty("situacao").GetString(), bruno1.GetProperty("respondidoEmUtc").GetString(), bruno1.GetProperty("podeVerRespostas").GetBoolean(), bruno1.GetProperty("avaliado").ValueKind));
        var ana1 = tudo.GetProperty("itens")[0];
        Assert.Equal(("NAO_RESPONDEU", JsonValueKind.Null, false), (ana1.GetProperty("situacao").GetString(), ana1.GetProperty("respondidoEmUtc").ValueKind, ana1.GetProperty("podeVerRespostas").GetBoolean()));
        Assert.Equal(["ZQr1 Bruno"], Nomes(responderam));
        Assert.Equal(["ZQr1 Ana", "ZQr1 Carla"], Nomes(faltam));
        Assert.Equal(["ZQr1 Bruno", "ZQr1 Carla"], Nomes(doSetor));
        Assert.Equal(["ZQr1 Bruno"], Nomes(buscaAcento));
        Assert.Equal(["ZQr1 Carla"], Nomes(pagina2));
        Assert.Equal((2, 2), (pagina2.GetProperty("totalPaginas").GetInt32(), pagina2.GetProperty("total").GetInt32() - 1));
        foreach (var filtrada in new[] { responderam, faltam, doSetor, buscaAcento, pagina2 })
        {
            Assert.Equal((1, 2, 3, 33), Resumo(filtrada)); // o resumo não muda com os filtros
        }
    }

    [Fact]
    public async Task Quem_respondeu_e_saiu_do_publico_continua_na_lista_como_respondeu_e_fica_fora_das_contas_do_resumo()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api, "ZQr2");
        var fica = await PessoaDoSetorAsync(api, "ZQr2 Fica", setor);
        var saiu = await PessoaDoSetorAsync(api, "ZQr2 Saiu", setor);
        var id = await SemearAsync(api, Titulo("ZQr2"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, fica, saiu);
        await RespostaAsync(api, id, pergunta, saiu, "2026-10-05 10:00:00");
        await AdvertenciasDeTeste.InativarAsync(api, saiu); // deixa de ser do público

        var lista = await ListarAsync(api, id);

        Assert.Equal(["ZQr2 Fica", "ZQr2 Saiu"], Nomes(lista));
        Assert.Equal("RESPONDIDO", lista.GetProperty("itens")[1].GetProperty("situacao").GetString());
        Assert.Equal((0, 1, 1, 0), Resumo(lista)); // esperada só quem fica
        Assert.False(lista.GetProperty("itens")[1].GetProperty("funcionario").GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task Na_avaliacao_cada_linha_e_uma_avaliacao_com_o_avaliado()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "ZQr3 Membro B");
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "ZQr3 Membro A");
        var id = await SemearAsync(api, Titulo("ZQr3"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);
        await RespostaAsync(api, id, nota, setor.GestorFuncionarioId, "2026-10-05 10:00:00", "4", m1.Id);

        var lista = await ListarAsync(api, id);

        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal(2, itens.Count);
        Assert.Equal([m2.Nome, m1.Nome], itens.Select(i => i.GetProperty("avaliado").GetProperty("nome").GetString())); // o mesmo avaliador: ordem pelo nome do avaliado
        Assert.Equal(["NAO_RESPONDEU", "RESPONDIDO"], itens.Select(i => i.GetProperty("situacao").GetString()));
        Assert.All(itens, i => Assert.Equal(setor.GestorFuncionarioId, i.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Equal((1, 1, 2, 50), Resumo(lista));
    }

    [Fact]
    public async Task Na_Sugestao_cada_envio_e_uma_linha_o_mais_recente_primeiro_e_so_os_respondidos_contam()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api, "ZQr4");
        var bruno = await PessoaDoSetorAsync(api, "ZQr4 Bruno", setor);
        var vanessa = await PessoaDoSetorAsync(api, "ZQr4 Vanessa", setor);
        var id = await SemearAsync(api, Titulo("ZQr4 sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        var ideia = await PerguntaAsync(api, id, "Sua ideia", "RESPOSTA_LONGA", 1, true);
        await EscolherPessoasAsync(api, id, bruno, vanessa);
        await RespostaAsync(api, id, ideia, bruno, "2026-10-02 08:05:09", "Primeira");
        await RespostaAsync(api, id, ideia, vanessa, "2026-10-05 14:48:30", "Da Vanessa");
        await RespostaAsync(api, id, ideia, bruno, "2026-10-06 11:20:41", "Segunda");

        var lista = await ListarAsync(api, id);

        Assert.Equal(["ZQr4 Bruno", "ZQr4 Bruno", "ZQr4 Vanessa"], Nomes(lista));
        Assert.Equal(["2026-10-06T14:20:41Z", "2026-10-02T11:05:09Z", "2026-10-05T17:48:30Z"], lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("respondidoEmUtc").GetString()));
        Assert.Equal((3, (int?)null, (int?)null, (int?)null), Resumo(lista));
        Assert.All(lista.GetProperty("itens").EnumerateArray(), i => Assert.True(i.GetProperty("podeVerRespostas").GetBoolean()));
    }

    // ---------------------------------------------------------------- o anônimo

    private static async Task<(int Id, int Pergunta)> AnonimoAsync(ApiDeAcesso api, string prefixo, int pessoas, string situacao = "PUBLICADO", string? prazo = null)
    {
        var setor = await NovoSetorAsync(api, prefixo);
        var id = await SemearAsync(api, Titulo(prefixo), situacao, anonimo: true, alvo: "PESSOAS", prazo: prazo);
        var pergunta = await PerguntaAsync(api, id, "O que acha?");
        var ids = new List<int>();
        for (var i = 0; i < pessoas; i++)
        {
            ids.Add(await PessoaDoSetorAsync(api, $"{prefixo} Pessoa {i}", setor));
        }

        await EscolherPessoasAsync(api, id, [.. ids]);
        return (id, pergunta);
    }

    private static Task ParticiparAsync(ApiDeAcesso api, int formulario, int pergunta, int pessoa) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid()}', {formulario}, {pessoa}); INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {formulario}, 'ok', '2026-10-01 00:00:00', 1)");

    [Fact]
    public async Task O_anonimo_nao_tem_lista_ignora_os_filtros_e_antes_de_encerrar_so_mostra_os_esperados()
    {
        var api = await ApiAsync();
        var (id, pergunta) = await AnonimoAsync(api, "ZQr5", 3);
        var pessoas = (await Sql.ColunaAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} ORDER BY ID_FUNCIONARIO")).Select(int.Parse).ToList();
        await ParticiparAsync(api, id, pergunta, pessoas[0]);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(id, "busca=ZQr5&situacao=RESPONDIDO&setorId=999999&pagina=3&tamanhoPagina=5"));
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarRespondentesFormulario");
        Assert.Equal((true, 0, 0, 0, 3, 5), (lista.GetProperty("anonimo").GetBoolean(), lista.GetProperty("itens").GetArrayLength(), lista.GetProperty("total").GetInt32(), lista.GetProperty("totalPaginas").GetInt32(), Resumo(lista).Item3, lista.GetProperty("tamanhoPagina").GetInt32()));
        Assert.Equal((null, null, 3, null), Resumo(lista)); // sem contagem, nem faixa
    }

    [Fact]
    public async Task O_anonimo_encerrado_mostra_os_numeros_exatos_e_o_prazo_vencido_com_a_tarefa_parada_ja_conta_como_encerrado()
    {
        var api = await ApiAsync();
        var (encerrado, p1) = await AnonimoAsync(api, "ZQr6", 3);
        var (vencido, p2) = await AnonimoAsync(api, "ZQr6b", 4, prazo: "2026-10-06");
        foreach (var (formulario, pergunta, quantas) in new[] { (encerrado, p1, 2), (vencido, p2, 1) })
        {
            var pessoas = (await Sql.ColunaAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {formulario} ORDER BY ID_FUNCIONARIO")).Select(int.Parse).Take(quantas).ToList();
            foreach (var pessoa in pessoas)
            {
                await ParticiparAsync(api, formulario, pergunta, pessoa);
            }
        }

        await EncerrarAsync(api, encerrado);

        var doEncerrado = await ListarAsync(api, encerrado);
        var doVencido = await ListarAsync(api, vencido);

        Assert.Equal((2, 1, 3, 67), Resumo(doEncerrado));
        Assert.Equal((1, 3, 4, 25), Resumo(doVencido)); // o prazo de ontem vale como encerrado mesmo com o formulário ainda ativo no banco
        Assert.Equal((0, 0), (doEncerrado.GetProperty("itens").GetArrayLength(), doVencido.GetProperty("total").GetInt32()));
    }

    // ---------------------------------------------------------------- recusas

    [Fact]
    public async Task Por_link_e_422_inexistente_e_404_filtro_invalido_e_400_e_so_o_RH_acessa()
    {
        var api = await ApiAsync();
        var porLink = await SemearAsync(api, Titulo("ZQr7 entrevista"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('r', 43));
        var comum = await SemearAsync(api, Titulo("ZQr7"), "PUBLICADO", alvo: "PESSOAS");
        var rh = await api.ClienteDoRhAgoraAsync();

        var link = await rh.GetAsync(Rota(porLink));
        var inexistente = await rh.GetAsync(Rota(99999999));
        var invalidos = new List<int>();
        foreach (var consulta in new[] { "situacao=ABERTO", "setorId=0", "pagina=0", "tamanhoPagina=101" })
        {
            invalidos.Add((int)(await rh.GetAsync(Rota(comum, consulta))).StatusCode);
        }

        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota(comum));
        var funcionario = await api.Api.ClienteComo(api.Elenco.MembroA).GetAsync(Rota(comum));
        var semLogin = await api.Api.ClienteAnonimo().GetAsync(Rota(comum));

        Assert.Equal(422, (int)link.StatusCode);
        Assert.Equal("FORMULARIO_POR_LINK", Codigo(await CorpoAsync(link)));
        Assert.Equal(404, (int)inexistente.StatusCode);
        Assert.All(invalidos, status => Assert.Equal(400, status));
        Assert.Equal([403, 403, 401], new[] { gestor, funcionario, semLogin }.Select(r => (int)r.StatusCode));
    }

    [Fact]
    public async Task Rascunho_sem_publico_devolve_lista_vazia_e_resumo_sem_esperados()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZQr8 rascunho"));

        var lista = await ListarAsync(api, id);

        Assert.Equal((0, 0), (lista.GetProperty("itens").GetArrayLength(), lista.GetProperty("total").GetInt32()));
        Assert.Equal((0, null, null, null), Resumo(lista));
    }

    // ---------------------------------------------------------------- ver respostas

    private sealed record Completo(int Id, int Curta, int Longa, int Multipla, int Escala, int Anexo, int Casado);

    private static async Task<Completo> CompletoAsync(ApiDeAcesso api, string prefixo, params int[] pessoas)
    {
        var id = await SemearAsync(api, Titulo(prefixo), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var curta = await PerguntaAsync(api, id, "Seu nome?", "RESPOSTA_CURTA", 1, true);
        var longa = await PerguntaAsync(api, id, "Comentários", "RESPOSTA_LONGA", 2);
        var multipla = await PerguntaAsync(api, id, "Estado civil", "MULTIPLA_ESCOLHA", 3, true, "Solteiro", "Casado");
        var escala = await PerguntaAsync(api, id, "Nota", "ESCALA", 4, true);
        var anexo = await PerguntaAsync(api, id, "Comprovante", "ARQUIVO", 5);
        if (pessoas.Length > 0)
        {
            await EscolherPessoasAsync(api, id, pessoas);
        }

        var casado = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {multipla} AND ORDEM = 2");
        return new Completo(id, curta, longa, multipla, escala, anexo, casado);
    }

    private static async Task ResponderCompletoAsync(HttpClient cliente, Completo c) =>
        Assert.Equal(201, (int)(await ResponderAsync(cliente, c.Id, Envio(Json(new { perguntaId = c.Curta, texto = "Ana" }, new { perguntaId = c.Multipla, alternativaIds = new[] { c.Casado } }, new { perguntaId = c.Escala, valorEscala = 5 }, new { perguntaId = c.Anexo, arquivoIndice = 0 }), null, ("residencia.pdf", ArquivosDeTeste.Pdf())))).StatusCode);

    [Fact]
    public async Task O_RH_ve_as_respostas_de_uma_pessoa_pelas_perguntas_com_o_arquivo_so_pelo_nome()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var c = await CompletoAsync(api, "ZQr9", eu);
        await ResponderCompletoAsync(cliente, c);
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await rh.GetAsync($"/api/rh/formularios/{c.Id}/respostas/funcionarios/{eu}");
        var corpo = await CorpoAsync(resposta);
        var itens = corpo.GetProperty("respostas").EnumerateArray().ToList();

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterRespostasDeFuncionario");
        Assert.Equal((c.Id, eu, JsonValueKind.Null, "2026-10-07T12:42:00Z"), (corpo.GetProperty("formularioId").GetInt32(), corpo.GetProperty("funcionario").GetProperty("id").GetInt32(), corpo.GetProperty("avaliado").ValueKind, corpo.GetProperty("respondidoEmUtc").GetString()));
        Assert.Equal([c.Curta, c.Longa, c.Multipla, c.Escala, c.Anexo], itens.Select(i => i.GetProperty("perguntaId").GetInt32()));
        Assert.Equal("Ana", itens[0].GetProperty("texto").GetString());
        Assert.Equal((JsonValueKind.Null, 0), (itens[1].GetProperty("texto").ValueKind, itens[1].GetProperty("alternativas").GetArrayLength())); // a opcional em branco
        Assert.Equal("Casado", itens[2].GetProperty("alternativas")[0].GetProperty("texto").GetString());
        Assert.Equal(5, itens[3].GetProperty("valorEscala").GetInt32());
        Assert.Equal("residencia.pdf", itens[4].GetProperty("arquivo").GetProperty("nome").GetString());
        Assert.DoesNotContain("formularios/", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ver_respostas_e_404_se_a_pessoa_nao_respondeu_ou_respondeu_outro_formulario_e_422_no_anonimo_e_400_com_parametro_fora_do_tipo()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var (_, outro) = await PessoaAsync(api);
        var c = await CompletoAsync(api, "ZQr10", eu, outro);
        var outroFormulario = await CompletoAsync(api, "ZQr10b", eu, outro);
        await ResponderCompletoAsync(cliente, c);
        var (anonimo, _) = await AnonimoAsync(api, "ZQr10c", 1);
        var rh = await api.ClienteDoRhAgoraAsync();

        var quemNaoRespondeu = await rh.GetAsync($"/api/rh/formularios/{c.Id}/respostas/funcionarios/{outro}");
        var emOutroFormulario = await rh.GetAsync($"/api/rh/formularios/{outroFormulario.Id}/respostas/funcionarios/{eu}");
        var pessoaInexistente = await rh.GetAsync($"/api/rh/formularios/{c.Id}/respostas/funcionarios/99999999");
        var formularioInexistente = await rh.GetAsync($"/api/rh/formularios/99999999/respostas/funcionarios/{eu}");
        var doAnonimo = await rh.GetAsync($"/api/rh/formularios/{anonimo}/respostas/funcionarios/{eu}");
        var comAvaliado = await rh.GetAsync($"/api/rh/formularios/{c.Id}/respostas/funcionarios/{eu}?avaliadoId={outro}");
        var comInstante = await rh.GetAsync($"/api/rh/formularios/{c.Id}/respostas/funcionarios/{eu}?respondidoEmUtc=2026-10-07T12:42:00Z");
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync($"/api/rh/formularios/{c.Id}/respostas/funcionarios/{eu}");

        Assert.Equal([404, 404, 404, 404, 422, 400, 400, 403], new[] { quemNaoRespondeu, emOutroFormulario, pessoaInexistente, formularioInexistente, doAnonimo, comAvaliado, comInstante, gestor }.Select(r => (int)r.StatusCode));
        Assert.Equal("FORMULARIO_ANONIMO", Codigo(await CorpoAsync(doAnonimo)));
        Assert.True((await CorpoAsync(comAvaliado)).GetProperty("errors").TryGetProperty("avaliadoId", out _));
        Assert.True((await CorpoAsync(comInstante)).GetProperty("errors").TryGetProperty("respondidoEmUtc", out _));
    }

    [Fact]
    public async Task Na_avaliacao_o_RH_pede_o_avaliado_e_ve_so_aquela_avaliacao()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("ZQr11"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);
        await RespostaAsync(api, id, nota, setor.GestorFuncionarioId, "2026-10-05 10:00:00", "2", m1.Id);
        await RespostaAsync(api, id, nota, setor.GestorFuncionarioId, "2026-10-06 10:00:00", "5", m2.Id);
        var rh = await api.ClienteDoRhAgoraAsync();

        var semAvaliado = await rh.GetAsync($"/api/rh/formularios/{id}/respostas/funcionarios/{setor.GestorFuncionarioId}");
        var doSegundo = await CorpoAsync(await rh.GetAsync($"/api/rh/formularios/{id}/respostas/funcionarios/{setor.GestorFuncionarioId}?avaliadoId={m2.Id}"));
        var deQuemNaoFoiAvaliado = await rh.GetAsync($"/api/rh/formularios/{id}/respostas/funcionarios/{setor.GestorFuncionarioId}?avaliadoId={setor.GestorFuncionarioId}");

        Assert.Equal(400, (int)semAvaliado.StatusCode);
        Assert.Equal((m2.Id, 5, "2026-10-06T13:00:00Z"), (doSegundo.GetProperty("avaliado").GetProperty("id").GetInt32(), doSegundo.GetProperty("respostas")[0].GetProperty("valorEscala").GetInt32(), doSegundo.GetProperty("respondidoEmUtc").GetString()));
        Assert.Equal(404, (int)deQuemNaoFoiAvaliado.StatusCode);
    }

    [Fact]
    public async Task Na_Sugestao_o_instante_diz_qual_envio_se_quer_ver_e_sem_ele_e_400()
    {
        var api = await ApiAsync();
        var (_, eu) = await PessoaAsync(api);
        var id = await SemearAsync(api, Titulo("ZQr12 sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        var ideia = await PerguntaAsync(api, id, "Sua ideia", "RESPOSTA_LONGA", 1, true);
        await EscolherPessoasAsync(api, id, eu);
        await RespostaAsync(api, id, ideia, eu, "2026-10-02 08:05:09", "Primeira");
        await RespostaAsync(api, id, ideia, eu, "2026-10-06 11:20:41", "Segunda");
        var rh = await api.ClienteDoRhAgoraAsync();

        var semInstante = await rh.GetAsync($"/api/rh/formularios/{id}/respostas/funcionarios/{eu}");
        var primeira = await CorpoAsync(await rh.GetAsync($"/api/rh/formularios/{id}/respostas/funcionarios/{eu}?respondidoEmUtc=2026-10-02T11:05:09Z"));
        var segunda = await CorpoAsync(await rh.GetAsync($"/api/rh/formularios/{id}/respostas/funcionarios/{eu}?respondidoEmUtc=2026-10-06T14:20:41Z"));
        var semNenhuma = await rh.GetAsync($"/api/rh/formularios/{id}/respostas/funcionarios/{eu}?respondidoEmUtc=2026-10-03T00:00:00Z");

        Assert.Equal(400, (int)semInstante.StatusCode);
        Assert.Equal("Primeira", primeira.GetProperty("respostas")[0].GetProperty("texto").GetString());
        Assert.Equal(("Segunda", "2026-10-06T14:20:41Z"), (segunda.GetProperty("respostas")[0].GetProperty("texto").GetString(), segunda.GetProperty("respondidoEmUtc").GetString()));
        Assert.Equal(404, (int)semNenhuma.StatusCode);
    }

    // ---------------------------------------------------------------- o arquivo

    [Fact]
    public async Task O_RH_baixa_o_arquivo_por_um_link_temporario_com_o_evento_BAIXOU_sem_o_nome_do_arquivo()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var c = await CompletoAsync(api, "ZQr13", eu);
        await ResponderCompletoAsync(cliente, c);
        var respostaDoArquivo = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {c.Anexo}");
        var respostaDeTexto = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {c.Curta}");
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await rh.GetAsync($"/api/rh/formularios/{c.Id}/arquivos/{respostaDoArquivo}/download");
        var semArquivo = await rh.GetAsync($"/api/rh/formularios/{c.Id}/arquivos/{respostaDeTexto}/download");
        var deOutroFormulario = await rh.GetAsync($"/api/rh/formularios/{(await CompletoAsync(api, "ZQr13b")).Id}/arquivos/{respostaDoArquivo}/download");
        var inexistente = await rh.GetAsync($"/api/rh/formularios/{c.Id}/arquivos/99999999/download");
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync($"/api/rh/formularios/{c.Id}/arquivos/{respostaDoArquivo}/download");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("baixarArquivoDeResposta");
        var link = await CorpoAsync(resposta);
        Assert.Equal(["expiraEmUtc", "url"], link.EnumerateObject().Select(p => p.Name).Order()); // o corpo só tem o link e quando vence: o caminho do armazenamento não é um campo
        Assert.StartsWith("https://", link.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.Equal([404, 404, 404, 403], new[] { semArquivo, deOutroFormulario, inexistente, gestor }.Select(r => (int)r.StatusCode));
        var evento = Assert.Single(await EventosAsync(api, c.Id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Baixou), (evento.Area, evento.Acao));
        Assert.DoesNotContain("residencia", evento.Descricao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_formulario_anonimo_nao_ha_arquivo_para_baixar_e_e_404()
    {
        var api = await ApiAsync();
        var (id, pergunta) = await AnonimoAsync(api, "ZQr14", 1);
        var pessoa = int.Parse((await Sql.ColunaAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id}")).Single());
        await ParticiparAsync(api, id, pergunta, pessoa);
        var resposta = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}");

        var baixar = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{id}/arquivos/{resposta}/download");

        Assert.Equal(404, (int)baixar.StatusCode);
        Assert.Empty(await EventosAsync(api, id));
    }
}
