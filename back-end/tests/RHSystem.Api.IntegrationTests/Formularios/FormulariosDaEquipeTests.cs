using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Os formulários da equipe do gestor (<c>listarFormulariosDaEquipe</c>, <c>listarRespondentesDaEquipe</c> e <c>lembrarQuemFaltaNaEquipe</c>, só o perfil Gestor), no banco REAL com o relógio em 2026-10-07T12:42Z. A equipe é a definição única (ativos, admissão começada, do setor, sem o gestor); contagens SÓ dela; só quem e
/// quando, nunca o conteúdo; fora da lista: por link, Sugestão, as duas avaliações, encerrado e o que não tem ninguém da equipe; no anônimo aberto só os esperados; o lembrete é só para a equipe e, no anônimo, um por dia e por alcance, com a resposta sempre igual.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FormulariosDaEquipeTests(MySqlFixture mysql)
{
    private const string Rota = "/api/gestor/formularios";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(FormulariosDaEquipeTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private sealed record Time(SetorDeAdvertencia Setor, int[] Membros, HttpClient Gestor);

    private static async Task<Time> TimeAsync(ApiDeAcesso api, int membros = 3)
    {
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var ids = new int[membros];
        for (var i = 0; i < membros; i++)
        {
            ids[i] = (await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: $"ZEq{i}")).Id;
        }

        return new Time(setor, ids, await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId));
    }

    private static async Task<int> FormularioAsync(ApiDeAcesso api, string prefixo, string situacao = "PUBLICADO", string tipo = "PESQUISA", bool anonimo = false, string? prazo = null, string alvo = "PESSOAS", string? sentido = null, string? token = null, params int[] pessoas)
    {
        var id = await SemearAsync(api, Titulo(prefixo), situacao, anonimo: anonimo, tipo: tipo, alvo: alvo, prazo: prazo, sentido: sentido, token: token);
        await PerguntaAsync(api, id, "Como foi?");
        if (pessoas.Length > 0)
        {
            await EscolherPessoasAsync(api, id, pessoas);
        }

        return id;
    }

    private static Task ParticiparAsync(ApiDeAcesso api, int formulario, int pessoa) => Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid()}', {formulario}, {pessoa})");

    private static async Task<JsonElement> ListarAsync(HttpClient gestor) => await CorpoAsync(await gestor.GetAsync(Rota));

    private static JsonElement Item(JsonElement lista, int id) => lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == id);

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_traz_so_os_pedidos_publicados_com_gente_da_equipe_com_as_contagens_da_equipe_pelo_prazo_e_pelo_titulo()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api);
        var (m1, m2, m3) = (time.Membros[0], time.Membros[1], time.Membros[2]);
        var fora = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZEq fora"), RHSystem.Domain.Autenticacao.Perfil.Funcionario);
        var noPrazo = await FormularioAsync(api, "ZEq B pesquisa", prazo: "2026-11-15", pessoas: [m1, m2, fora, time.Setor.GestorFuncionarioId]);
        var porSetor = await FormularioAsync(api, "ZEq A admissão", tipo: "ADMISSAO", prazo: "2026-10-31", alvo: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({porSetor}, {time.Setor.SetorId})");
        var semPrazo = await FormularioAsync(api, "ZEq C sem prazo", pessoas: [m3]);
        var anonimo = await FormularioAsync(api, "ZEq D anônimo", anonimo: true, prazo: "2026-11-20", pessoas: [m1, m2, m3]);
        await ParticiparAsync(api, anonimo, m1);
        await FormulariosDeTeste.ResponderAsync(api, noPrazo, await PerguntaDe(api, noPrazo), m1);
        var excluidos = new[]
        {
            await FormularioAsync(api, "ZEq link", tipo: "ENTREVISTA", alvo: "LINK", token: new string('e', 43)),
            await FormularioAsync(api, "ZEq sugestão", tipo: "OUTRO", pessoas: [m1, m2]),
            await FormularioAsync(api, "ZEq avaliação da equipe", tipo: "AVALIACAO", sentido: "GESTOR_AVALIA_EQUIPE", pessoas: [time.Setor.GestorFuncionarioId]),
            await FormularioAsync(api, "ZEq avaliação do gestor", tipo: "AVALIACAO", sentido: "EQUIPE_AVALIA_GESTOR", pessoas: [m1, m2]),
            await FormularioAsync(api, "ZEq rascunho", situacao: "RASCUNHO", pessoas: [m1]),
            await FormularioAsync(api, "ZEq agendado", situacao: "AGENDADO", pessoas: [m1]),
            await FormularioAsync(api, "ZEq vencido", prazo: "2026-10-06", pessoas: [m1]),
            await FormularioAsync(api, "ZEq só de fora", pessoas: [fora]),
            await FormularioAsync(api, "ZEq só do gestor", pessoas: [time.Setor.GestorFuncionarioId]),
        };
        var encerrado = await FormularioAsync(api, "ZEq encerrado", pessoas: [m1]);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {encerrado}");

        var resposta = await time.Gestor.GetAsync(Rota);
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarFormulariosDaEquipe");
        Assert.Equal([porSetor, noPrazo, anonimo, semPrazo], lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Where(id => id == porSetor || id == noPrazo || id == anonimo || id == semPrazo).ToList()); // prazo mais próximo, depois o maior, o sem prazo por último
        Assert.DoesNotContain(lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()), id => excluidos.Contains(id) || id == encerrado);
        Assert.Equal((1, 2, 50, "2026-11-15"), (Item(lista, noPrazo).GetProperty("respondidos").GetInt32(), Item(lista, noPrazo).GetProperty("esperados").GetInt32(), Item(lista, noPrazo).GetProperty("taxaDeRespostaPercentual").GetInt32(), Item(lista, noPrazo).GetProperty("prazo").GetString())); // só a equipe: o de fora e o gestor não contam
        Assert.Equal((0, 3, 0), (Item(lista, porSetor).GetProperty("respondidos").GetInt32(), Item(lista, porSetor).GetProperty("esperados").GetInt32(), Item(lista, porSetor).GetProperty("taxaDeRespostaPercentual").GetInt32()));
        var doAnonimo = Item(lista, anonimo);
        Assert.Equal((true, JsonValueKind.Null, 3, JsonValueKind.Null), (doAnonimo.GetProperty("anonimo").GetBoolean(), doAnonimo.GetProperty("respondidos").ValueKind, doAnonimo.GetProperty("esperados").GetInt32(), doAnonimo.GetProperty("taxaDeRespostaPercentual").ValueKind)); // aberto: nem número, nem faixa
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (Item(lista, semPrazo).GetProperty("prazo").ValueKind, Item(lista, semPrazo).GetProperty("sentidoAvaliacao").ValueKind));
    }

    private static async Task<int> PerguntaDe(ApiDeAcesso api, int formulario) => (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_PERGUNTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {formulario} LIMIT 1");

    [Fact]
    public async Task Gestor_sem_setor_ve_lista_vazia_e_so_o_Gestor_acessa_as_tres_rotas()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api);
        var id = await FormularioAsync(api, "ZEq perfis", pessoas: [time.Membros[0]]);
        var semSetor = await AdvertenciasDeTeste.ClienteDeAsync(api, api.Elenco.GestorSemSetor.FuncionarioId);
        var rh = await api.ClienteDoRhAgoraAsync();
        var membro = api.Api.ClienteComo(api.Elenco.MembroA);
        var anonimo = api.Api.ClienteAnonimo();
        var rotas = new (string Metodo, string Caminho)[] { ("GET", Rota), ("GET", $"{Rota}/{id}/respondentes"), ("POST", $"{Rota}/{id}/lembretes") };

        var vazia = await ListarAsync(semSetor);
        var respostas = new List<int>();
        foreach (var cliente in new[] { rh, membro, anonimo })
        {
            foreach (var (metodo, caminho) in rotas)
            {
                respostas.Add((int)(await (metodo == "GET" ? cliente.GetAsync(caminho) : cliente.PostAsync(caminho, null))).StatusCode);
            }
        }

        Assert.Empty(vazia.GetProperty("itens").EnumerateArray());
        Assert.Equal([403, 403, 403, 403, 403, 403, 401, 401, 401], respostas);
        Assert.Equal(404, (int)(await semSetor.GetAsync($"{Rota}/{id}/respondentes")).StatusCode);
    }

    // ---------------------------------------------------------------- quem respondeu

    [Fact]
    public async Task Os_respondentes_da_equipe_dizem_so_quem_e_quando_em_ordem_de_nome_com_a_observacao_das_ferias()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api);
        var fora = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZEq fora"), RHSystem.Domain.Autenticacao.Perfil.Funcionario);
        var id = await FormularioAsync(api, "ZEq respondentes", pessoas: [.. time.Membros, fora, time.Setor.GestorFuncionarioId]);
        var pergunta = await PerguntaDe(api, id);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {id}, {time.Membros[1]}, 'Segredo.', '2026-10-03 09:31:00', 0)");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, STATUS_FERIAS) VALUES ({time.Membros[2]}, '2026-01-02', '2026-10-04', '2026-10-05', '2026-10-17', 'APROVADA')");

        var resposta = await time.Gestor.GetAsync($"{Rota}/{id}/respondentes");
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarRespondentesDaEquipe");
        Assert.Equal((id, false, 1, 2, 3, 33), (corpo.GetProperty("formularioId").GetInt32(), corpo.GetProperty("anonimo").GetBoolean(), corpo.GetProperty("respondidos").GetInt32(), corpo.GetProperty("faltam").GetInt32(), corpo.GetProperty("esperados").GetInt32(), corpo.GetProperty("taxaDeRespostaPercentual").GetInt32()));
        var itens = corpo.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal(time.Membros, itens.Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32())); // a equipe, por nome (ZEq0, ZEq1, ZEq2), sem o gestor nem o de fora
        Assert.Equal(["NAO_RESPONDEU", "RESPONDIDO", "NAO_RESPONDEU"], itens.Select(i => i.GetProperty("situacao").GetString()));
        Assert.Equal("2026-10-03T12:31:00Z", itens[1].GetProperty("respondidoEmUtc").GetString());
        Assert.Equal([JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.String], itens.Select(i => i.GetProperty("observacao").ValueKind));
        Assert.Equal("de férias até 17/10", itens[2].GetProperty("observacao").GetString());
        Assert.DoesNotContain("Segredo", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal); // nunca o conteúdo
    }

    [Fact]
    public async Task No_anonimo_a_lista_e_vazia_e_aberto_so_os_esperados_encerrado_os_numeros_da_equipe_sem_dizer_quem()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api, 4);
        var fora = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZEq fora"), RHSystem.Domain.Autenticacao.Perfil.Funcionario);
        var aberto = await FormularioAsync(api, "ZEq anônimo aberto", anonimo: true, pessoas: [.. time.Membros, fora]);
        var vencido = await FormularioAsync(api, "ZEq anônimo vencido", anonimo: true, prazo: "2026-10-06", pessoas: [.. time.Membros, fora]);
        foreach (var formulario in new[] { aberto, vencido })
        {
            await ParticiparAsync(api, formulario, time.Membros[0]);
            await ParticiparAsync(api, formulario, time.Membros[1]);
            await ParticiparAsync(api, formulario, time.Membros[2]);
            await ParticiparAsync(api, formulario, fora); // não é da equipe: não conta
        }

        var doAberto = await CorpoAsync(await time.Gestor.GetAsync($"{Rota}/{aberto}/respondentes"));
        var doVencido = await CorpoAsync(await time.Gestor.GetAsync($"{Rota}/{vencido}/respondentes"));

        static object Numeros(JsonElement e) => (e.GetProperty("anonimo").GetBoolean(), e.GetProperty("itens").GetArrayLength(), e.GetProperty("respondidos").ValueKind == JsonValueKind.Null ? null : (int?)e.GetProperty("respondidos").GetInt32(), e.GetProperty("faltam").ValueKind == JsonValueKind.Null ? null : (int?)e.GetProperty("faltam").GetInt32(), e.GetProperty("esperados").GetInt32(), e.GetProperty("taxaDeRespostaPercentual").ValueKind == JsonValueKind.Null ? null : (int?)e.GetProperty("taxaDeRespostaPercentual").GetInt32());
        Assert.Equal((true, 0, (int?)null, (int?)null, 4, (int?)null), Numeros(doAberto));
        Assert.Equal((true, 0, (int?)3, (int?)1, 4, (int?)75), Numeros(doVencido)); // o prazo vencido conta como encerrado: 3 da equipe participaram, sem saber quem
    }

    [Fact]
    public async Task Formulario_de_outro_setor_Sugestao_avaliacao_link_e_nao_publicado_dao_404_e_o_encerrado_ainda_abre()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api);
        var outro = await TimeAsync(api, 1);
        var casos = new Dictionary<string, int>
        {
            ["de outro setor"] = await FormularioAsync(api, "ZEq outro", pessoas: [outro.Membros[0]]),
            ["Sugestão"] = await FormularioAsync(api, "ZEq sugestão", tipo: "OUTRO", pessoas: [time.Membros[0]]),
            ["avaliação da equipe"] = await FormularioAsync(api, "ZEq av1", tipo: "AVALIACAO", sentido: "GESTOR_AVALIA_EQUIPE", pessoas: [time.Setor.GestorFuncionarioId]),
            ["avaliação do gestor"] = await FormularioAsync(api, "ZEq av2", tipo: "AVALIACAO", sentido: "EQUIPE_AVALIA_GESTOR", pessoas: [time.Membros[0]]),
            ["por link"] = await FormularioAsync(api, "ZEq link", tipo: "ENTREVISTA", alvo: "LINK", token: new string('k', 43)),
            ["rascunho"] = await FormularioAsync(api, "ZEq rascunho", situacao: "RASCUNHO", pessoas: [time.Membros[0]]),
            ["agendado"] = await FormularioAsync(api, "ZEq agendado", situacao: "AGENDADO", pessoas: [time.Membros[0]]),
        };
        var encerrado = await FormularioAsync(api, "ZEq encerrado", pessoas: [time.Membros[0]]);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {encerrado}");

        var statusDosCasos = new Dictionary<string, int>();
        foreach (var (nome, id) in casos)
        {
            statusDosCasos[nome] = (int)(await time.Gestor.GetAsync($"{Rota}/{id}/respondentes")).StatusCode;
        }

        var inexistente = await time.Gestor.GetAsync($"{Rota}/99999999/respondentes");
        var doEncerrado = await time.Gestor.GetAsync($"{Rota}/{encerrado}/respondentes");

        Assert.All(statusDosCasos, par => Assert.Equal(404, par.Value));
        Assert.Equal((404, 200), ((int)inexistente.StatusCode, (int)doEncerrado.StatusCode));
    }

    // ---------------------------------------------------------------- lembrete

    private static async Task<(int, int)> NumerosAsync(HttpResponseMessage resposta)
    {
        var corpo = await CorpoAsync(resposta);
        return (corpo.GetProperty("enviados").GetInt32(), corpo.GetProperty("ignorados").GetInt32());
    }

    [Fact]
    public async Task O_lembrete_do_gestor_vai_so_para_a_equipe_que_falta_e_os_outros_entram_em_ignorados()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api, 4);
        var fora = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZEq fora"), RHSystem.Domain.Autenticacao.Perfil.Funcionario);
        var id = await FormularioAsync(api, "ZEq lembrete", prazo: "2026-10-30", pessoas: [.. time.Membros, fora, time.Setor.GestorFuncionarioId]);
        var pergunta = await PerguntaDe(api, id);
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, time.Membros[0]);

        var primeira = await time.Gestor.PostAsync($"{Rota}/{id}/lembretes", null);
        var segunda = await time.Gestor.PostAsync($"{Rota}/{id}/lembretes", null);

        Assert.Equal(200, (int)primeira.StatusCode);
        await primeira.AfirmarConformeAoContratoAsync("lembrarQuemFaltaNaEquipe");
        Assert.Equal(((3, 1), (0, 4)), (await NumerosAsync(primeira), await NumerosAsync(segunda))); // 3 enviados e 1 ignorado (já respondeu); depois ninguém: já receberam hoje
        var destinatarios = await Sql.ColunaAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id} ORDER BY ID_FUNCIONARIO");
        Assert.Equal(time.Membros.Skip(1).Select(m => m.ToString()), destinatarios); // nem o gestor, nem o de fora, nem quem respondeu
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.EnviouEmail), (evento.Area, evento.Acao));
    }

    [Fact]
    public async Task O_lembrete_do_gestor_recusa_o_que_nao_e_da_equipe_com_404_e_o_encerrado_com_422()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api);
        var outro = await TimeAsync(api, 1);
        var deOutro = await FormularioAsync(api, "ZEq outro", pessoas: [outro.Membros[0]]);
        var sugestao = await FormularioAsync(api, "ZEq sugestão", tipo: "OUTRO", pessoas: [time.Membros[0]]);
        var avaliacao = await FormularioAsync(api, "ZEq avaliação", tipo: "AVALIACAO", sentido: "EQUIPE_AVALIA_GESTOR", pessoas: [time.Membros[0]]);
        var vencido = await FormularioAsync(api, "ZEq vencido", prazo: "2026-10-06", pessoas: [time.Membros[0]]);
        var encerrado = await FormularioAsync(api, "ZEq encerrado", pessoas: [time.Membros[0]]);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {encerrado}");

        var respostas = new List<HttpResponseMessage>();
        foreach (var id in new[] { deOutro, sugestao, avaliacao, 99999999, vencido, encerrado })
        {
            respostas.Add(await time.Gestor.PostAsync($"{Rota}/{id}/lembretes", null));
        }

        Assert.Equal([404, 404, 404, 404, 422, 422], respostas.Select(r => (int)r.StatusCode));
        Assert.Equal(["FORMULARIO_NAO_PUBLICADO", "FORMULARIO_NAO_PUBLICADO"], new[] { Codigo(await CorpoAsync(respostas[4])), Codigo(await CorpoAsync(respostas[5])) });
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID IN (" + string.Join(",", new[] { deOutro, sugestao, avaliacao, vencido, encerrado }) + ")"));
    }

    [Fact]
    public async Task No_anonimo_o_lembrete_do_gestor_responde_sempre_igual_e_vale_um_por_dia_e_por_alcance_so_para_a_equipe()
    {
        var api = await ApiAsync();
        var time = await TimeAsync(api, 3);
        var fora = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZEq fora"), RHSystem.Domain.Autenticacao.Perfil.Funcionario);
        var id = await FormularioAsync(api, "ZEq anônimo", anonimo: true, prazo: "2026-10-30", pessoas: [.. time.Membros, fora]);
        await ParticiparAsync(api, id, time.Membros[0]);
        var rh = await api.ClienteDoRhAgoraAsync();

        var gestor1 = await time.Gestor.PostAsync($"{Rota}/{id}/lembretes", null);
        var gestor2 = await time.Gestor.PostAsync($"{Rota}/{id}/lembretes", null);
        var doRh = await rh.PostAsJsonAsync($"/api/rh/formularios/{id}/lembretes", new { funcionarioIds = Array.Empty<int>() });
        var constante = """{"anonimo":true,"enviados":null,"ignorados":null}""";

        Assert.Equal([constante, constante, constante], new[] { await gestor1.Content.ReadAsStringAsync(), await gestor2.Content.ReadAsStringAsync(), await doRh.Content.ReadAsStringAsync() });
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND REFERENCIA_ID = {id} AND CHAVE_IDEMPOTENCIA LIKE '%:envio=1'"));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND CHAVE_IDEMPOTENCIA = 'LEMBRETE_FORMULARIO:form={id}:dia=2026-10-07:alcance=SETOR_{time.Setor.SetorId}' AND SITUACAO = 'CONCLUIDA'"));

        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == "LEMBRETE_FORMULARIO");
            var parametros = JsonDocument.Parse($$"""{"setorId":{{time.Setor.SetorId}}}""").RootElement.Clone();
            await tarefa.ExecutarAsync(new TarefaParaExecutar(0, $"LEMBRETE_FORMULARIO:form={id}:dia=2026-10-07:alcance=SETOR_{time.Setor.SetorId}:envio=1", id, new DateOnly(2026, 10, 7), parametros, 0), CancellationToken.None);
        }

        Assert.Equal((2L, 2L, 0L), (await ContarAsync(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id} AND SIGILOSO = 1"), await ContarAsync(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id} AND ID_FUNCIONARIO IS NULL"), await ContarAsync(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id} AND DESTINATARIO_EMAIL IN (SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO IN ({fora}, {time.Membros[0]}, {time.Setor.GestorFuncionarioId}))"))); // só os 2 da equipe que não participaram: nem o de fora, nem quem participou, nem o gestor
        Assert.Equal(3, (await EventosAsync(api, id)).Count); // um evento por clique, sempre, sem número nem destinatário
    }
}
