using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// As perguntas e as alternativas pelo RH (<c>adicionarPerguntaFormulario</c>, <c>editarPerguntaFormulario</c> e <c>excluirPerguntaFormulario</c>), no banco REAL. A ordem é única por formulário (a chave do banco confere linha a linha): mover e inserir passam por dois
/// passos, e a corrida de duas abas vira nova tentativa e depois 409, nunca 500. Pergunta respondida só muda <c>obrigatoria</c> e <c>ordem</c>; no anônimo publicado nada muda, com ou sem resposta (o mesmo 409). Nenhum evento leva o texto da pergunta.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PerguntasDoFormularioTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PerguntasDoFormularioTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    /// <summary>O corpo da pergunta SEM os campos opcionais vazios: o contrato não aceita <c>null</c> em <c>ordem</c> nem em <c>id</c>, então "sem valor" é o campo ausente.</summary>
    private static object Nova(string texto, string tipo = "RESPOSTA_CURTA", bool obrigatoria = false, int? ordem = null, params object[] alternativas)
    {
        var corpo = new Dictionary<string, object?> { ["texto"] = texto, ["tipo"] = tipo, ["obrigatoria"] = obrigatoria, ["alternativas"] = alternativas };
        if (ordem is not null)
        {
            corpo["ordem"] = ordem;
        }

        return corpo;
    }

    private static object Alt(string texto, int? id = null) => id is null ? new Dictionary<string, object?> { ["texto"] = texto } : new Dictionary<string, object?> { ["id"] = id, ["texto"] = texto };

    private static async Task<HttpResponseMessage> AdicionarAsync(ApiDeAcesso api, int formularioId, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{formularioId}/perguntas", corpo);

    private static async Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, int formularioId, int perguntaId, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{formularioId}/perguntas/{perguntaId}", corpo);

    private static async Task<HttpResponseMessage> ExcluirAsync(ApiDeAcesso api, int formularioId, int perguntaId) => await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{formularioId}/perguntas/{perguntaId}");

    /// <summary>As posições das perguntas do formulário, em ordem ("1,2,3").</summary>
    private static async Task<string?> OrdensAsync(ApiDeAcesso api, int formularioId) => await TextoAsync(api, $"SELECT GROUP_CONCAT(ORDEM ORDER BY ORDEM) FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {formularioId}");

    /// <summary>Os textos das perguntas na ordem do formulário ("A|B|C").</summary>
    private static async Task<string?> TextosAsync(ApiDeAcesso api, int formularioId) => await TextoAsync(api, $"SELECT GROUP_CONCAT(TEXTO_PERGUNTA ORDER BY ORDEM SEPARATOR '|') FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {formularioId}");

    private static async Task<string?> AlternativasAsync(ApiDeAcesso api, int perguntaId) =>
        await TextoAsync(api, $"SELECT GROUP_CONCAT(CONCAT(ORDEM, ':', TEXTO_ALTERNATIVA) ORDER BY ORDEM SEPARATOR '|') FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {perguntaId}");

    private static async Task<int> RascunhoComTresAsync(ApiDeAcesso api, bool anonimo = false)
    {
        var id = await SemearAsync(api, Titulo("Com três perguntas"), anonimo: anonimo);
        await PerguntaAsync(api, id, "A", ordem: 1);
        await PerguntaAsync(api, id, "B", ordem: 2);
        await PerguntaAsync(api, id, "C", ordem: 3);
        return id;
    }

    private static async Task<int> IdDaPerguntaAsync(ApiDeAcesso api, int formularioId, string texto) => (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_PERGUNTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {formularioId} AND TEXTO_PERGUNTA = {Valor(texto)}");

    // ---------------------------------------------------------------- adicionar

    [Fact]
    public async Task Adicionar_sem_ordem_vai_para_o_fim_e_devolve_a_pergunta_com_id_ordem_e_alternativas()
    {
        var api = await ApiAsync();
        var id = await RascunhoComTresAsync(api);

        var resposta = await AdicionarAsync(api, id, Nova("D", "MULTIPLA_ESCOLHA", true, null, Alt("Ótimo"), Alt("Bom"), Alt("Ruim")));
        var pergunta = await CorpoAsync(resposta);

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("adicionarPerguntaFormulario");
        Assert.Equal(("D", "MULTIPLA_ESCOLHA", true, 4), (pergunta.GetProperty("texto").GetString(), pergunta.GetProperty("tipo").GetString(), pergunta.GetProperty("obrigatoria").GetBoolean(), pergunta.GetProperty("ordem").GetInt32()));
        Assert.Equal(["Ótimo", "Bom", "Ruim"], pergunta.GetProperty("alternativas").EnumerateArray().Select(a => a.GetProperty("texto").GetString()));
        Assert.Equal([1, 2, 3], pergunta.GetProperty("alternativas").EnumerateArray().Select(a => a.GetProperty("ordem").GetInt32()));
        Assert.Equal(("1,2,3,4", "A|B|C|D", "1:Ótimo|2:Bom|3:Ruim"), (await OrdensAsync(api, id), await TextosAsync(api, id), await AlternativasAsync(api, pergunta.GetProperty("id").GetInt32())));
    }

    [Fact]
    public async Task Adicionar_o_primeiro_item_de_um_formulario_vazio_fica_na_posicao_1()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Vazio"));

        var pergunta = await CorpoAsync(await AdicionarAsync(api, id, Nova("Primeira")));

        Assert.Equal(1, pergunta.GetProperty("ordem").GetInt32());
        Assert.Equal("1", await OrdensAsync(api, id));
    }

    [Fact]
    public async Task Adicionar_com_ordem_entra_ali_e_as_seguintes_descem_sem_buraco()
    {
        var api = await ApiAsync();
        var id = await RascunhoComTresAsync(api);

        var resposta = await AdicionarAsync(api, id, Nova("Nova", ordem: 2));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(2, (await CorpoAsync(resposta)).GetProperty("ordem").GetInt32());
        Assert.Equal(("1,2,3,4", "A|Nova|B|C"), (await OrdensAsync(api, id), await TextosAsync(api, id)));
        var noInicio = await AdicionarAsync(api, id, Nova("No começo", ordem: 1));
        Assert.Equal((1, "1,2,3,4,5", "No começo|A|Nova|B|C"), ((await CorpoAsync(noInicio)).GetProperty("ordem").GetInt32(), await OrdensAsync(api, id), await TextosAsync(api, id)));
    }

    [Fact]
    public async Task Adicionar_com_ordem_alem_do_fim_vai_para_o_fim()
    {
        var api = await ApiAsync();
        var id = await RascunhoComTresAsync(api);

        var pergunta = await CorpoAsync(await AdicionarAsync(api, id, Nova("Longe", ordem: 99)));

        Assert.Equal(4, pergunta.GetProperty("ordem").GetInt32());
        Assert.Equal(("1,2,3,4", "A|B|C|Longe"), (await OrdensAsync(api, id), await TextosAsync(api, id)));
    }

    [Fact]
    public async Task Multipla_escolha_fora_de_2_a_10_alternativas_e_400_em_alternativas_e_os_outros_tipos_nao_levam_alternativas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Alternativas"));

        var uma = await AdicionarAsync(api, id, Nova("Uma só", "MULTIPLA_ESCOLHA", false, null, Alt("Única")));
        var onze = await AdicionarAsync(api, id, Nova("Onze", "MULTIPLA_ESCOLHA", false, null, [.. Enumerable.Range(1, 11).Select(i => Alt($"Opção {i}"))]));
        var curtaComAlternativa = await AdicionarAsync(api, id, Nova("Curta", "RESPOSTA_CURTA", false, null, Alt("A"), Alt("B")));
        var dez = await AdicionarAsync(api, id, Nova("Dez", "MULTIPLA_ESCOLHA", false, null, [.. Enumerable.Range(1, 10).Select(i => Alt($"Opção {i}"))]));

        Assert.Equal((400, 400, 400, 201), ((int)uma.StatusCode, (int)onze.StatusCode, (int)curtaComAlternativa.StatusCode, (int)dez.StatusCode));
        Assert.Equal(["A múltipla escolha precisa de 2 a 10 alternativas."], (await CorpoAsync(uma)).GetProperty("errors").GetProperty("alternativas").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["Só a múltipla escolha tem alternativas."], (await CorpoAsync(curtaComAlternativa)).GetProperty("errors").GetProperty("alternativas").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("Dez", await TextosAsync(api, id));
    }

    [Fact]
    public async Task Adicionar_com_o_corpo_invalido_devolve_os_erros_juntos_e_nao_grava()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Corpo inválido"));

        var resposta = await AdicionarAsync(api, id, Nova("", "MULTIPLA_ESCOLHA", false, 0, Alt("A")));

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Equal(["alternativas", "ordem", "texto"], (await CorpoAsync(resposta)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
        Assert.Null(await OrdensAsync(api, id));
        var semTipo = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{id}/perguntas", new { texto = "Sem tipo", obrigatoria = false });
        Assert.Equal(400, (int)semTipo.StatusCode);
    }

    [Fact]
    public async Task Ordem_e_id_de_alternativa_nao_aceitam_null_nem_texto_nem_fracao_porque_o_contrato_diz_inteiro()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Inteiros do corpo"));
        var cliente = await api.ClienteDoRhAgoraAsync();

        var ordemNula = await cliente.PostAsJsonAsync($"{Rota}/{id}/perguntas", new { texto = "P", tipo = "RESPOSTA_CURTA", obrigatoria = false, ordem = (int?)null });
        var ordemTexto = await cliente.PostAsJsonAsync($"{Rota}/{id}/perguntas", new { texto = "P", tipo = "RESPOSTA_CURTA", obrigatoria = false, ordem = "2" });
        var ordemFracao = await cliente.PostAsJsonAsync($"{Rota}/{id}/perguntas", new { texto = "P", tipo = "RESPOSTA_CURTA", obrigatoria = false, ordem = 1.5 });
        var idNulo = await cliente.PostAsJsonAsync($"{Rota}/{id}/perguntas", new { texto = "P", tipo = "MULTIPLA_ESCOLHA", obrigatoria = false, alternativas = new[] { new { id = (int?)null, texto = "A" }, new { id = (int?)null, texto = "B" } } });

        Assert.All(new[] { ordemNula, ordemTexto, ordemFracao, idNulo }, r => Assert.Equal(400, (int)r.StatusCode));
        Assert.Null(await OrdensAsync(api, id));
    }

    [Fact]
    public async Task Pergunta_de_arquivo_em_formulario_anonimo_e_422_ANONIMO_SEM_ARQUIVO()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Anônimo em rascunho"), anonimo: true);

        var resposta = await AdicionarAsync(api, id, Nova("Envie o comprovante", "ARQUIVO"));
        var problema = await CorpoAsync(resposta);

        Assert.Equal((422, "ANONIMO_SEM_ARQUIVO", "Formulário anônimo não pode ter pergunta de envio de arquivo."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("adicionarPerguntaFormulario");
        Assert.Null(await OrdensAsync(api, id));
        Assert.Equal(201, (int)(await AdicionarAsync(api, id, Nova("Texto vale"))).StatusCode);
    }

    [Fact]
    public async Task Com_nome_publicado_ja_respondido_aceita_pergunta_nova()
    {
        var api = await ApiAsync();
        var id = await PublicadoRespondidoAsync(api, Titulo("Publicado com nome"), anonimo: false);

        var resposta = await AdicionarAsync(api, id, Nova("Pergunta tardia", obrigatoria: false));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(("1,2", "Como foi?|Pergunta tardia"), (await OrdensAsync(api, id), await TextosAsync(api, id)));
    }

    [Fact]
    public async Task Duas_abas_acrescentando_ao_mesmo_tempo_nunca_dao_500_e_a_ordem_continua_sem_buraco()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Corrida de perguntas"));
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(1, 6).Select(i => cliente.PostAsJsonAsync($"{Rota}/{id}/perguntas", Nova($"Pergunta {i}"))));

        Assert.All(respostas, r => Assert.True((int)r.StatusCode is 201 or 409, $"status {(int)r.StatusCode}"));
        var gravadas = respostas.Count(r => (int)r.StatusCode == 201);
        Assert.True(gravadas >= 1);
        Assert.Equal(string.Join(',', Enumerable.Range(1, gravadas)), await OrdensAsync(api, id));
    }

    // ---------------------------------------------------------------- o anônimo publicado e o encerrado

    [Fact]
    public async Task Anonimo_publicado_nao_muda_pergunta_nenhuma_nem_obrigatoria_ou_ordem_e_o_409_e_o_mesmo_com_e_sem_resposta()
    {
        var api = await ApiAsync();
        var semResposta = await SemearAsync(api, Titulo("Anônimo sem resposta"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var perguntaSem = await PerguntaAsync(api, semResposta, "Pergunta fixa", ordem: 1);
        var comResposta = await PublicadoRespondidoAsync(api, Titulo("Anônimo com resposta"), anonimo: true);
        var perguntaCom = await IdDaPerguntaAsync(api, comResposta, "Como foi?");
        var veredito = new List<(int, string)>();

        foreach (var (formulario, pergunta) in new[] { (semResposta, perguntaSem), (comResposta, perguntaCom) })
        {
            var adicionar = await AdicionarAsync(api, formulario, Nova("Nova"));
            var editarTexto = await EditarAsync(api, formulario, pergunta, Nova("Outro texto"));
            var soObrigatoria = await EditarAsync(api, formulario, pergunta, Nova("Pergunta fixa", obrigatoria: true));
            var soOrdem = await EditarAsync(api, formulario, pergunta, Nova("Pergunta fixa", ordem: 1));
            var excluir = await ExcluirAsync(api, formulario, pergunta);
            foreach (var r in new[] { adicionar, editarTexto, soObrigatoria, soOrdem, excluir })
            {
                veredito.Add(((int)r.StatusCode, Codigo(await CorpoAsync(r))));
            }
        }

        Assert.Equal(10, veredito.Count);
        Assert.All(veredito, v => Assert.Equal((409, "ANONIMO_PERGUNTAS_FIXAS"), v));
        Assert.Equal("As perguntas de um formulário anônimo publicado não mudam.", (await CorpoAsync(await AdicionarAsync(api, semResposta, Nova("Nova")))).GetProperty("detail").GetString());
        Assert.Equal(("Pergunta fixa", "Como foi?"), (await TextosAsync(api, semResposta), await TextosAsync(api, comResposta)));
    }

    [Fact]
    public async Task Encerrado_e_422_FORMULARIO_ENCERRADO_em_adicionar_editar_e_excluir()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, id, "Pergunta antiga");

        var respostas = new[] { await AdicionarAsync(api, id, Nova("Nova")), await EditarAsync(api, id, pergunta, Nova("Mudou")), await ExcluirAsync(api, id, pergunta) };

        Assert.All(respostas, r => Assert.Equal(422, (int)r.StatusCode));
        foreach (var r in respostas)
        {
            Assert.Equal("FORMULARIO_ENCERRADO", Codigo(await CorpoAsync(r)));
        }
        Assert.Equal("Pergunta antiga", await TextosAsync(api, id));
        await respostas[0].AfirmarConformeAoContratoAsync("adicionarPerguntaFormulario");
    }

    [Fact]
    public async Task O_prazo_vencido_com_a_tarefa_parada_tambem_trava_as_perguntas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Venceu"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");

        var resposta = await AdicionarAsync(api, id, Nova("Tarde"));

        Assert.Equal((422, "FORMULARIO_ENCERRADO"), ((int)resposta.StatusCode, Codigo(await CorpoAsync(resposta))));
    }

    [Fact]
    public async Task Formulario_ou_pergunta_inexistente_e_404_e_pergunta_de_outro_formulario_tambem()
    {
        var api = await ApiAsync();
        var a = await SemearAsync(api, Titulo("Formulário A"));
        var b = await SemearAsync(api, Titulo("Formulário B"));
        var perguntaDeB = await PerguntaAsync(api, b, "De B");

        var respostas = new[]
        {
            await AdicionarAsync(api, 2147483000, Nova("Qualquer")),
            await EditarAsync(api, a, 2147483000, Nova("Qualquer")),
            await ExcluirAsync(api, a, 2147483000),
            await EditarAsync(api, a, perguntaDeB, Nova("Por outra porta")),
            await ExcluirAsync(api, a, perguntaDeB),
            await EditarAsync(api, 2147483000, perguntaDeB, Nova("Qualquer")),
        };

        Assert.All(respostas, r => Assert.Equal(404, (int)r.StatusCode));
        Assert.Equal("De B", await TextosAsync(api, b));
        await respostas[1].AfirmarConformeAoContratoAsync("editarPerguntaFormulario");
        await respostas[2].AfirmarConformeAoContratoAsync("excluirPerguntaFormulario");
    }

    [Fact]
    public async Task Quem_nao_e_do_RH_nao_mexe_nas_perguntas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Só o RH"));
        var pergunta = await PerguntaAsync(api, id, "P");
        var comum = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Funcionário"), Perfil.Funcionario);
        var cliente = await Comunicacao.AdvertenciasDeTeste.ClienteDeAsync(api, comum);

        Assert.Equal(
            (403, 403, 403),
            ((int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/perguntas", Nova("X"))).StatusCode, (int)(await cliente.PutAsJsonAsync($"{Rota}/{id}/perguntas/{pergunta}", Nova("X"))).StatusCode, (int)(await cliente.DeleteAsync($"{Rota}/{id}/perguntas/{pergunta}")).StatusCode));
    }

    // ---------------------------------------------------------------- editar

    [Fact]
    public async Task Editar_troca_o_conteudo_inteiro_alternativa_com_id_mantem_sem_id_e_nova_e_a_que_sumiu_e_apagada()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Editar alternativas"));
        var pergunta = await PerguntaAsync(api, id, "Como foi?", "MULTIPLA_ESCOLHA", 1, false, "Ótimo", "Bom", "Ruim");
        var idOtimo = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta} AND TEXTO_ALTERNATIVA = 'Ótimo'");
        var idRuim = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta} AND TEXTO_ALTERNATIVA = 'Ruim'");

        // "Bom" some, "Ótimo" vira "Excelente" (mantém o id), "Ruim" passa para o fim e entra uma nova
        var resposta = await EditarAsync(api, id, pergunta, Nova("Como foi a semana?", "MULTIPLA_ESCOLHA", true, null, Alt("Excelente", idOtimo), Alt("Mais ou menos"), Alt("Ruim", idRuim)));
        var editada = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("editarPerguntaFormulario");
        Assert.Equal(("Como foi a semana?", true), (editada.GetProperty("texto").GetString(), editada.GetProperty("obrigatoria").GetBoolean()));
        Assert.Equal(["Excelente", "Mais ou menos", "Ruim"], editada.GetProperty("alternativas").EnumerateArray().Select(a => a.GetProperty("texto").GetString()));
        Assert.Equal("1:Excelente|2:Mais ou menos|3:Ruim", await AlternativasAsync(api, pergunta));
        Assert.Equal((idOtimo, idRuim), (editada.GetProperty("alternativas")[0].GetProperty("id").GetInt32(), editada.GetProperty("alternativas")[2].GetProperty("id").GetInt32())); // os ids de quem ficou não mudam
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta} AND TEXTO_ALTERNATIVA = 'Bom'"));
    }

    [Fact]
    public async Task Trocar_o_tipo_de_multipla_escolha_para_outro_apaga_as_alternativas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Muda de tipo"));
        var pergunta = await PerguntaAsync(api, id, "Escolha", "MULTIPLA_ESCOLHA", 1, false, "Sim", "Não");

        var resposta = await EditarAsync(api, id, pergunta, Nova("Conte", "RESPOSTA_LONGA"));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(0, (await CorpoAsync(resposta)).GetProperty("alternativas").GetArrayLength());
        Assert.Equal(("RESPOSTA_LONGA", (string?)null), (await TextoAsync(api, $"SELECT TIPO_RESPOSTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta}"), await AlternativasAsync(api, pergunta)));
    }

    [Fact]
    public async Task Alternativa_com_id_que_nao_e_da_pergunta_ou_repetido_e_400_em_alternativas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Ids de alternativa"));
        var pergunta = await PerguntaAsync(api, id, "Escolha", "MULTIPLA_ESCOLHA", 1, false, "Sim", "Não");
        var outra = await PerguntaAsync(api, id, "Outra", "MULTIPLA_ESCOLHA", 2, false, "X", "Y");
        var idDaOutra = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {outra} AND TEXTO_ALTERNATIVA = 'X'");
        var idSim = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta} AND TEXTO_ALTERNATIVA = 'Sim'");

        var deOutra = await EditarAsync(api, id, pergunta, Nova("Escolha", "MULTIPLA_ESCOLHA", false, null, Alt("Sim", idDaOutra), Alt("Não")));
        var inexistente = await EditarAsync(api, id, pergunta, Nova("Escolha", "MULTIPLA_ESCOLHA", false, null, Alt("Sim", 2147483000), Alt("Não")));
        var repetido = await EditarAsync(api, id, pergunta, Nova("Escolha", "MULTIPLA_ESCOLHA", false, null, Alt("Sim", idSim), Alt("Sim de novo", idSim)));

        Assert.All(new[] { deOutra, inexistente, repetido }, r => Assert.Equal(400, (int)r.StatusCode));
        Assert.Equal(["Uma das alternativas não existe nesta pergunta ou foi repetida."], (await CorpoAsync(deOutra)).GetProperty("errors").GetProperty("alternativas").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("1:Sim|2:Não", await AlternativasAsync(api, pergunta));
    }

    [Fact]
    public async Task Editar_com_ordem_move_a_pergunta_sem_1062_e_sem_buraco_e_sem_ordem_nao_move()
    {
        var api = await ApiAsync();
        var id = await RascunhoComTresAsync(api);
        var c = await IdDaPerguntaAsync(api, id, "C");
        var a = await IdDaPerguntaAsync(api, id, "A");

        var paraOPrimeiro = await EditarAsync(api, id, c, Nova("C", ordem: 1));
        var semOrdem = await EditarAsync(api, id, a, Nova("A"));
        var paraOFim = await EditarAsync(api, id, c, Nova("C", ordem: 99));

        Assert.Equal((200, 1), ((int)paraOPrimeiro.StatusCode, (await CorpoAsync(paraOPrimeiro)).GetProperty("ordem").GetInt32()));
        Assert.Equal(2, (await CorpoAsync(semOrdem)).GetProperty("ordem").GetInt32()); // depois do primeiro passo A está na posição 2 e não se move
        Assert.Equal(3, (await CorpoAsync(paraOFim)).GetProperty("ordem").GetInt32());
        Assert.Equal(("1,2,3", "A|B|C"), (await OrdensAsync(api, id), await TextosAsync(api, id)));
    }

    [Fact]
    public async Task Mover_a_pergunta_3_para_a_posicao_1_deixa_a_ordem_1_2_3_com_os_outros_deslocados()
    {
        var api = await ApiAsync();
        var id = await RascunhoComTresAsync(api);

        await EditarAsync(api, id, await IdDaPerguntaAsync(api, id, "C"), Nova("C", ordem: 1));
        await EditarAsync(api, id, await IdDaPerguntaAsync(api, id, "A"), Nova("A", ordem: 3));

        Assert.Equal(("1,2,3", "C|B|A"), (await OrdensAsync(api, id), await TextosAsync(api, id)));
    }

    [Fact]
    public async Task Pergunta_respondida_so_muda_obrigatoria_e_ordem_e_o_resto_e_422_PERGUNTA_JA_RESPONDIDA_com_a_regra_do_banco()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Respondido"), "PUBLICADO", alvo: "PESSOAS");
        var respondida = await PerguntaAsync(api, id, "Respondida", "MULTIPLA_ESCOLHA", 1, false, "Sim", "Não");
        await PerguntaAsync(api, id, "Outra", ordem: 2);
        var idSim = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {respondida} AND TEXTO_ALTERNATIVA = 'Sim'");
        var idNao = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {respondida} AND TEXTO_ALTERNATIVA = 'Não'");
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Respondente"), Perfil.Funcionario);
        await ResponderAsync(api, id, respondida, pessoa);

        var texto = await EditarAsync(api, id, respondida, Nova("Outro texto", "MULTIPLA_ESCOLHA", false, null, Alt("Sim", idSim), Alt("Não", idNao)));
        var tipo = await EditarAsync(api, id, respondida, Nova("Respondida", "RESPOSTA_CURTA"));
        var alternativas = await EditarAsync(api, id, respondida, Nova("Respondida", "MULTIPLA_ESCOLHA", false, null, Alt("Sim", idSim), Alt("Talvez")));
        var alternativaNova = await EditarAsync(api, id, respondida, Nova("Respondida", "MULTIPLA_ESCOLHA", false, null, Alt("Sim", idSim), Alt("Não", idNao), Alt("Talvez")));
        var soObrigatoriaEOrdem = await EditarAsync(api, id, respondida, Nova("Respondida", "MULTIPLA_ESCOLHA", true, 2, Alt("Sim", idSim), Alt("Não", idNao)));

        foreach (var recusada in new[] { texto, tipo, alternativas, alternativaNova })
        {
            var problema = await CorpoAsync(recusada);
            Assert.Equal((422, "PERGUNTA_JA_RESPONDIDA", "Esta pergunta já foi respondida. Só dá para mudar se ela é obrigatória e a posição dela.", "Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario."), ((int)recusada.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString(), problema.GetProperty("regraDoBanco").GetString()));
        }

        await texto.AfirmarConformeAoContratoAsync("editarPerguntaFormulario");
        Assert.Equal(200, (int)soObrigatoriaEOrdem.StatusCode);
        Assert.Equal(("Outra|Respondida", "True", "1:Sim|2:Não"), (await TextosAsync(api, id), await TextoAsync(api, $"SELECT OBRIGATORIA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO_PERGUNTA = {respondida}"), await AlternativasAsync(api, respondida)));
    }

    [Fact]
    public async Task Editar_uma_pergunta_ainda_nao_respondida_de_formulario_com_nome_publicado_muda_tudo()
    {
        var api = await ApiAsync();
        var id = await PublicadoRespondidoAsync(api, Titulo("Publicado"), anonimo: false);
        var livre = await PerguntaAsync(api, id, "Ainda ninguém respondeu", ordem: 2);

        var resposta = await EditarAsync(api, id, livre, Nova("Agora é múltipla", "MULTIPLA_ESCOLHA", true, null, Alt("Sim"), Alt("Não")));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("Como foi?|Agora é múltipla", "1:Sim|2:Não"), (await TextosAsync(api, id), await AlternativasAsync(api, livre)));
    }

    // ---------------------------------------------------------------- excluir

    [Fact]
    public async Task Excluir_apaga_a_pergunta_e_as_alternativas_e_as_seguintes_sobem()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Excluir"));
        await PerguntaAsync(api, id, "A", ordem: 1);
        var b = await PerguntaAsync(api, id, "B", "MULTIPLA_ESCOLHA", 2, false, "Sim", "Não");
        await PerguntaAsync(api, id, "C", ordem: 3);
        await PerguntaAsync(api, id, "D", ordem: 4);

        var resposta = await ExcluirAsync(api, id, b);

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal(("1,2,3", "A|C|D", 0), (await OrdensAsync(api, id), await TextosAsync(api, id), await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {b}")));
        Assert.Equal(404, (int)(await ExcluirAsync(api, id, b)).StatusCode); // a segunda vez não existe mais
    }

    [Fact]
    public async Task Excluir_pergunta_respondida_e_422_com_a_regra_do_banco_do_excluir_e_nada_some()
    {
        var api = await ApiAsync();
        var id = await PublicadoRespondidoAsync(api, Titulo("Excluir respondida"), anonimo: false);
        var pergunta = await IdDaPerguntaAsync(api, id, "Como foi?");

        var resposta = await ExcluirAsync(api, id, pergunta);
        var problema = await CorpoAsync(resposta);

        Assert.Equal((422, "PERGUNTA_JA_RESPONDIDA", "Esta pergunta já foi respondida e não pode ser excluída.", "Pergunta ja respondida: nao e possivel exclui-la."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString(), problema.GetProperty("regraDoBanco").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("excluirPerguntaFormulario");
        Assert.Equal("Como foi?", await TextosAsync(api, id));
    }

    // ---------------------------------------------------------------- auditoria

    [Fact]
    public async Task Cada_mudanca_de_pergunta_grava_um_evento_ALTEROU_sem_o_texto_da_pergunta()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Auditoria de perguntas");
        var id = await SemearAsync(api, titulo, anonimo: true);

        var criada = await CorpoAsync(await AdicionarAsync(api, id, Nova("Texto secreto da pergunta anônima")));
        var pergunta = criada.GetProperty("id").GetInt32();
        await EditarAsync(api, id, pergunta, Nova("Outro texto secreto", obrigatoria: true));
        await ExcluirAsync(api, id, pergunta);

        var eventos = await EventosAsync(api, id);
        Assert.Equal(3, eventos.Count);
        Assert.All(eventos, e => Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Alterou), (e.Area, e.Acao)));
        Assert.Equal([$"Acrescentou uma pergunta ao formulário \"{titulo}\".", $"Alterou uma pergunta do formulário \"{titulo}\".", $"Excluiu uma pergunta do formulário \"{titulo}\"."], eventos.OrderBy(e => e.Id).Select(e => e.Descricao));
        Assert.DoesNotContain(eventos, e => e.Descricao.Contains("secreto", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- a corrida entre a leitura e a gravação

    [Fact]
    public async Task Editar_pergunta_de_anonimo_enquanto_outra_aba_publica_relê_e_da_o_409_sem_gravar()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Anônimo duas abas"), anonimo: true);
        var pergunta = await PerguntaAsync(api, id, "Pergunta original");
        var cliente = await ClienteComOutraAbaAsync(api, () => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = TRUE, PUBLICO = 'PESSOAS', DATA_PUBLICACAO = '2026-10-07 09:41:00' WHERE ID_FORMULARIO = {id}"));

        var resposta = await cliente.PutAsJsonAsync($"{Rota}/{id}/perguntas/{pergunta}", Nova("Pergunta trocada por baixo"));

        Assert.Equal((409, "ANONIMO_PERGUNTAS_FIXAS"), ((int)resposta.StatusCode, Codigo(await CorpoAsync(resposta))));
        Assert.Equal("Pergunta original", await TextosAsync(api, id));
    }

    [Fact]
    public async Task Excluir_pergunta_enquanto_outra_aba_encerra_o_formulario_relê_e_da_422_sem_apagar()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Encerrado no meio"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, id, "Fica");
        var cliente = await ClienteComOutraAbaAsync(api, () => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-07 09:41:00' WHERE ID_FORMULARIO = {id}"));

        var resposta = await cliente.DeleteAsync($"{Rota}/{id}/perguntas/{pergunta}");

        Assert.Equal((422, "FORMULARIO_ENCERRADO"), ((int)resposta.StatusCode, Codigo(await CorpoAsync(resposta))));
        Assert.Equal("Fica", await TextosAsync(api, id));
    }

    [Fact]
    public async Task Os_textos_com_apostrofo_e_emoji_voltam_inteiros()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Textos difíceis"));
        var texto = "Como é o dia no \"Setor d'Água\"? 😀 `código` <b>negrito</b>";

        var pergunta = await CorpoAsync(await AdicionarAsync(api, id, Nova(texto)));

        Assert.Equal(texto, pergunta.GetProperty("texto").GetString()); // markdown e HTML ficam como texto: quem mostra é o front
        Assert.Equal(texto, await TextosAsync(api, id));
    }
}
