using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Gestores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// Os limites dos pedidos de ajuste e da correção do RH que os outros testes não pisavam (achados pela mutação do incremento 3b): o menor e o maior valor que ainda vale, o texto do evento de auditoria de cada decisão, o escopo do gestor, a paginação e as corridas
/// em que outra aba decide entre a leitura e a gravação (o decorador do repositório faz a outra decisão de verdade, por outro contexto, bem naquele ponto). Cada teste usa a sua pessoa e o seu dia.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class LimitesDoPedidoDeAjusteTests(MySqlFixture mysql)
{
    private const string Pedidos = "/api/eu/ponto/pedidos-de-ajuste";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(LimitesDoPedidoDeAjusteTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Utc(DateOnly dia, int hora, int minuto = 0, int segundo = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:{segundo:00}Z";

    private static object Item(string tipo, string utc) => new { tipo, instanteUtc = utc, marcacaoId = (int?)null };

    private static object Corpo(DateOnly dia, string justificativa, params object[] itens) => new { data = $"{dia:yyyy-MM-dd}", justificativa, itens };

    private static async Task<HttpResponseMessage> PedirAsync(ApiDeAcesso api, PessoaDeTeste pessoa, object corpo) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(Pedidos, corpo);

    private static async Task<HttpResponseMessage> CorrigirAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia, params object[] marcacoes) =>
        await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"/api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia:yyyy-MM-dd}", new { marcacoes });

    private static object Batida(string tipo, string utc) => new { id = (int?)null, tipo, instanteUtc = utc };

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static async Task<int> GestorIdAsync(ApiDeAcesso api, PessoaDeTeste pessoa) => (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");

    private static async Task<DateTime> AdmissaoAsync(ApiDeAcesso api, int funcionarioId) => Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_ADMISSAO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId}"), System.Globalization.CultureInfo.InvariantCulture);

    private static Task AdmitirEmAsync(ApiDeAcesso api, int funcionarioId, DateTime dia) =>
        ApoioDeTeste.ExecutarAsync(api.Conexao, "UPDATE FUNCIONARIOS SET DATA_ADMISSAO = @dia WHERE ID_FUNCIONARIO = @id", ("@dia", dia.Date), ("@id", funcionarioId));

    // ---------------------------------------------------------------- pedir: o menor e o maior que ainda valem

    [Fact]
    public async Task Justificativa_de_5_e_de_500_caracteres_valem()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB;
        var dia1 = new DateOnly(2026, 9, 21);
        var dia2 = new DateOnly(2026, 9, 22);

        var cinco = await PedirAsync(api, pessoa, Corpo(dia1, "Curta", Item("ENTRADA", Utc(dia1, 11)), Item("SAIDA", Utc(dia1, 20))));
        var quinhentos = await PedirAsync(api, pessoa, Corpo(dia2, new string('x', 500), Item("ENTRADA", Utc(dia2, 11)), Item("SAIDA", Utc(dia2, 20))));

        Assert.Equal((201, 201), ((int)cinco.StatusCode, (int)quinhentos.StatusCode));
    }

    [Fact]
    public async Task Dez_batidas_num_pedido_valem()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 9, 23);
        var itens = Enumerable.Range(0, 10).Select(i => Item(i % 2 == 0 ? "ENTRADA" : "SAIDA", Utc(dia, 10 + i))).ToArray();

        var resposta = await PedirAsync(api, api.Elenco.MembroB, Corpo(dia, "Fiz cinco idas e vindas na obra neste dia.", itens));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(10, (await CorpoAsync(resposta)).GetProperty("itens").GetArrayLength());
    }

    [Fact]
    public async Task O_dia_da_admissao_vale_para_pedir_ajuste()
    {
        var api = await ApiAsync();
        var bruno = api.Elenco.DeOutroSetor;
        var dia = new DateOnly(2026, 9, 24);
        var original = await AdmissaoAsync(api, bruno.FuncionarioId);
        await AdmitirEmAsync(api, bruno.FuncionarioId, dia.ToDateTime(TimeOnly.MinValue));
        try
        {
            var resposta = await PedirAsync(api, bruno, Corpo(dia, "Primeiro dia na empresa, esqueci de bater.", Item("ENTRADA", Utc(dia, 11)), Item("SAIDA", Utc(dia, 20))));

            Assert.Equal(201, (int)resposta.StatusCode);
        }
        finally
        {
            await AdmitirEmAsync(api, bruno.FuncionarioId, original);
        }
    }

    [Fact]
    public async Task Horario_no_instante_exato_de_agora_vale_e_a_batida_no_primeiro_segundo_do_dia_tambem()
    {
        var api = await ApiAsync();
        var ontem = new DateOnly(2026, 10, 6);
        var outroDia = new DateOnly(2026, 9, 25);

        var agora = await PedirAsync(api, api.Elenco.MembroA, Corpo(ontem, "Fiquei até depois do fim do expediente.", Item("ENTRADA", Utc(ontem, 11)), Item("SAIDA", "2026-10-07T12:42:00Z"))); // exatamente agora: não é futuro
        var inicioDoDia = await PedirAsync(api, api.Elenco.MembroB, Corpo(outroDia, "Cheguei à meia-noite no plantão da virada.", Item("ENTRADA", Utc(outroDia, 3)), Item("SAIDA", Utc(outroDia, 20)))); // 00:00:00 em Brasília: o primeiro segundo do dia

        Assert.Equal((201, 201), ((int)agora.StatusCode, (int)inicioDoDia.StatusCode));
    }

    [Fact]
    public async Task Pedido_ja_recusado_do_dia_nao_impede_um_novo_pedido()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB;
        var dia = new DateOnly(2026, 9, 26);
        await PontoDeTeste.NovoPedidoRejeitadoAsync(api.Conexao, julia.FuncionarioId, dia, await GestorIdAsync(api, api.Elenco.Gestor));

        var novo = await PedirAsync(api, julia, Corpo(dia, "Corrigi o horário que o gestor tinha recusado.", Item("ENTRADA", Utc(dia, 11)), Item("SAIDA", Utc(dia, 20))));

        Assert.Equal(201, (int)novo.StatusCode);
    }

    [Fact]
    public async Task A_lista_dos_proprios_pedidos_pagina_de_verdade()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA;
        var gestorId = await GestorIdAsync(api, api.Elenco.Gestor);
        foreach (var dia in new[] { new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 5) })
        {
            await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, diego.FuncionarioId, dia, gestorId);
        }

        var cliente = await api.ClienteAgoraAsync(diego.UsuarioId);
        var todos = (await CorpoAsync(await cliente.GetAsync($"{Pedidos}?tamanhoPagina=100"))).GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();
        var paginas = new List<int>();
        for (var pagina = 1; pagina <= 3; pagina++)
        {
            var itens = (await CorpoAsync(await cliente.GetAsync($"{Pedidos}?tamanhoPagina=1&pagina={pagina}"))).GetProperty("itens").EnumerateArray().ToList();
            paginas.Add(Assert.Single(itens).GetProperty("id").GetInt32());
        }

        Assert.True(todos.Count >= 3);
        Assert.Equal(todos.Take(3), paginas);
    }

    // ---------------------------------------------------------------- a correção direta do RH: o menor e o maior que ainda valem

    [Fact]
    public async Task Vinte_batidas_com_a_primeira_no_primeiro_segundo_do_dia_valem_na_correcao_do_RH()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 7, 6); // segunda-feira
        var marcacoes = Enumerable.Range(0, 20).Select(i => Batida(i % 2 == 0 ? "ENTRADA" : "SAIDA", Utc(dia, 3 + i))).ToArray(); // de 00:00:00 a 19:00 em Brasília

        var resposta = await CorrigirAsync(api, api.Elenco.MembroA.FuncionarioId, dia, marcacoes);

        Assert.Equal(200, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task O_dia_da_admissao_vale_na_correcao_do_RH()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB;
        var dia = new DateOnly(2026, 7, 14);
        var original = await AdmissaoAsync(api, julia.FuncionarioId);
        await AdmitirEmAsync(api, julia.FuncionarioId, dia.ToDateTime(TimeOnly.MinValue));
        try
        {
            var resposta = await CorrigirAsync(api, julia.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)), Batida("SAIDA", Utc(dia, 20)));

            Assert.Equal(200, (int)resposta.StatusCode);
        }
        finally
        {
            await AdmitirEmAsync(api, julia.FuncionarioId, original);
        }
    }

    // ---------------------------------------------------------------- o texto do evento de cada decisão e a observação de 500

    [Fact]
    public async Task O_evento_da_etapa_do_gestor_diz_aprovou_ou_recusou_o_pedido_do_dia_e_a_observacao_de_500_vale()
    {
        var api = await ApiAsync();
        var carlos = api.Elenco.Gestor;
        var gestorId = await GestorIdAsync(api, carlos);
        var paraAprovar = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, new DateOnly(2026, 8, 10), gestorId);
        var paraRecusar = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, new DateOnly(2026, 8, 10), gestorId);
        var cliente = await api.ClienteAgoraAsync(carlos.UsuarioId);

        var aprovou = await cliente.PostAsJsonAsync($"/api/gestor/ponto/pedidos-de-ajuste/{paraAprovar}/aprovar", new { observacao = new string('x', 500) });
        var recusou = await cliente.PostAsJsonAsync($"/api/gestor/ponto/pedidos-de-ajuste/{paraRecusar}/recusar", new { observacao = "Sem a assinatura do cliente." });

        Assert.Equal((200, 200), ((int)aprovou.StatusCode, (int)recusou.StatusCode));
        Assert.Equal(["Aprovou o pedido de ajuste de ponto do dia 10/08/2026 (etapa do gestor)."], (await AuditoriaDeTeste.EventosAsync(api.Conexao, "PONTOS_MANUTENCOES", paraAprovar)).Where(e => e.Acao == RHSystem.Domain.Auditoria.AuditoriaAcao.Aprovou).Select(e => e.Descricao));
        Assert.Equal(["Recusou o pedido de ajuste de ponto do dia 10/08/2026 (etapa do gestor)."], (await AuditoriaDeTeste.EventosAsync(api.Conexao, "PONTOS_MANUTENCOES", paraRecusar)).Where(e => e.Acao == RHSystem.Domain.Auditoria.AuditoriaAcao.Recusou).Select(e => e.Descricao));
    }

    [Fact]
    public async Task O_evento_da_etapa_do_RH_diz_recusou_o_pedido_do_dia()
    {
        var api = await ApiAsync();
        var pedido = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, new DateOnly(2026, 8, 11), await GestorIdAsync(api, api.Elenco.Gestor), gestorJaAprovou: true);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"/api/rh/ponto/pedidos-de-ajuste/{pedido}/recusar", new { observacao = "Sem comprovante." });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(["Recusou o pedido de ajuste de ponto do dia 11/08/2026 (etapa do RH)."], (await AuditoriaDeTeste.EventosAsync(api.Conexao, "PONTOS_MANUTENCOES", pedido)).Where(e => e.Acao == RHSystem.Domain.Auditoria.AuditoriaAcao.Recusou).Select(e => e.Descricao));
    }

    // ---------------------------------------------------------------- o escopo do gestor

    [Fact]
    public async Task Gestor_sem_setor_nao_ve_pedido_nenhum_nem_por_id_e_quem_saiu_da_equipe_some_da_lista_do_gestor_antigo()
    {
        var api = await ApiAsync();
        var carlos = api.Elenco.Gestor;
        var gestorId = await GestorIdAsync(api, carlos);
        var julia = api.Elenco.MembroB;
        var pedido = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, julia.FuncionarioId, new DateOnly(2026, 8, 12), gestorId);

        var semSetor = await api.ClienteAgoraAsync(api.Elenco.GestorSemSetor.UsuarioId);
        var listaSemSetor = await CorpoAsync(await semSetor.GetAsync("/api/gestor/ponto/pedidos-de-ajuste?tamanhoPagina=100"));
        var porIdSemSetor = await semSetor.GetAsync($"/api/gestor/ponto/pedidos-de-ajuste/{pedido}");

        Assert.Equal((0, 0, 404), (listaSemSetor.GetProperty("itens").GetArrayLength(), listaSemSetor.GetProperty("total").GetInt32(), (int)porIdSemSetor.StatusCode));

        var setorOriginal = await Sql.ContarAsync(api.Conexao, $"SELECT ID_SETOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {julia.FuncionarioId}");
        var setorDeBruno = await Sql.ContarAsync(api.Conexao, $"SELECT ID_SETOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {api.Elenco.DeOutroSetor.FuncionarioId}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setorDeBruno} WHERE ID_FUNCIONARIO = {julia.FuncionarioId}");
        try
        {
            var doCarlos = await api.ClienteAgoraAsync(carlos.UsuarioId);
            var lista = await CorpoAsync(await doCarlos.GetAsync("/api/gestor/ponto/pedidos-de-ajuste?tamanhoPagina=100"));
            var porId = await doCarlos.GetAsync($"/api/gestor/ponto/pedidos-de-ajuste/{pedido}");

            Assert.DoesNotContain(pedido, lista.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32())); // a pessoa mudou de setor: o gestor antigo não enxerga mais (a dívida conhecida)
            Assert.Equal(404, (int)porId.StatusCode);
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setorOriginal} WHERE ID_FUNCIONARIO = {julia.FuncionarioId}");
        }
    }

    // ---------------------------------------------------------------- outra aba decide no meio: o 409 certo

    [Fact]
    public async Task Gestor_que_perde_a_corrida_para_outra_decisao_recebe_PEDIDO_JA_DECIDIDO()
    {
        var api = await ApiAsync();
        var carlos = api.Elenco.Gestor;
        var gestorId = await GestorIdAsync(api, carlos);
        var pedido = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, new DateOnly(2026, 8, 13), gestorId);
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IPedidosDeAjusteRepositorio>();
            servicos.AddScoped<IPedidosDeAjusteRepositorio>(sp => new DecideAntesNoGestor(ActivatorUtilities.CreateInstance<PedidosDeAjusteRepositorio>(sp), api.Api.Services, gestorId));
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(carlos.UsuarioId)).DefaultRequestHeaders.Authorization;

        var resposta = await cliente.PostAsJsonAsync($"/api/gestor/ponto/pedidos-de-ajuste/{pedido}/aprovar", new { observacao = "De acordo." });

        Assert.Equal(409, (int)resposta.StatusCode);
        Assert.Equal("PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(resposta))); // a outra aba recusou: o pedido está concluído, não "na etapa errada"
    }

    [Fact]
    public async Task RH_que_perde_a_corrida_para_outra_decisao_recebe_PEDIDO_JA_DECIDIDO()
    {
        var api = await ApiAsync();
        var gestorId = await GestorIdAsync(api, api.Elenco.Gestor);
        var pedido = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, new DateOnly(2026, 8, 14), gestorId, gestorJaAprovou: true);
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IConsultaDePedidosDeAjuste>();
            servicos.AddScoped<IConsultaDePedidosDeAjuste>(sp => new RhDecideNaSegundaLeitura(ActivatorUtilities.CreateInstance<ConsultaDePedidosDeAjuste>(sp), api.Api.Services, api.Elenco.Rh.FuncionarioId));
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;

        var resposta = await cliente.PostAsJsonAsync($"/api/rh/ponto/pedidos-de-ajuste/{pedido}/recusar", new { observacao = "Segunda aba." });

        Assert.Equal(409, (int)resposta.StatusCode);
        Assert.Equal("PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(resposta))); // a outra aba já recusou entre a leitura e a trava da pessoa
    }

    /// <summary>O repositório de verdade, que antes de gravar a decisão do gestor faz a decisão de OUTRA aba (recusa o pedido por outro contexto): a gravação do gestor acha zero linhas.</summary>
    private sealed class DecideAntesNoGestor(IPedidosDeAjusteRepositorio real, IServiceProvider raiz, int gestorId) : IPedidosDeAjusteRepositorio
    {
        public Task<bool> TemPendenteNoDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct) => real.TemPendenteNoDiaAsync(funcionarioId, dia, ct);

        public Task<int> CriarAsync(int funcionarioId, DateOnly dia, string justificativa, DateTime agoraUtc, EtapaInicialDoPedido etapa, IReadOnlyList<ItemParaAplicar> itens, CancellationToken ct) =>
            real.CriarAsync(funcionarioId, dia, justificativa, agoraUtc, etapa, itens, ct);

        public async Task<bool> DecidirComoGestorAsync(int pedidoId, int gestorIdDaDecisao, bool aprovar, DateTime agoraUtc, string? observacao, CancellationToken ct)
        {
            await using (var escopo = raiz.CreateAsyncScope())
            {
                var outraAba = ActivatorUtilities.CreateInstance<PedidosDeAjusteRepositorio>(escopo.ServiceProvider);
                Assert.True(await outraAba.DecidirComoGestorAsync(pedidoId, gestorId, false, agoraUtc, "Outra aba.", ct));
            }

            return await real.DecidirComoGestorAsync(pedidoId, gestorIdDaDecisao, aprovar, agoraUtc, observacao, ct);
        }

        public Task<int?> GestorIdDaPessoaAsync(int funcionarioId, CancellationToken ct) => real.GestorIdDaPessoaAsync(funcionarioId, ct);

        public Task<bool> DecidirComoRhAsync(int pedidoId, bool aprovar, int funcionarioRhId, int? gestorIdDoRh, DateTime agoraUtc, string? observacao, CancellationToken ct) =>
            real.DecidirComoRhAsync(pedidoId, aprovar, funcionarioRhId, gestorIdDoRh, agoraUtc, observacao, ct);

        public Task AplicarBatidasAsync(int funcionarioId, DateOnly dia, IReadOnlyList<BatidaDepoisDeAplicar> batidas, DateTime agoraUtc, CancellationToken ct) => real.AplicarBatidasAsync(funcionarioId, dia, batidas, agoraUtc, ct);

        public Task<string?> EmailCorporativoAsync(int funcionarioId, CancellationToken ct) => real.EmailCorporativoAsync(funcionarioId, ct);
    }

    /// <summary>A consulta de verdade, que na SEGUNDA leitura (a de dentro da transação do RH, já com a pessoa travada) faz antes a decisão de OUTRA aba (o RH recusa o pedido por outro contexto).</summary>
    private sealed class RhDecideNaSegundaLeitura(IConsultaDePedidosDeAjuste real, IServiceProvider raiz, int rhId) : IConsultaDePedidosDeAjuste
    {
        private int _leituras;

        public async Task<PedidoLido?> ObterAsync(int pedidoId, EscopoDePedidos escopo, CancellationToken ct)
        {
            if (++_leituras == 2)
            {
                await using var escopoDaOutraAba = raiz.CreateAsyncScope();
                var outraAba = ActivatorUtilities.CreateInstance<PedidosDeAjusteRepositorio>(escopoDaOutraAba.ServiceProvider);
                var gestorId = await outraAba.GestorIdDaPessoaAsync(rhId, ct);
                Assert.True(await outraAba.DecidirComoRhAsync(pedidoId, false, rhId, gestorId, ElencoDeTeste.Hoje, "Outra aba.", ct));
            }

            return await real.ObterAsync(pedidoId, escopo, ct);
        }

        public Task<PaginaDePedidos> ListarAsync(EscopoDePedidos escopo, FiltroDePedidos filtro, ParametrosDeLista parametros, CancellationToken ct) => real.ListarAsync(escopo, filtro, parametros, ct);
    }
}
