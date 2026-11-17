using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// As duas rotas que a decisão do dono de 2026-11-17 criou: <c>alterarPrazoDoFormulario</c> (mudar o prazo de um formulário PUBLICADO) e <c>voltarFormularioParaRascunho</c> (devolver um AGENDADO a rascunho). No banco REAL com o relógio em 2026-10-07T12:42Z (09:42 em Brasília).
/// O prazo troca a tarefa de encerramento e o lembrete automático (a chave leva o instante, e uma chave cancelada volta); voltar a rascunho apaga o dia e o código do link e cancela todas as tarefas pendentes.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PrazoEVoltarParaRascunhoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PrazoEVoltarParaRascunhoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static object Corpo(string alvo, int[]? pessoas = null, string? agendarPara = null, string? prazo = null, object? recorrencia = null) =>
        new { alvo, setorIds = Array.Empty<int>(), funcionarioIds = pessoas ?? [], agendarPara, prazo, recorrencia, lembreteAutomatico = true };

    private static async Task<HttpResponseMessage> PublicarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{id}/publicar", corpo);

    private static async Task<HttpResponseMessage> PrazoAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}/prazo", corpo);

    private static async Task<HttpResponseMessage> RascunhoAsync(ApiDeAcesso api, int id) => await (await api.ClienteDoRhAgoraAsync()).PostAsync($"{Rota}/{id}/rascunho", content: null);

    private static async Task<int[]> PessoasAsync(ApiDeAcesso api, int quantas)
    {
        var ids = new int[quantas];
        for (var i = 0; i < quantas; i++)
        {
            ids[i] = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo($"Pessoa {i}"), Perfil.Funcionario);
        }

        return ids;
    }

    /// <summary>Um formulário de rascunho com uma pergunta e a descrição (a verificação passa), pronto para publicar.</summary>
    private static async Task<int> ProntoAsync(ApiDeAcesso api, string titulo, string tipo = "PESQUISA")
    {
        var id = await SemearAsync(api, titulo, tipo: tipo, descricao: "Conte como foi.");
        await PerguntaAsync(api, id, "Como foi?", obrigatoria: true);
        return id;
    }

    /// <summary>Publicado pela API (então as tarefas existem), por pessoas, com o prazo e a data de agendamento escolhidos.</summary>
    private static async Task<int> PublicadoAsync(ApiDeAcesso api, string prazo, string? agendarPara = null, object? recorrencia = null)
    {
        var id = await ProntoAsync(api, Titulo("Pesquisa de clima"));
        var resposta = await PublicarAsync(api, id, Corpo("PESSOAS", await PessoasAsync(api, 3), agendarPara, prazo, recorrencia));
        Assert.Equal(200, (int)resposta.StatusCode);
        return id;
    }

    private static Task<long> Encerramentos(ApiDeAcesso api, int id, string situacao, string instanteUtc) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'ENCERRAR_FORMULARIO' AND REFERENCIA_ID = {id} AND SITUACAO = '{situacao}' AND CHAVE_IDEMPOTENCIA = 'ENCERRAR_FORMULARIO:formulario={id}:em={instanteUtc}'");

    private static Task<long> Lembretes(ApiDeAcesso api, int id, string situacao, string dia) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND REFERENCIA_ID = {id} AND SITUACAO = '{situacao}' AND CHAVE_IDEMPOTENCIA = 'LEMBRETE_FORMULARIO:form={id}:dia={dia}:alcance=TODOS'");

    private static Task<long> PendentesDeQualquerTipo(ApiDeAcesso api, int id) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {id} AND TIPO IN ('PUBLICAR_FORMULARIO', 'ENCERRAR_FORMULARIO', 'REABRIR_FORMULARIO', 'LEMBRETE_FORMULARIO') AND SITUACAO = 'PENDENTE'");

    // ---------------------------------------------------------------- alterarPrazoDoFormulario

    [Fact]
    public async Task Mudar_o_prazo_de_um_publicado_grava_o_novo_dia_troca_a_tarefa_de_encerrar_e_o_lembrete_e_registra_o_evento()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-31");
        Assert.Equal((1L, 1L), (await Encerramentos(api, id, "PENDENTE", "2026-11-01T03:00:00Z"), await Lembretes(api, id, "PENDENTE", "2026-10-24"))); // o ponto de partida: prazo 31/10, lembrete 7 dias antes

        var resposta = await PrazoAsync(api, id, new { prazo = "2026-11-15" });

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("alterarPrazoDoFormulario");
        var formulario = await CorpoAsync(resposta);
        Assert.Equal(("PUBLICADO", "2026-11-15"), (formulario.GetProperty("situacao").GetString(), formulario.GetProperty("publicacao").GetProperty("prazo").GetString()));
        Assert.Equal("2026-11-15", await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        Assert.Equal(
            (1L, 1L, 1L, 1L),
            (await Encerramentos(api, id, "CANCELADA", "2026-11-01T03:00:00Z"), await Encerramentos(api, id, "PENDENTE", "2026-11-16T03:00:00Z"), await Lembretes(api, id, "CANCELADA", "2026-10-24"), await Lembretes(api, id, "PENDENTE", "2026-11-08")));
        var titulo = await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");
        var alterou = Assert.Single(await EventosAsync(api, id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal((AuditoriaArea.Formularios, $"Mudou o prazo do formulário \"{titulo}\" de 31/10/2026 para 15/11/2026."), (alterou.Area, alterou.Descricao));
    }

    [Fact]
    public async Task Encurtar_o_prazo_para_hoje_vale_e_nao_deixa_lembrete_pendente_porque_o_dia_dele_ja_passou()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-31");

        var resposta = await PrazoAsync(api, id, new { prazo = "2026-10-07" });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((1L, 1L, 1L), (await Encerramentos(api, id, "CANCELADA", "2026-11-01T03:00:00Z"), await Encerramentos(api, id, "PENDENTE", "2026-10-08T03:00:00Z"), await Lembretes(api, id, "CANCELADA", "2026-10-24")));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND REFERENCIA_ID = {id} AND SITUACAO = 'PENDENTE'"));
    }

    [Fact]
    public async Task Voltar_a_um_prazo_ja_usado_reativa_as_tarefas_canceladas_em_vez_de_criar_outras()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-31");

        Assert.Equal(200, (int)(await PrazoAsync(api, id, new { prazo = "2026-11-15" })).StatusCode);
        Assert.Equal(200, (int)(await PrazoAsync(api, id, new { prazo = "2026-10-31" })).StatusCode);

        Assert.Equal(
            (1L, 1L, 1L, 1L),
            (await Encerramentos(api, id, "PENDENTE", "2026-11-01T03:00:00Z"), await Encerramentos(api, id, "CANCELADA", "2026-11-16T03:00:00Z"), await Lembretes(api, id, "PENDENTE", "2026-10-24"), await Lembretes(api, id, "CANCELADA", "2026-11-08")));
        Assert.Equal(2L, await PendentesDeQualquerTipo(api, id));
    }

    [Fact]
    public async Task O_mesmo_prazo_e_200_sem_efeito_nem_evento_novo()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-31");
        var eventosAntes = (await EventosAsync(api, id)).Count;

        var resposta = await PrazoAsync(api, id, new { prazo = "2026-10-31" });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(eventosAntes, (await EventosAsync(api, id)).Count);
        Assert.Equal(2L, await PendentesDeQualquerTipo(api, id));
    }

    [Fact]
    public async Task Num_formulario_recorrente_so_o_prazo_da_rodada_muda_e_a_proxima_reabertura_continua()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-20", recorrencia: new { intervalo = 1, unidade = "MES" });
        var reaberturaAntes = await TextoAsync(api, $"SELECT PROXIMA_REABERTURA FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");

        var resposta = await PrazoAsync(api, id, new { prazo = "2026-10-25" });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.NotNull(reaberturaAntes);
        Assert.Equal(reaberturaAntes, await TextoAsync(api, $"SELECT PROXIMA_REABERTURA FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        Assert.Equal("2026-10-25", await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Theory]
    [InlineData("{\"prazo\":\"2026-10-06\"}", "prazo")]
    [InlineData("{\"prazo\":null}", "corpo")]
    [InlineData("{}", "corpo")]
    public async Task Prazo_anterior_a_hoje_vazio_ausente_ou_invalido_e_400_com_o_erro_no_campo_e_nada_muda(string corpo, string campo)
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-31");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PutAsync($"{Rota}/{id}/prazo", new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
        Assert.Equal("2026-10-31", await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Rascunho_e_agendado_sao_422_FORMULARIO_NAO_PUBLICADO_e_encerrado_ou_com_prazo_vencido_e_422_FORMULARIO_ENCERRADO()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("Rascunho"));
        var agendado = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");
        var vencido = await SemearAsync(api, Titulo("Prazo vencido"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");

        var naoPublicados = new[] { await PrazoAsync(api, rascunho, new { prazo = "2026-11-15" }), await PrazoAsync(api, agendado, new { prazo = "2026-11-15" }) };
        var encerrados = new[] { await PrazoAsync(api, encerrado, new { prazo = "2026-11-15" }), await PrazoAsync(api, vencido, new { prazo = "2026-11-15" }) };

        foreach (var r in naoPublicados)
        {
            Assert.Equal((422, "FORMULARIO_NAO_PUBLICADO"), ((int)r.StatusCode, Codigo(await CorpoAsync(r))));
        }

        foreach (var r in encerrados)
        {
            Assert.Equal((422, "FORMULARIO_ENCERRADO"), ((int)r.StatusCode, Codigo(await CorpoAsync(r))));
        }

        await naoPublicados[0].AfirmarConformeAoContratoAsync("alterarPrazoDoFormulario");
        await encerrados[0].AfirmarConformeAoContratoAsync("alterarPrazoDoFormulario");
        Assert.Equal(404, (int)(await PrazoAsync(api, 2147483000, new { prazo = "2026-11-15" })).StatusCode);
    }

    [Fact]
    public async Task A_caixa_de_sugestoes_nao_tem_prazo_e_pedir_um_e_400_no_campo_prazo()
    {
        var api = await ApiAsync();
        var sugestao = await SemearAsync(api, Titulo("Sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");

        var resposta = await PrazoAsync(api, sugestao, new { prazo = "2026-11-15" });

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Equal("A caixa de sugestões é sempre aberta: não tem prazo.", (await CorpoAsync(resposta)).GetProperty("errors").GetProperty("prazo")[0].GetString());
    }

    [Fact]
    public async Task So_o_RH_muda_o_prazo_e_volta_a_rascunho()
    {
        var api = await ApiAsync();
        var publicado = await SemearAsync(api, Titulo("Só o RH"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-31");
        var agendado = await SemearAsync(api, Titulo("Só o RH 2"), "AGENDADO", alvo: "PESSOAS");

        var comum = (await PessoasAsync(api, 1))[0];
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, comum);

        Assert.Equal((403, 403), ((int)(await cliente.PutAsJsonAsync($"{Rota}/{publicado}/prazo", new { prazo = "2026-11-15" })).StatusCode, (int)(await cliente.PostAsync($"{Rota}/{agendado}/rascunho", content: null)).StatusCode));
        Assert.Equal("2026-10-31", await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {publicado}"));
    }

    // ---------------------------------------------------------------- voltarFormularioParaRascunho

    [Fact]
    public async Task Um_agendado_volta_a_rascunho_cancela_todas_as_tarefas_apaga_o_dia_e_deixa_publicar_de_novo()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-11-30", agendarPara: "2026-10-20");
        Assert.Equal(("False", 3L), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await PendentesDeQualquerTipo(api, id))); // publicar, encerrar e lembrete

        var resposta = await RascunhoAsync(api, id);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("voltarFormularioParaRascunho");
        var formulario = await CorpoAsync(resposta);
        Assert.Equal(("RASCUNHO", JsonValueKind.Null, JsonValueKind.Null, "2026-11-30"), (formulario.GetProperty("situacao").GetString(), formulario.GetProperty("publicacao").GetProperty("agendarPara").ValueKind, formulario.GetProperty("publicacao").GetProperty("publicadoEmUtc").ValueKind, formulario.GetProperty("publicacao").GetProperty("prazo").GetString())); // as outras escolhas ficam guardadas
        Assert.Equal((null, "False", 0L), (await TextoAsync(api, $"SELECT DATA_PUBLICACAO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await PendentesDeQualquerTipo(api, id)));
        Assert.Equal(3L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {id} AND TIPO IN ('PUBLICAR_FORMULARIO', 'ENCERRAR_FORMULARIO', 'LEMBRETE_FORMULARIO') AND SITUACAO = 'CANCELADA'"));
        var alterou = Assert.Single(await EventosAsync(api, id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal($"Voltou o formulário \"{await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")}\" para rascunho.", alterou.Descricao);

        var denovo = await PublicarAsync(api, id, Corpo("PESSOAS", await PessoasAsync(api, 3), "2026-10-25", "2026-11-30"));

        Assert.Equal(200, (int)denovo.StatusCode);
        Assert.Equal(("AGENDADO", 3L), ((await CorpoAsync(denovo)).GetProperty("situacao").GetString(), await PendentesDeQualquerTipo(api, id))); // o encerramento e o lembrete cancelados voltam, e a publicação do novo dia é outra tarefa
    }

    [Fact]
    public async Task O_agendado_por_link_perde_o_codigo_ao_voltar_a_rascunho_e_ganha_outro_ao_publicar()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Entrevista agendada"), tipo: "ENTREVISTA");
        var agendada = await CorpoAsync(await PublicarAsync(api, id, Corpo("LINK", agendarPara: "2026-11-03", prazo: "2026-11-17")));
        var codigo = agendada.GetProperty("publicacao").GetProperty("tokenDoLink").GetString();
        Assert.NotNull(codigo);

        var resposta = await RascunhoAsync(api, id);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await CorpoAsync(resposta)).GetProperty("publicacao").GetProperty("tokenDoLink").ValueKind);
        Assert.Null(await TextoAsync(api, $"SELECT LINK_PUBLICO_CODIGO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));

        var outroCodigo = (await CorpoAsync(await PublicarAsync(api, id, Corpo("LINK", agendarPara: "2026-11-03", prazo: "2026-11-17")))).GetProperty("publicacao").GetProperty("tokenDoLink").GetString();
        Assert.NotEqual(codigo, outroCodigo);
    }

    [Fact]
    public async Task So_o_agendado_volta_a_rascunho_os_outros_sao_422_FORMULARIO_NAO_ESTA_AGENDADO_e_nada_muda()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("Rascunho"));
        var publicado = await SemearAsync(api, Titulo("Publicado"), "PUBLICADO", alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");

        var respostas = new[] { await RascunhoAsync(api, rascunho), await RascunhoAsync(api, publicado), await RascunhoAsync(api, encerrado) };

        foreach (var r in respostas)
        {
            var problema = await CorpoAsync(r);
            Assert.Equal((422, "FORMULARIO_NAO_ESTA_AGENDADO", "Só um formulário agendado volta para rascunho."), ((int)r.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        }

        await respostas[1].AfirmarConformeAoContratoAsync("voltarFormularioParaRascunho");
        Assert.Equal("True", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {publicado}"));
        Assert.Equal(404, (int)(await RascunhoAsync(api, 2147483000)).StatusCode);
    }

    [Fact]
    public async Task Duas_abas_voltando_o_mesmo_agendado_a_rascunho_dao_um_200_e_um_422_e_um_so_evento()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-11-30", agendarPara: "2026-10-20");
        var cliente = await api.ClienteDoRhAgoraAsync();
        var eventosAntes = (await EventosAsync(api, id)).Count;

        var respostas = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => cliente.PostAsync($"{Rota}/{id}/rascunho", content: null)));

        Assert.Equal([200, 422], respostas.Select(r => (int)r.StatusCode).Order().ToArray());
        Assert.Equal(eventosAntes + 1, (await EventosAsync(api, id)).Count);
    }
}
