using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Emails;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// A decisão final do RH (<c>/api/rh/ponto/pedidos-de-ajuste</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. Os pedidos nascem pela API (o caminho de verdade) e passam pelo gestor quando o caso pede. Ana Souza é o RH do elenco; quem precisa de um RH
/// com ou sem linha em <c>GESTORES</c>, ou que seja a pessoa do pedido, ganha um RH novo.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DecidirComoRhTests(MySqlFixture mysql)
{
    private const string RotaEu = "/api/eu/ponto/pedidos-de-ajuste";
    private const string RotaGestor = "/api/gestor/ponto/pedidos-de-ajuste";
    private const string RotaRh = "/api/rh/ponto/pedidos-de-ajuste";
    private static int _dia;
    private static int _rh;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(DecidirComoRhTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<PessoaDeTeste> NovoRhAsync(ApiDeAcesso api, bool emGestores = false, int? setorId = null)
    {
        var n = Interlocked.Increment(ref _rh);
        var email = $"rh.decisao.{n}@empresa.com.br";
        await api.Api.CriarUsuarioAsync(email, nivel: "RH");
        var funcionarioId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
        var usuarioId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionarioId}");
        if (setorId is { } setor)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setor} WHERE ID_FUNCIONARIO = {funcionarioId}");
        }

        if (emGestores)
        {
            await api.NovoGestorAsync(funcionarioId);
        }

        return new PessoaDeTeste(funcionarioId, usuarioId, "Pessoa de Teste", email, Perfil.RH);
    }

    private sealed record Semente(int PedidoId, DateOnly Dia, int FuncionarioId);

    /// <summary>
    /// Um pedido de <paramref name="pessoa"/> num dia de agosto só dele. O dia nasce com as batidas <paramref name="batidas"/> (hora cheia em UTC; padrão: a entrada às 11:00) e o pedido pede os <paramref name="itens"/> (padrão: a saída às 20:00).
    /// </summary>
    private static async Task<Semente> PedirAsync(ApiDeAcesso api, PessoaDeTeste pessoa, (TipoDeMarcacao, string)[]? batidas = null, object[]? itens = null, string justificativa = "Esqueci de bater a saída no fim do dia.")
    {
        var n = Interlocked.Increment(ref _dia) % 28 + 1;
        var dia = new DateOnly(2026, 8, n);
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, dia, (batidas ?? [(TipoDeMarcacao.Entrada, $"2026-08-{n:00}T11:00:00Z")]).Select(b => (b.Item1, b.Item2)).ToArray());
        var corpo = new { data = dia.ToString("yyyy-MM-dd"), justificativa, itens = itens ?? [new { tipo = "SAIDA", instanteUtc = $"2026-08-{n:00}T20:00:00Z", marcacaoId = (int?)null }] };
        var resposta = await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(RotaEu, corpo);
        Assert.Equal(201, (int)resposta.StatusCode);
        return new Semente((await CorpoAsync(resposta)).GetProperty("id").GetInt32(), dia, pessoa.FuncionarioId);
    }

    private static string Instante(DateOnly dia, int hora, int minuto = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00Z";

    private static async Task<HttpResponseMessage> PelaEtapaDoGestorAsync(ApiDeAcesso api, int pedidoId, string acao = "aprovar", object? corpo = null) =>
        await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).PostAsJsonAsync($"{RotaGestor}/{pedidoId}/{acao}", corpo ?? new { });

    private static async Task<HttpResponseMessage> DecidirAsync(ApiDeAcesso api, PessoaDeTeste rh, int pedidoId, string acao, object? corpo = null) =>
        await (await api.ClienteAgoraAsync(rh.UsuarioId)).PostAsJsonAsync($"{RotaRh}/{pedidoId}/{acao}", corpo ?? new { });

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static async Task<long> BatidasAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {funcionarioId} AND p.DATA = '{dia:yyyy-MM-dd}'");

    // ---------------------------------------------------------------- aprovar

    [Fact]
    public async Task RH_aprova_o_que_o_gestor_aprovou_aplica_a_batida_como_CORRIGIDA_RH_e_conclui_com_a_observacao_do_RH()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroA);
        await PelaEtapaDoGestorAsync(api, s.PedidoId, "aprovar", new { observacao = "Confirmo o horário." });

        var resposta = await DecidirAsync(api, e.Rh, s.PedidoId, "aprovar", new { observacao = "Conferido com a portaria." });

        Assert.Equal(200, (int)resposta.StatusCode);
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("APROVADA", "CONCLUIDA", false, "Este pedido já foi decidido."), (pedido.GetProperty("status").GetString(), pedido.GetProperty("etapa").GetString(), pedido.GetProperty("podeDecidir").GetBoolean(), pedido.GetProperty("motivoNaoPodeDecidir").GetString()));
        var decisaoRh = pedido.GetProperty("decisaoRh");
        Assert.Equal(("APROVADA", "Ana Souza", false, "2026-10-07T12:42:00Z", "Conferido com a portaria."), (decisaoRh.GetProperty("decisao").GetString(), decisaoRh.GetProperty("decididaPor").GetProperty("nome").GetString(), decisaoRh.GetProperty("automatica").GetBoolean(), decisaoRh.GetProperty("decididaEmUtc").GetString(), decisaoRh.GetProperty("observacao").GetString()));
        Assert.Equal(JsonValueKind.Null, pedido.GetProperty("decisaoGestor").GetProperty("observacao").ValueKind); // a observação do RH substituiu a do gestor
        Assert.Equal("APROVADA", pedido.GetProperty("decisaoGestor").GetProperty("decisao").GetString());
        var saida = pedido.GetProperty("periodosDoDia").EnumerateArray().Single().GetProperty("saida");
        Assert.Equal(("Saída almoço", "CORRIGIDA_RH", Instante(s.Dia, 20), "2026-10-07T12:42:00Z"), (saida.GetProperty("rotulo").GetString(), saida.GetProperty("origem").GetString(), saida.GetProperty("instanteUtc").GetString(), saida.GetProperty("corrigidaEmUtc").GetString())); // o registro atual já traz a correção
        Assert.Equal(2, await BatidasAsync(api, s.FuncionarioId, s.Dia));
        Assert.Equal((e.Rh.FuncionarioId.ToString(), "2026-10-07 09:42:00"), (await TextoAsync(api, $"SELECT ID_FUNCIONARIO_RH FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {s.PedidoId}"), Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_CONFIRMACAO_RH FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {s.PedidoId}"), System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Pedido_com_varias_batidas_uma_corrigida_e_duas_novas_aplica_todas_de_uma_vez()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var julia = e.MembroB;
        var n = Interlocked.Increment(ref _dia) % 28 + 1;
        var dia = new DateOnly(2026, 7, n);
        await PontoDeTeste.BaterEmAsync(api.Conexao, julia.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Instante(dia, 11)), (TipoDeMarcacao.Saida, Instante(dia, 15)));
        var saidaDoAlmoco = (int)await Sql.ContarAsync(api.Conexao, $"SELECT m.ID_MARCACAO FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {julia.FuncionarioId} AND p.DATA = '{dia:yyyy-MM-dd}' AND m.TIPO_MARCACAO = 'SAIDA'");
        var itens = new object[]
        {
            new { tipo = "SAIDA", instanteUtc = Instante(dia, 15, 5), marcacaoId = (int?)saidaDoAlmoco },
            new { tipo = "ENTRADA", instanteUtc = Instante(dia, 16, 5), marcacaoId = (int?)null },
            new { tipo = "SAIDA", instanteUtc = Instante(dia, 20, 50), marcacaoId = (int?)null },
        };
        var resposta = await (await api.ClienteAgoraAsync(julia.UsuarioId)).PostAsJsonAsync(RotaEu, new { data = dia.ToString("yyyy-MM-dd"), justificativa = "Voltei do almoço às 13:05 e saí às 17:50.", itens });
        var id = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        await PelaEtapaDoGestorAsync(api, id);

        var aprovado = await DecidirAsync(api, e.Rh, id, "aprovar");

        Assert.Equal(200, (int)aprovado.StatusCode);
        var batidas = (await CorpoAsync(aprovado)).GetProperty("periodosDoDia").EnumerateArray().SelectMany(p => new[] { p.GetProperty("entrada"), p.GetProperty("saida") }).ToList();
        Assert.Equal([Instante(dia, 11), Instante(dia, 15, 5), Instante(dia, 16, 5), Instante(dia, 20, 50)], batidas.Select(b => b.GetProperty("instanteUtc").GetString()!));
        Assert.Equal(["SISTEMA", "CORRIGIDA_RH", "CORRIGIDA_RH", "CORRIGIDA_RH"], batidas.Select(b => b.GetProperty("origem").GetString()!));
        Assert.Equal(4, await BatidasAsync(api, julia.FuncionarioId, dia));
    }

    [Fact]
    public async Task O_dia_mudou_depois_do_pedido_e_a_sequencia_ficaria_invalida_e_422_e_nada_e_aplicado()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroA);
        await PelaEtapaDoGestorAsync(api, s.PedidoId);
        // Depois do pedido, o RH corrigiu o dia direto e já fechou a saída às 19:00: aplicar o pedido deixaria ENTRADA, SAÍDA, SAÍDA.
        await PontoDeTeste.BaterCorrigidaPeloRhAsync(api.Conexao, s.FuncionarioId, s.Dia, TipoDeMarcacao.Saida, Instante(s.Dia, 19), "2026-10-07T12:00:00Z");

        var resposta = await DecidirAsync(api, e.Rh, s.PedidoId, "aprovar");

        Assert.Equal(422, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("PONTO_SEQUENCIA_INVALIDA", "Com as batidas do pedido o dia fica com uma entrada sem saída. Peça à pessoa um novo pedido."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(2, await BatidasAsync(api, s.FuncionarioId, s.Dia)); // nada aplicado
        Assert.Equal(("PENDENTE", null), (await TextoAsync(api, $"SELECT STATUS_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {s.PedidoId}"), await TextoAsync(api, $"SELECT ID_FUNCIONARIO_RH FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {s.PedidoId}"))); // o pedido continua pendente, intacto
    }

    [Fact]
    public async Task Batida_ja_existente_no_mesmo_horario_e_409_PONTO_BATIDA_DUPLICADA_e_nada_e_aplicado()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroB);
        await PelaEtapaDoGestorAsync(api, s.PedidoId);
        await PontoDeTeste.BaterCorrigidaPeloRhAsync(api.Conexao, s.FuncionarioId, s.Dia, TipoDeMarcacao.Saida, Instante(s.Dia, 20), "2026-10-07T12:00:00Z"); // exatamente o horário pedido

        var resposta = await DecidirAsync(api, e.Rh, s.PedidoId, "aprovar");

        Assert.Equal(409, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("PONTO_BATIDA_DUPLICADA", "Já existe uma batida neste horário. Nada foi alterado."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(2, await BatidasAsync(api, s.FuncionarioId, s.Dia));
        Assert.Equal("PENDENTE", await TextoAsync(api, $"SELECT STATUS_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {s.PedidoId}"));
    }

    // ---------------------------------------------------------------- recusar

    [Fact]
    public async Task RH_recusa_o_que_o_gestor_aprovou_e_nenhuma_batida_muda()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroA);
        await PelaEtapaDoGestorAsync(api, s.PedidoId);

        var resposta = await DecidirAsync(api, e.Rh, s.PedidoId, "recusar", new { observacao = "Fora do prazo." });

        Assert.Equal(200, (int)resposta.StatusCode);
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("REJEITADA", "CONCLUIDA", "APROVADA", "REJEITADA", "Fora do prazo."), (pedido.GetProperty("status").GetString(), pedido.GetProperty("etapa").GetString(), pedido.GetProperty("decisaoGestor").GetProperty("decisao").GetString(), pedido.GetProperty("decisaoRh").GetProperty("decisao").GetString(), pedido.GetProperty("decisaoRh").GetProperty("observacao").GetString()));
        Assert.Equal(1, await BatidasAsync(api, s.FuncionarioId, s.Dia)); // continua só a entrada
    }

    // ---------------------------------------------------------------- fora da vez

    [Fact]
    public async Task Pedido_ainda_na_etapa_do_gestor_e_409_PEDIDO_ETAPA_INCORRETA_com_o_texto_da_rota_e_concluido_e_PEDIDO_JA_DECIDIDO()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var naEtapaDoGestor = await PedirAsync(api, e.MembroA);
        var recusadoPeloGestor = await PedirAsync(api, e.MembroB);
        await PelaEtapaDoGestorAsync(api, recusadoPeloGestor.PedidoId, "recusar");

        var aprovar = await DecidirAsync(api, e.Rh, naEtapaDoGestor.PedidoId, "aprovar");
        var recusar = await DecidirAsync(api, e.Rh, naEtapaDoGestor.PedidoId, "recusar");
        var concluido = await DecidirAsync(api, e.Rh, recusadoPeloGestor.PedidoId, "aprovar");

        var daAprovacao = await CorpoAsync(aprovar);
        Assert.Equal((409, "PEDIDO_ETAPA_INCORRETA", "Este pedido ainda está na etapa do gestor. Espere a análise dele."), ((int)aprovar.StatusCode, Codigo(daAprovacao), daAprovacao.GetProperty("detail").GetString()));
        var daRecusa = await CorpoAsync(recusar);
        Assert.Equal((409, "PEDIDO_ETAPA_INCORRETA", "Este pedido não está mais na etapa em que você pode decidir. Atualize a lista."), ((int)recusar.StatusCode, Codigo(daRecusa), daRecusa.GetProperty("detail").GetString()));
        var jaDecidido = await CorpoAsync(concluido);
        Assert.Equal((409, "PEDIDO_JA_DECIDIDO", "Este pedido já foi respondido. Atualize a lista."), ((int)concluido.StatusCode, Codigo(jaDecidido), jaDecidido.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Duplo_clique_do_RH_em_paralelo_aprova_uma_vez_so_e_a_batida_entra_uma_vez()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroA);
        await PelaEtapaDoGestorAsync(api, s.PedidoId);
        var cliente = await api.ClienteAgoraAsync(e.Rh.UsuarioId);
        var portao = new TaskCompletionSource();

        var tentativas = Enumerable.Range(0, 4).Select(async _ =>
        {
            await portao.Task;
            return await cliente.PostAsJsonAsync($"{RotaRh}/{s.PedidoId}/aprovar", new { });
        }).ToList();
        portao.SetResult();
        var respostas = await Task.WhenAll(tentativas);

        Assert.Equal([200, 409, 409, 409], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(2, await BatidasAsync(api, s.FuncionarioId, s.Dia));
    }

    // ---------------------------------------------------------------- pedido sem etapa do gestor, GESTORES e o próprio pedido

    [Fact]
    public async Task Pedido_sem_etapa_do_gestor_decidido_por_RH_em_GESTORES_grava_o_gestor_dele_sem_virar_gestorResponsavel()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var rh = await NovoRhAsync(api, emGestores: true);
        var s = await PedirAsync(api, e.Gestor); // o gestor pede: o pedido nasce no RH, sem etapa do gestor

        var resposta = await DecidirAsync(api, rh, s.PedidoId, "aprovar", new { observacao = "Ok." });

        Assert.Equal(200, (int)resposta.StatusCode);
        var pedido = await CorpoAsync(resposta);
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, "APROVADA"), (pedido.GetProperty("gestorResponsavel").ValueKind, pedido.GetProperty("decisaoGestor").ValueKind, pedido.GetProperty("decisaoRh").GetProperty("decisao").GetString()));
        Assert.Equal(rh.FuncionarioId, pedido.GetProperty("decisaoRh").GetProperty("decididaPor").GetProperty("id").GetInt32());
        var gestorDoRh = await TextoAsync(api, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {rh.FuncionarioId}");
        Assert.Equal(gestorDoRh, await TextoAsync(api, $"SELECT ID_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {s.PedidoId}")); // o banco exige: o trigger
    }

    [Fact]
    public async Task Pedido_sem_etapa_do_gestor_decidido_por_RH_fora_de_GESTORES_e_422_PEDIDO_RH_FORA_DE_GESTORES_com_a_regra_do_banco()
    {
        var api = await ApiAsync();
        var rh = await NovoRhAsync(api);
        var s = await PedirAsync(api, api.Elenco.GestorSemSetor);

        var aprovar = await DecidirAsync(api, rh, s.PedidoId, "aprovar");
        var recusar = await DecidirAsync(api, rh, s.PedidoId, "recusar");

        foreach (var resposta in new[] { aprovar, recusar })
        {
            Assert.Equal(422, (int)resposta.StatusCode);
            var problema = await CorpoAsync(resposta);
            Assert.Equal(("PEDIDO_RH_FORA_DE_GESTORES", "Informe o gestor responsavel pela analise."), (Codigo(problema), problema.GetProperty("regraDoBanco").GetString()));
        }

        Assert.Equal(1, await BatidasAsync(api, s.FuncionarioId, s.Dia));
    }

    [Fact]
    public async Task A_propria_pessoa_do_RH_nao_decide_o_pedido_sem_etapa_do_gestor_e_PEDIDO_PROPRIO_vence_o_outro_erro()
    {
        var api = await ApiAsync();
        var emGestores = await NovoRhAsync(api, emGestores: true);
        var foraDeGestores = await NovoRhAsync(api);
        var proprioEmGestores = await PedirAsync(api, emGestores);
        var proprioForaDeGestores = await PedirAsync(api, foraDeGestores);

        var a = await DecidirAsync(api, emGestores, proprioEmGestores.PedidoId, "aprovar");
        var b = await DecidirAsync(api, foraDeGestores, proprioForaDeGestores.PedidoId, "aprovar");

        foreach (var resposta in new[] { a, b })
        {
            Assert.Equal(422, (int)resposta.StatusCode);
            var problema = await CorpoAsync(resposta);
            Assert.Equal(("PEDIDO_PROPRIO", "Você não pode decidir o próprio pedido.", "O gestor nao pode analisar a propria solicitacao."), (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("regraDoBanco").GetString()));
        }
    }

    [Fact]
    public async Task O_RH_confirma_o_proprio_pedido_que_passou_pelo_gestor()
    {
        var api = await ApiAsync();
        var rh = await NovoRhAsync(api, setorId: api.Elenco.SetorComercialId); // do Comercial: o pedido dela vai primeiro ao Carlos
        var s = await PedirAsync(api, rh);
        var criado = await CorpoAsync(await (await api.ClienteAgoraAsync(rh.UsuarioId)).GetAsync($"{RotaEu}/{s.PedidoId}"));
        Assert.Equal("GESTOR", criado.GetProperty("etapa").GetString());
        await PelaEtapaDoGestorAsync(api, s.PedidoId);

        var vistoPorEla = await CorpoAsync(await (await api.ClienteAgoraAsync(rh.UsuarioId)).GetAsync($"{RotaRh}/{s.PedidoId}"));
        var resposta = await DecidirAsync(api, rh, s.PedidoId, "aprovar");

        Assert.Equal((true, JsonValueKind.Null), (vistoPorEla.GetProperty("podeDecidir").GetBoolean(), vistoPorEla.GetProperty("motivoNaoPodeDecidir").ValueKind)); // o trigger só compara o ID_GESTOR: ela confirma
        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("APROVADA", (await CorpoAsync(resposta)).GetProperty("status").GetString());
    }

    // ---------------------------------------------------------------- podeDecidir e a fila

    [Fact]
    public async Task Os_cinco_textos_de_motivoNaoPodeDecidir_chegam_pela_rota_do_RH()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var rhEmGestores = await NovoRhAsync(api, emGestores: true);
        var rhSemGestores = await NovoRhAsync(api);
        var naEtapaDoGestor = await PedirAsync(api, e.MembroA);
        var passouPeloGestor = await PedirAsync(api, e.MembroB);
        await PelaEtapaDoGestorAsync(api, passouPeloGestor.PedidoId);
        var semEtapaDoGestor = await PedirAsync(api, e.GestorSemSetor);
        var concluido = await PedirAsync(api, e.DeOutroSetor);
        await (await api.ClienteAgoraAsync(e.GestorDoFinanceiro.UsuarioId)).PostAsJsonAsync($"{RotaGestor}/{concluido.PedidoId}/recusar", new { });
        await DecidirAsync(api, e.Rh, passouPeloGestor.PedidoId, "recusar"); // já decidido: texto de concluído
        var aindaAbertoPeloRh = await PedirAsync(api, e.Gestor); // sem etapa do gestor, aberto

        async Task<(bool, string?)> Ver(PessoaDeTeste quem, int id)
        {
            var pedido = await CorpoAsync(await (await api.ClienteAgoraAsync(quem.UsuarioId)).GetAsync($"{RotaRh}/{id}"));
            return (pedido.GetProperty("podeDecidir").GetBoolean(), pedido.GetProperty("motivoNaoPodeDecidir").GetString());
        }

        Assert.Equal((false, "Aguardando a análise do gestor."), await Ver(rhEmGestores, naEtapaDoGestor.PedidoId));
        Assert.Equal((false, "Este pedido já foi decidido."), await Ver(rhEmGestores, passouPeloGestor.PedidoId));
        Assert.Equal((true, (string?)null), await Ver(rhEmGestores, aindaAbertoPeloRh.PedidoId));
        Assert.Equal((false, "Este pedido não passa pelo gestor e o banco exige que quem decide também esteja cadastrado como gestor. Peça a outra pessoa do RH."), await Ver(rhSemGestores, aindaAbertoPeloRh.PedidoId));
        Assert.Equal((false, "Este pedido já foi decidido."), await Ver(rhEmGestores, concluido.PedidoId));
        Assert.True(semEtapaDoGestor.PedidoId > 0);
    }

    [Fact]
    public async Task A_fila_do_RH_e_a_da_empresa_toda_o_resumo_comRh_e_a_bolinha_do_menu_e_os_filtros_funcionam()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var doComercial = await PedirAsync(api, e.MembroA);
        var doFinanceiro = await PedirAsync(api, e.DeOutroSetor);
        await PelaEtapaDoGestorAsync(api, doComercial.PedidoId);
        var cliente = await api.ClienteAgoraAsync(e.Rh.UsuarioId);

        var fila = await CorpoAsync(await cliente.GetAsync(RotaRh));
        var naEtapaDoRh = await CorpoAsync(await cliente.GetAsync($"{RotaRh}?etapa=RH&tamanhoPagina=100"));
        var daPessoa = await CorpoAsync(await cliente.GetAsync($"{RotaRh}?funcionarioId={e.DeOutroSetor.FuncionarioId}&tamanhoPagina=100"));
        var menu = await CorpoAsync(await cliente.GetAsync("/api/rh/contadores-do-menu"));

        var ids = (await CorpoAsync(await cliente.GetAsync($"{RotaRh}?tamanhoPagina=100"))).GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(doComercial.PedidoId, ids);
        Assert.Contains(doFinanceiro.PedidoId, ids); // de outro setor: o RH vê toda a empresa
        Assert.Equal(naEtapaDoRh.GetProperty("total").GetInt32(), fila.GetProperty("resumo").GetProperty("comRh").GetInt32());
        Assert.Equal(fila.GetProperty("resumo").GetProperty("comRh").GetInt32(), menu.GetProperty("pedidosDeAjusteDePonto").GetInt32());
        Assert.Contains(doComercial.PedidoId, naEtapaDoRh.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        Assert.DoesNotContain(doFinanceiro.PedidoId, naEtapaDoRh.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32())); // este espera a Marina
        Assert.Equal([doFinanceiro.PedidoId], daPessoa.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).Where(i => i == doFinanceiro.PedidoId));
    }

    // ---------------------------------------------------------------- o que sai da decisão

    [Fact]
    public async Task Aprovar_e_recusar_recalculam_o_dia_pela_porta()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var espiao = new RecalculoEspiao();
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IRecalculoDoBancoDeHoras>();
            s.AddSingleton<IRecalculoDoBancoDeHoras>(espiao);
        }));
        var aprovado = await PedirAsync(api, e.MembroA);
        var recusado = await PedirAsync(api, e.MembroB);
        await PelaEtapaDoGestorAsync(api, aprovado.PedidoId);
        await PelaEtapaDoGestorAsync(api, recusado.PedidoId);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(e.Rh.UsuarioId)).DefaultRequestHeaders.Authorization;

        await cliente.PostAsJsonAsync($"{RotaRh}/{aprovado.PedidoId}/aprovar", new { });
        await cliente.PostAsJsonAsync($"{RotaRh}/{recusado.PedidoId}/recusar", new { });

        Assert.Equal([(aprovado.FuncionarioId, aprovado.Dia), (recusado.FuncionarioId, recusado.Dia)], espiao.Chamadas); // o dia com pedido em análise não lança: ao sair do estado pendente, qualquer que seja o resultado, o dia é recalculado
    }

    [Fact]
    public async Task Recusar_o_pedido_de_um_dia_util_sem_batida_lanca_a_falta_que_a_consolidacao_deixou_esperando()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo da recusa {Guid.NewGuid():N}", 44m);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO = {e.MembroA.FuncionarioId}");
        var dia = new DateOnly(2026, 10, 1); // quinta-feira, dia útil, sem nenhuma batida
        await PontoDeTeste.BaterEmAsync(api.Conexao, e.MembroA.FuncionarioId, dia);
        var corpo = new { data = "2026-10-01", justificativa = "Esqueci de bater o dia inteiro.", itens = new object[] { new { tipo = "ENTRADA", instanteUtc = "2026-10-01T11:00:00Z", marcacaoId = (int?)null }, new { tipo = "SAIDA", instanteUtc = "2026-10-01T20:00:00Z", marcacaoId = (int?)null } } };
        var criado = await (await api.ClienteAgoraAsync(e.MembroA.UsuarioId)).PostAsJsonAsync(RotaEu, corpo);
        Assert.Equal(201, (int)criado.StatusCode);
        var pedidoId = (await CorpoAsync(criado)).GetProperty("id").GetInt32();
        await PelaEtapaDoGestorAsync(api, pedidoId);
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IRecalculoDoBancoDeHoras>().RecalcularDiaAsync(e.MembroA.FuncionarioId, dia, CancellationToken.None); // a consolidação do dia: com o pedido em análise a linha antiga espera
        }

        var antes = await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = {e.MembroA.FuncionarioId} AND DIA_REFERENCIA = '2026-10-01'");
        var resposta = await DecidirAsync(api, e.Rh, pedidoId, "recusar");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(0, antes);
        Assert.Equal(("DEBITO", "8.80", "Falta"), (await TextoAsync(api, $"SELECT TIPO_LANCAMENTO FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = {e.MembroA.FuncionarioId} AND DIA_REFERENCIA = '2026-10-01'"), await TextoAsync(api, $"SELECT QUANTIDADE FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = {e.MembroA.FuncionarioId} AND DIA_REFERENCIA = '2026-10-01'"), await TextoAsync(api, $"SELECT OBSERVACAO FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = {e.MembroA.FuncionarioId} AND DIA_REFERENCIA = '2026-10-01'")));
    }

    [Fact]
    public async Task O_RH_aprova_o_pedido_de_quem_foi_inativado_depois_e_a_batida_entra_no_dia()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroB);
        await PelaEtapaDoGestorAsync(api, s.PedidoId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {s.FuncionarioId}");
        try
        {
            var resposta = await DecidirAsync(api, e.Rh, s.PedidoId, "aprovar");

            Assert.Equal(200, (int)resposta.StatusCode); // o contrato não recusa o pedido de inativo (decisão 3 do plano)
            Assert.Equal(("APROVADA", 2L), ((await CorpoAsync(resposta)).GetProperty("status").GetString(), await BatidasAsync(api, s.FuncionarioId, s.Dia)));
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE WHERE ID_FUNCIONARIO = {s.FuncionarioId}");
        }
    }

    private sealed class RecalculoEspiao : IRecalculoDoBancoDeHoras
    {
        public List<(int FuncionarioId, DateOnly Dia)> Chamadas { get; } = [];

        public Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct)
        {
            lock (Chamadas)
            {
                Chamadas.Add((funcionarioId, dia));
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Cada_decisao_do_RH_enfileira_o_aviso_da_etapa_RH_e_a_auditoria_registra_sem_a_observacao()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroA);
        await PelaEtapaDoGestorAsync(api, s.PedidoId);
        await using (var anterior = api.Api.Services.CreateAsyncScope())
        {
            await anterior.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None); // o que os outros testes deixaram na fila não conta aqui
        }

        api.Api.Enviador.Limpar();

        await DecidirAsync(api, e.Rh, s.PedidoId, "aprovar", new { observacao = "Observação reservada do RH." });

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE CHAVE_IDEMPOTENCIA = 'RESULTADO_AJUSTE_PONTO:pedido={s.PedidoId}:etapa=RH'"));
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None);
        }

        var enviada = api.Api.Enviador.Enviadas.Single(m => m.Texto.Contains("foi aprovado pelo RH (Ana Souza)", StringComparison.Ordinal));
        Assert.Contains("As batidas do dia foram corrigidas.", enviada.Texto, StringComparison.Ordinal);
        Assert.Contains("Observação: Observação reservada do RH.", enviada.Texto, StringComparison.Ordinal);
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE AREA = 'PONTO' AND ACAO = 'APROVOU' AND ID_REGISTRO = {s.PedidoId} AND ID_FUNCIONARIO_ATOR = {e.Rh.FuncionarioId}")); // a do gestor tem outro ator
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO LIKE '%Observação reservada%'"));
    }

    [Fact]
    public async Task So_o_RH_decide_e_pedido_inexistente_e_404_e_observacao_longa_e_400()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var s = await PedirAsync(api, e.MembroA);
        await PelaEtapaDoGestorAsync(api, s.PedidoId);

        var gestor = await (await api.ClienteAgoraAsync(e.Gestor.UsuarioId)).PostAsJsonAsync($"{RotaRh}/{s.PedidoId}/aprovar", new { });
        var funcionario = await (await api.ClienteAgoraAsync(e.MembroA.UsuarioId)).GetAsync(RotaRh);
        var inexistente = await DecidirAsync(api, e.Rh, 999999, "aprovar");
        var longa = await DecidirAsync(api, e.Rh, s.PedidoId, "aprovar", new { observacao = new string('x', 501) });
        var semCorpo = await (await api.ClienteAgoraAsync(e.Rh.UsuarioId)).PostAsync($"{RotaRh}/{s.PedidoId}/aprovar", content: null);

        Assert.Equal((403, 403, 404, 400, 400), ((int)gestor.StatusCode, (int)funcionario.StatusCode, (int)inexistente.StatusCode, (int)longa.StatusCode, (int)semCorpo.StatusCode));
        Assert.Equal(1, await BatidasAsync(api, s.FuncionarioId, s.Dia));
    }

    [Fact]
    public async Task As_respostas_da_etapa_do_RH_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var a = await PedirAsync(api, e.MembroA);
        var b = await PedirAsync(api, e.MembroB);
        var semGestor = await PedirAsync(api, e.GestorSemSetor);
        var rhSemGestores = await NovoRhAsync(api);
        await PelaEtapaDoGestorAsync(api, a.PedidoId);
        await PelaEtapaDoGestorAsync(api, b.PedidoId);
        var cliente = await api.ClienteAgoraAsync(e.Rh.UsuarioId);

        await (await cliente.GetAsync(RotaRh)).AfirmarConformeAoContratoAsync("listarPedidosDeAjusteParaRh");
        await (await cliente.GetAsync($"{RotaRh}?etapa=XX")).AfirmarConformeAoContratoAsync("listarPedidosDeAjusteParaRh"); // 400
        await (await cliente.GetAsync($"{RotaRh}/{a.PedidoId}")).AfirmarConformeAoContratoAsync("obterPedidoDeAjusteParaRh");
        await (await cliente.GetAsync($"{RotaRh}/999999")).AfirmarConformeAoContratoAsync("obterPedidoDeAjusteParaRh"); // 404
        await (await DecidirAsync(api, e.Rh, a.PedidoId, "aprovar")).AfirmarConformeAoContratoAsync("aprovarPedidoDeAjusteComoRh");
        await (await DecidirAsync(api, e.Rh, a.PedidoId, "aprovar")).AfirmarConformeAoContratoAsync("aprovarPedidoDeAjusteComoRh"); // 409
        await (await DecidirAsync(api, rhSemGestores, semGestor.PedidoId, "aprovar")).AfirmarConformeAoContratoAsync("aprovarPedidoDeAjusteComoRh"); // 422
        await (await DecidirAsync(api, e.Rh, b.PedidoId, "recusar", new { observacao = "Não confere." })).AfirmarConformeAoContratoAsync("recusarPedidoDeAjusteComoRh");
        await (await DecidirAsync(api, e.Rh, b.PedidoId, "recusar")).AfirmarConformeAoContratoAsync("recusarPedidoDeAjusteComoRh"); // 409
        await (await DecidirAsync(api, rhSemGestores, semGestor.PedidoId, "recusar")).AfirmarConformeAoContratoAsync("recusarPedidoDeAjusteComoRh"); // 422
        await (await DecidirAsync(api, e.MembroA, a.PedidoId, "aprovar")).AfirmarConformeAoContratoAsync("aprovarPedidoDeAjusteComoRh"); // 403
    }
}
