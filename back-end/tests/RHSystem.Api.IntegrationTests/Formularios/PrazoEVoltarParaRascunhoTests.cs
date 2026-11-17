using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
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

    // ---------------------------------------------------------------- a rodada de correção da revisão independente

    private static async Task RecuperarAsync(ApiDeAcesso api, string tipo)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).RecuperarAsync(CancellationToken.None);
    }

    private static Task<long> LembretesPendentes(ApiDeAcesso api, int id) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND REFERENCIA_ID = {id} AND SITUACAO = 'PENDENTE'");

    [Theory]
    [InlineData("2026-10-13", null)] // hoje + 6: o dia do lembrete (06/10) já passou
    [InlineData("2026-10-14", "2026-10-07")] // hoje + 7, publicado em 01/10: o dia do lembrete é hoje
    [InlineData("2026-10-15", "2026-10-08")] // hoje + 8: amanhã
    public async Task O_lembrete_do_prazo_novo_segue_a_mesma_regra_da_recuperacao_e_a_recuperacao_nao_cria_outro(string prazo, string? diaDoLembrete)
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Lembrete no limite"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-31"); // publicado em 01/10, com o lembrete ligado

        var resposta = await PrazoAsync(api, id, new { prazo });
        var depoisDaTroca = await LembretesPendentes(api, id);
        await RecuperarAsync(api, "LEMBRETE_FORMULARIO");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(diaDoLembrete is null ? 0L : 1L, depoisDaTroca);
        Assert.Equal(depoisDaTroca, await LembretesPendentes(api, id)); // a recuperação não acrescenta um lembrete que a troca não agendou (nem esquece o que agendou)
        if (diaDoLembrete is not null)
        {
            Assert.Equal(1L, await Lembretes(api, id, "PENDENTE", diaDoLembrete));
        }
    }

    [Fact]
    public async Task Formulario_recorrente_com_proxima_reabertura_nao_muda_o_prazo_por_aqui_422_FORMULARIO_RECORRENTE_e_nada_muda()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-20", recorrencia: new { intervalo = 1, unidade = "MES" });
        var reaberturaAntes = await TextoAsync(api, $"SELECT PROXIMA_REABERTURA FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");

        var resposta = await PrazoAsync(api, id, new { prazo = "2026-10-25" });

        Assert.NotNull(reaberturaAntes);
        var problema = await CorpoAsync(resposta);
        Assert.Equal((422, "FORMULARIO_RECORRENTE", "Formulário recorrente não muda o prazo por aqui: cada rodada nova repete a duração da anterior. Para mudar, encerre o formulário e crie outro."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("alterarPrazoDoFormulario");
        Assert.Equal(("2026-10-20", reaberturaAntes), (await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT PROXIMA_REABERTURA FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
        Assert.Equal(3L, await PendentesDeQualquerTipo(api, id)); // as tarefas de antes continuam: encerrar, lembrete e reabertura
    }

    [Fact]
    public async Task Recorrencia_que_ja_acabou_sem_proxima_reabertura_deixa_mudar_o_prazo()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-20", recorrencia: new { intervalo = 1, unidade = "MES", ate = "2026-10-31" }); // a próxima (07/11) passaria do limite: não há reabertura
        Assert.Null(await TextoAsync(api, $"SELECT PROXIMA_REABERTURA FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));

        var resposta = await PrazoAsync(api, id, new { prazo = "2026-10-25" });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("2026-10-25", await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Publicado_sem_prazo_ganha_um_e_so_nascem_as_tarefas_novas_com_o_evento_Definiu()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Sem prazo ainda");
        var id = await SemearAsync(api, titulo, "PUBLICADO", alvo: "PESSOAS");

        var resposta = await PrazoAsync(api, id, new { prazo = "2026-11-15" });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((1L, 1L, 0L), (await Encerramentos(api, id, "PENDENTE", "2026-11-16T03:00:00Z"), await Lembretes(api, id, "PENDENTE", "2026-11-08"), await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {id} AND SITUACAO = 'CANCELADA'")));
        var evento = Assert.Single(await EventosAsync(api, id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal($"Definiu o prazo do formulário \"{titulo}\" para 15/11/2026.", evento.Descricao);
    }

    [Fact]
    public async Task Por_link_nao_ha_lembrete_automatico_e_no_anonimo_ha()
    {
        var api = await ApiAsync();
        var porLink = await SemearAsync(api, Titulo("Entrevista por link"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", prazo: "2026-10-31", token: "codigo-do-link-de-teste-0123456789abcdefghijk");
        var anonimo = await SemearAsync(api, Titulo("Pesquisa anônima"), "PUBLICADO", anonimo: true, alvo: "PESSOAS", prazo: "2026-10-31");

        Assert.Equal(200, (int)(await PrazoAsync(api, porLink, new { prazo = "2026-11-15" })).StatusCode);
        Assert.Equal(200, (int)(await PrazoAsync(api, anonimo, new { prazo = "2026-11-15" })).StatusCode);

        Assert.Equal((1L, 0L), (await Encerramentos(api, porLink, "PENDENTE", "2026-11-16T03:00:00Z"), await LembretesPendentes(api, porLink)));
        Assert.Equal((1L, 1L), (await Encerramentos(api, anonimo, "PENDENTE", "2026-11-16T03:00:00Z"), await LembretesPendentes(api, anonimo)));
    }

    [Fact]
    public async Task Duas_abas_mudando_o_prazo_ao_mesmo_tempo_dao_dois_200_e_ficam_so_as_tarefas_do_prazo_que_ficou()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-31");
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(new[] { "2026-11-10", "2026-11-20" }.Select(prazo => cliente.PutAsJsonAsync($"{Rota}/{id}/prazo", new { prazo })));

        Assert.All(respostas, r => Assert.Equal(200, (int)r.StatusCode));
        var final = (await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"))!;
        Assert.Contains(final, new[] { "2026-11-10", "2026-11-20" });
        var nominal = DateOnly.ParseExact(final, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T03:00:00Z";
        Assert.Equal((1L, 2L), (await Encerramentos(api, id, "PENDENTE", nominal), await PendentesDeQualquerTipo(api, id))); // só o encerrar e o lembrete do prazo que ficou
    }

    [Fact]
    public async Task Mudar_o_prazo_ao_mesmo_tempo_que_encerrar_nunca_deixa_tarefa_de_encerrar_pendente()
    {
        var api = await ApiAsync();
        var id = await PublicadoAsync(api, prazo: "2026-10-31");
        var cliente = await api.ClienteDoRhAgoraAsync();

        var prazo = cliente.PutAsJsonAsync($"{Rota}/{id}/prazo", new { prazo = "2026-11-15" });
        var encerrar = cliente.PostAsync($"{Rota}/{id}/encerrar?confirmar=true", content: null);
        var respostas = await Task.WhenAll(prazo, encerrar);

        Assert.Equal(200, (int)respostas[1].StatusCode); // encerrar sempre vale: ou lê o prazo velho e a gravação recusa e ele relê, ou lê o novo
        Assert.Contains((int)respostas[0].StatusCode, new[] { 200, 422 }); // o prazo ou muda antes de encerrar ou encontra o formulário encerrado
        Assert.Equal("False", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'ENCERRAR_FORMULARIO' AND REFERENCIA_ID = {id} AND SITUACAO = 'PENDENTE'"));
    }

    [Fact]
    public async Task O_codigo_antigo_do_link_agendado_vira_404_na_pagina_publica_ao_voltar_a_rascunho()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Entrevista com link antigo"), tipo: "ENTREVISTA");
        var codigo = (await CorpoAsync(await PublicarAsync(api, id, Corpo("LINK", agendarPara: "2026-11-03", prazo: "2026-11-17")))).GetProperty("publicacao").GetProperty("tokenDoLink").GetString()!;

        Assert.Equal(200, (int)(await RascunhoAsync(api, id)).StatusCode);

        Assert.Equal(404, (int)(await api.Api.ClienteAnonimo().GetAsync($"/api/publico/formularios/{codigo}")).StatusCode);
    }

    [Fact]
    public async Task Um_agendado_cujo_dia_ja_passou_e_continua_agendado_volta_a_rascunho_e_a_recuperacao_nao_o_agenda_de_novo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Agendado vencido"), "AGENDADO", alvo: "PESSOAS");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET DATA_PUBLICACAO = '2026-10-05 00:00:00' WHERE ID_FORMULARIO = {id}"); // o dia passou e a tarefa de publicar nunca existiu
        await RecuperarAsync(api, "PUBLICAR_FORMULARIO");
        Assert.True(await PendentesDeQualquerTipo(api, id) > 0, "a recuperação deveria ter agendado uma nova tentativa de publicar");

        var resposta = await RascunhoAsync(api, id);
        await RecuperarAsync(api, "PUBLICAR_FORMULARIO");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("RASCUNHO", (await CorpoAsync(resposta)).GetProperty("situacao").GetString());
        Assert.Equal(0L, await PendentesDeQualquerTipo(api, id)); // sem data de publicação, a recuperação não tem o que agendar
    }
}
