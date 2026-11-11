using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Emails;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// A etapa do gestor (<c>/api/gestor/ponto/pedidos-de-ajuste</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. A equipe de Carlos Ribeiro (Comercial) é Diego Farias e Júlia Nunes; Bruno Lima é do Financeiro (Marina Costa). Cada teste cria os pedidos
/// pela própria API (o caminho de verdade), cada um num dia só dele, e decide pela rota do gestor.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DecidirComoGestorTests(MySqlFixture mysql)
{
    private const string RotaEu = "/api/eu/ponto/pedidos-de-ajuste";
    private const string RotaGestor = "/api/gestor/ponto/pedidos-de-ajuste";
    private static int _dia = 0;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(DecidirComoGestorTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    /// <summary>Um dia de setembro só deste pedido (1 a 28), com a entrada já batida: o pedido pede a saída.</summary>
    private static async Task<int> PedirAsync(ApiDeAcesso api, PessoaDeTeste pessoa, string justificativa = "Esqueci de bater a saída no fim do dia.")
    {
        var n = Interlocked.Increment(ref _dia) % 28 + 1;
        var data = new DateOnly(2026, 9, n);
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, data, (TipoDeMarcacao.Entrada, $"2026-09-{n:00}T11:00:00Z"));
        var resposta = await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(RotaEu, new { data = data.ToString("yyyy-MM-dd"), justificativa, itens = new[] { new { tipo = "SAIDA", instanteUtc = $"2026-09-{n:00}T20:00:00Z", marcacaoId = (int?)null } } });
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    private static async Task<HttpResponseMessage> DecidirAsync(ApiDeAcesso api, PessoaDeTeste gestor, int pedidoId, string acao, object? corpo = null) =>
        await (await api.ClienteAgoraAsync(gestor.UsuarioId)).PostAsJsonAsync($"{RotaGestor}/{pedidoId}/{acao}", corpo ?? new { });

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    // ---------------------------------------------------------------- a fila

    [Fact]
    public async Task A_fila_do_gestor_so_tem_os_pedidos_da_equipe_dele_e_o_resumo_independe_da_pagina_e_do_filtro()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var deDiego = await PedirAsync(api, e.MembroA);
        var deJulia = await PedirAsync(api, e.MembroB);
        var deBruno = await PedirAsync(api, e.DeOutroSetor); // outro setor
        var doProprioGestor = await PedirAsync(api, e.Gestor); // o gestor pede: vai ao RH
        var cliente = await api.ClienteAgoraAsync(e.Gestor.UsuarioId);

        var fila = await CorpoAsync(await cliente.GetAsync(RotaGestor));
        var umaPorPagina = await CorpoAsync(await cliente.GetAsync($"{RotaGestor}?tamanhoPagina=1"));
        var concluidos = await CorpoAsync(await cliente.GetAsync($"{RotaGestor}?status=APROVADA"));
        var deUmaPessoa = await CorpoAsync(await cliente.GetAsync($"{RotaGestor}?funcionarioId={e.MembroB.FuncionarioId}"));

        var ids = fila.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(deDiego, ids);
        Assert.Contains(deJulia, ids);
        Assert.DoesNotContain(deBruno, ids);
        Assert.DoesNotContain(doProprioGestor, ids);
        var resumo = fila.GetProperty("resumo");
        Assert.Equal(ids.Count, resumo.GetProperty("comGestor").GetInt32() + resumo.GetProperty("comRh").GetInt32() + fila.GetProperty("itens").EnumerateArray().Count(p => p.GetProperty("etapa").GetString() == "CONCLUIDA"));
        Assert.Equal(1, umaPorPagina.GetProperty("itens").GetArrayLength());
        Assert.Equal(resumo.GetProperty("comGestor").GetInt32(), umaPorPagina.GetProperty("resumo").GetProperty("comGestor").GetInt32()); // a página e o filtro não mudam o resumo
        Assert.Equal(0, concluidos.GetProperty("itens").GetArrayLength());
        Assert.Equal(resumo.GetProperty("comGestor").GetInt32(), concluidos.GetProperty("resumo").GetProperty("comGestor").GetInt32());
        var idsDeJulia = deUmaPessoa.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList(); // os outros testes também pediram por ela
        Assert.Contains(deJulia, idsDeJulia);
        Assert.DoesNotContain(deDiego, idsDeJulia);
        Assert.All(deUmaPessoa.GetProperty("itens").EnumerateArray(), p => Assert.Equal(e.MembroB.FuncionarioId, p.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.All(fila.GetProperty("itens").EnumerateArray(), p => Assert.Equal("Comercial", p.GetProperty("funcionario").GetProperty("setor").GetString()));
    }

    [Fact]
    public async Task O_gestor_pode_decidir_o_pedido_da_equipe_na_etapa_dele_e_o_pedido_de_fora_e_404()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var daEquipe = await PedirAsync(api, e.MembroA);
        var deOutraEquipe = await PedirAsync(api, e.DeOutroSetor);
        var doGestor = await PedirAsync(api, e.Gestor);
        var cliente = await api.ClienteAgoraAsync(e.Gestor.UsuarioId);

        var pedido = await CorpoAsync(await cliente.GetAsync($"{RotaGestor}/{daEquipe}"));
        var fora = (int)(await cliente.GetAsync($"{RotaGestor}/{deOutraEquipe}")).StatusCode;
        var proprio = (int)(await cliente.GetAsync($"{RotaGestor}/{doGestor}")).StatusCode;
        var inexistente = (int)(await cliente.GetAsync($"{RotaGestor}/999999")).StatusCode;

        Assert.Equal((true, JsonValueKind.Null, "GESTOR"), (pedido.GetProperty("podeDecidir").GetBoolean(), pedido.GetProperty("motivoNaoPodeDecidir").ValueKind, pedido.GetProperty("etapa").GetString()));
        Assert.Equal((404, 404, 404), (fora, proprio, inexistente)); // o mesmo 404 para o de outra equipe, o do próprio gestor e o que não existe
        Assert.Equal(404, (int)(await DecidirAsync(api, e.Gestor, deOutraEquipe, "aprovar")).StatusCode);
        Assert.Equal(404, (int)(await DecidirAsync(api, e.Gestor, doGestor, "recusar")).StatusCode);
    }

    [Fact]
    public async Task Pedido_de_gente_da_equipe_que_ficou_com_outro_gestor_nao_e_do_gestor_atual()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await PedirAsync(api, e.MembroB);
        var marina = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {e.GestorDoFinanceiro.FuncionarioId}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE PONTOS_MANUTENCOES SET ID_GESTOR = {marina} WHERE ID_MANUTENCAO_PONTO = {id}"); // o pedido ficou com o gestor antigo

        Assert.Equal(404, (int)(await (await api.ClienteAgoraAsync(e.Gestor.UsuarioId)).GetAsync($"{RotaGestor}/{id}")).StatusCode);
        Assert.Equal(404, (int)(await DecidirAsync(api, e.Gestor, id, "aprovar")).StatusCode);
    }

    // ---------------------------------------------------------------- aprovar e recusar

    [Fact]
    public async Task Aprovar_grava_APROVOU_o_pedido_segue_PENDENTE_na_etapa_do_RH_e_nenhuma_batida_muda()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await PedirAsync(api, e.MembroA);
        var batidasAntes = await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {e.MembroA.FuncionarioId}");

        var resposta = await DecidirAsync(api, e.Gestor, id, "aprovar", new { observacao = "  Confirmo: ele saiu às 17:00.  " });

        Assert.Equal(200, (int)resposta.StatusCode);
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("PENDENTE", "RH", false, "Aguardando a análise do RH."), (pedido.GetProperty("status").GetString(), pedido.GetProperty("etapa").GetString(), pedido.GetProperty("podeDecidir").GetBoolean(), pedido.GetProperty("motivoNaoPodeDecidir").GetString()));
        var decisao = pedido.GetProperty("decisaoGestor");
        Assert.Equal(("APROVADA", "Carlos Ribeiro", false, "2026-10-07T12:42:00Z", "Confirmo: ele saiu às 17:00."), (decisao.GetProperty("decisao").GetString(), decisao.GetProperty("decididaPor").GetProperty("nome").GetString(), decisao.GetProperty("automatica").GetBoolean(), decisao.GetProperty("decididaEmUtc").GetString(), decisao.GetProperty("observacao").GetString()));
        Assert.Equal(JsonValueKind.Null, pedido.GetProperty("decisaoRh").ValueKind);
        Assert.Equal(("PENDENTE", "APROVOU"), (await TextoAsync(api, $"SELECT STATUS_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {id}"), await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {id}")));
        Assert.Equal(batidasAntes, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {e.MembroA.FuncionarioId}")); // aprovar na etapa do gestor não muda batida
    }

    [Fact]
    public async Task Recusar_encerra_o_pedido_REJEITADA_CONCLUIDA_sem_decisao_do_RH_e_com_a_data_da_analise_do_relogio()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await PedirAsync(api, e.MembroB);

        var resposta = await DecidirAsync(api, e.Gestor, id, "recusar", new { observacao = "Sem comprovação." });

        Assert.Equal(200, (int)resposta.StatusCode);
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("REJEITADA", "CONCLUIDA", "Este pedido já foi decidido."), (pedido.GetProperty("status").GetString(), pedido.GetProperty("etapa").GetString(), pedido.GetProperty("motivoNaoPodeDecidir").GetString()));
        Assert.Equal(("REJEITADA", "Sem comprovação."), (pedido.GetProperty("decisaoGestor").GetProperty("decisao").GetString(), pedido.GetProperty("decisaoGestor").GetProperty("observacao").GetString()));
        Assert.Equal(JsonValueKind.Null, pedido.GetProperty("decisaoRh").ValueKind);
        Assert.Equal(("RECUSOU", "2026-10-07 09:42:00"), (await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {id}"), Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_ANALISE FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {id}"), System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture))); // a análise vem do relógio da aplicação, não do NOW() do banco
    }

    [Fact]
    public async Task Decidir_de_novo_e_409_PEDIDO_ETAPA_INCORRETA_se_ja_foi_ao_RH_e_PEDIDO_JA_DECIDIDO_se_ja_foi_recusado()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var aprovado = await PedirAsync(api, e.MembroA);
        var recusado = await PedirAsync(api, e.MembroB);
        await DecidirAsync(api, e.Gestor, aprovado, "aprovar");
        await DecidirAsync(api, e.Gestor, recusado, "recusar");

        var aprovarDeNovo = await DecidirAsync(api, e.Gestor, aprovado, "aprovar");
        var recusarOAprovado = await DecidirAsync(api, e.Gestor, aprovado, "recusar");
        var aprovarORecusado = await DecidirAsync(api, e.Gestor, recusado, "aprovar");

        Assert.Equal(409, (int)aprovarDeNovo.StatusCode);
        var etapa = await CorpoAsync(aprovarDeNovo);
        Assert.Equal(("PEDIDO_ETAPA_INCORRETA", "Pedido fora da vez.", "Este pedido não está mais na etapa em que você pode decidir. Atualize a lista."), (Codigo(etapa), etapa.GetProperty("title").GetString(), etapa.GetProperty("detail").GetString()));
        Assert.Equal("PEDIDO_ETAPA_INCORRETA", Codigo(await CorpoAsync(recusarOAprovado)));
        var jaDecidido = await CorpoAsync(aprovarORecusado);
        Assert.Equal((409, "PEDIDO_JA_DECIDIDO", "Este pedido já foi respondido. Atualize a lista."), ((int)aprovarORecusado.StatusCode, Codigo(jaDecidido), jaDecidido.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Duplo_clique_em_paralelo_aprova_uma_vez_so_e_a_outra_e_409_nunca_500()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await PedirAsync(api, e.MembroA);
        var cliente = await api.ClienteAgoraAsync(e.Gestor.UsuarioId);
        var portao = new TaskCompletionSource();

        var tentativas = Enumerable.Range(0, 4).Select(async _ =>
        {
            await portao.Task;
            return await cliente.PostAsJsonAsync($"{RotaGestor}/{id}/aprovar", new { });
        }).ToList();
        portao.SetResult();
        var respostas = await Task.WhenAll(tentativas);

        Assert.Equal([200, 409, 409, 409], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE AREA = 'PONTO' AND ACAO = 'APROVOU' AND ID_REGISTRO = {id}"));
    }

    [Fact]
    public async Task O_UPDATE_condicional_so_vale_uma_vez_e_so_para_o_gestor_do_pedido()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await PedirAsync(api, e.MembroA);
        var carlos = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {e.Gestor.FuncionarioId}");
        var marina = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {e.GestorDoFinanceiro.FuncionarioId}");
        using var escopo = api.Api.Services.CreateScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IPedidosDeAjusteRepositorio>();

        var outroGestor = await repositorio.DecidirComoGestorAsync(id, marina, aprovar: true, ElencoDeTeste.Hoje, null, CancellationToken.None);
        var primeira = await repositorio.DecidirComoGestorAsync(id, carlos, aprovar: true, ElencoDeTeste.Hoje, null, CancellationToken.None);
        var segunda = await repositorio.DecidirComoGestorAsync(id, carlos, aprovar: false, ElencoDeTeste.Hoje, null, CancellationToken.None); // a recusa não vale depois da aprovação

        Assert.Equal((false, true, false), (outroGestor, primeira, segunda));
        Assert.Equal(("PENDENTE", "APROVOU"), (await TextoAsync(api, $"SELECT STATUS_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {id}"), await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {id}")));
    }

    [Fact]
    public async Task Observacao_com_mais_de_500_caracteres_e_400_corpo_ausente_e_400_e_so_o_gestor_decide()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await PedirAsync(api, e.MembroA);

        var longa = await DecidirAsync(api, e.Gestor, id, "aprovar", new { observacao = new string('x', 501) });
        var semCorpo = await (await api.ClienteAgoraAsync(e.Gestor.UsuarioId)).PostAsync($"{RotaGestor}/{id}/aprovar", content: null);
        var dofuncionario = await DecidirAsync(api, e.MembroA, id, "aprovar");
        var doRh = await DecidirAsync(api, e.Rh, id, "aprovar");
        var anonimo = await api.Api.ClienteAnonimo().PostAsJsonAsync($"{RotaGestor}/{id}/aprovar", new { });

        Assert.Equal((400, 400, 403, 403, 401), ((int)longa.StatusCode, (int)semCorpo.StatusCode, (int)dofuncionario.StatusCode, (int)doRh.StatusCode, (int)anonimo.StatusCode));
        Assert.True((await CorpoAsync(longa)).GetProperty("errors").TryGetProperty("observacao", out _));
        Assert.Equal("GESTOR", (await CorpoAsync(await (await api.ClienteAgoraAsync(e.Gestor.UsuarioId)).GetAsync($"{RotaGestor}/{id}"))).GetProperty("etapa").GetString()); // nada foi decidido
    }

    // ---------------------------------------------------------------- e-mail, auditoria e menu

    [Fact]
    public async Task Cada_decisao_enfileira_o_aviso_com_chave_unica_e_o_envio_conta_o_resultado_sem_a_observacao_na_auditoria()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await PedirAsync(api, e.MembroA);
        var antes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'RESULTADO_AJUSTE_PONTO'");
        await using (var anterior = api.Api.Services.CreateAsyncScope())
        {
            await anterior.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None); // o que os outros testes deixaram na fila não conta aqui
        }

        api.Api.Enviador.Limpar();

        await DecidirAsync(api, e.Gestor, id, "aprovar", new { observacao = "Confirmo o horário." });

        Assert.Equal(antes + 1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'RESULTADO_AJUSTE_PONTO'"));
        Assert.Equal($"RESULTADO_AJUSTE_PONTO:pedido={id}:etapa=GESTOR", await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'RESULTADO_AJUSTE_PONTO' AND REFERENCIA_ID = {id}"));
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None);
        }

        var enviada = api.Api.Enviador.Enviadas.Single(m => m.Assunto == "Resposta do seu pedido de ajuste de ponto" && m.Texto.Contains("Diego Farias", StringComparison.Ordinal));
        Assert.Equal(e.MembroA.Email, enviada.Para);
        Assert.Contains("foi aprovado pelo gestor (Carlos Ribeiro)", enviada.Texto, StringComparison.Ordinal);
        Assert.Contains("Ele agora segue para a análise do RH.", enviada.Texto, StringComparison.Ordinal);
        Assert.Contains("Observação: Confirmo o horário.", enviada.Texto, StringComparison.Ordinal);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO LIKE '%Confirmo o horário%'")); // nem a observação nem o destinatário vão para a auditoria
    }

    [Fact]
    public async Task Auditoria_registra_PONTO_APROVOU_ou_RECUSOU_com_o_pedido_e_sem_a_observacao()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var aprovado = await PedirAsync(api, e.MembroA);
        var recusado = await PedirAsync(api, e.MembroB);

        await DecidirAsync(api, e.Gestor, aprovado, "aprovar", new { observacao = "Segredo da observação." });
        await DecidirAsync(api, e.Gestor, recusado, "recusar");

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE AREA = 'PONTO' AND ACAO = 'APROVOU' AND ENTIDADE = 'PONTOS_MANUTENCOES' AND ID_REGISTRO = {aprovado} AND ID_FUNCIONARIO_ATOR = {e.Gestor.FuncionarioId} AND IP IS NOT NULL"));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE AREA = 'PONTO' AND ACAO = 'RECUSOU' AND ENTIDADE = 'PONTOS_MANUTENCOES' AND ID_REGISTRO = {recusado}"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO LIKE '%Segredo da observação%'"));
    }

    [Fact]
    public async Task As_bolinhas_do_menu_do_gestor_contam_os_pedidos_na_etapa_dele_e_somem_quando_ele_decide()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var cliente = await api.ClienteAgoraAsync(e.Gestor.UsuarioId);
        var antes = await CorpoAsync(await cliente.GetAsync("/api/gestor/contadores-do-menu"));
        var id = await PedirAsync(api, e.MembroB);

        var comPedido = await CorpoAsync(await cliente.GetAsync("/api/gestor/contadores-do-menu"));
        await DecidirAsync(api, e.Gestor, id, "aprovar");
        var depois = await CorpoAsync(await cliente.GetAsync("/api/gestor/contadores-do-menu"));

        Assert.Equal(antes.GetProperty("pedidosDeAjusteDePonto").GetInt32() + 1, comPedido.GetProperty("pedidosDeAjusteDePonto").GetInt32());
        Assert.Equal(antes.GetProperty("pendenciasDaEquipe").GetInt32() + 1, comPedido.GetProperty("pendenciasDaEquipe").GetInt32()); // a parte de ajustes das pendências da equipe
        Assert.Equal(antes.GetProperty("pedidosDeAjusteDePonto").GetInt32(), depois.GetProperty("pedidosDeAjusteDePonto").GetInt32()); // aprovou: já é do RH
    }

    [Fact]
    public async Task As_respostas_da_etapa_do_gestor_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var cliente = await api.ClienteAgoraAsync(e.Gestor.UsuarioId);
        var a = await PedirAsync(api, e.MembroA);
        var b = await PedirAsync(api, e.MembroB);

        await (await cliente.GetAsync(RotaGestor)).AfirmarConformeAoContratoAsync("listarPedidosDeAjusteDaEquipe");
        await (await cliente.GetAsync($"{RotaGestor}?etapa=XX")).AfirmarConformeAoContratoAsync("listarPedidosDeAjusteDaEquipe"); // 400
        await (await cliente.GetAsync($"{RotaGestor}/{a}")).AfirmarConformeAoContratoAsync("obterPedidoDeAjusteDaEquipe");
        await (await cliente.GetAsync($"{RotaGestor}/999999")).AfirmarConformeAoContratoAsync("obterPedidoDeAjusteDaEquipe"); // 404
        await (await DecidirAsync(api, e.Gestor, a, "aprovar")).AfirmarConformeAoContratoAsync("aprovarPedidoDeAjusteComoGestor");
        await (await DecidirAsync(api, e.Gestor, a, "aprovar")).AfirmarConformeAoContratoAsync("aprovarPedidoDeAjusteComoGestor"); // 409
        await (await DecidirAsync(api, e.Gestor, b, "recusar", new { observacao = "Não confere." })).AfirmarConformeAoContratoAsync("recusarPedidoDeAjusteComoGestor");
        await (await DecidirAsync(api, e.Gestor, b, "recusar")).AfirmarConformeAoContratoAsync("recusarPedidoDeAjusteComoGestor"); // 409
        await (await DecidirAsync(api, e.Gestor, 999999, "recusar")).AfirmarConformeAoContratoAsync("recusarPedidoDeAjusteComoGestor"); // 404
        await (await DecidirAsync(api, e.MembroA, a, "aprovar")).AfirmarConformeAoContratoAsync("aprovarPedidoDeAjusteComoGestor"); // 403
    }
}
