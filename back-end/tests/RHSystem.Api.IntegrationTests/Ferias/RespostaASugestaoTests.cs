using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>
/// A resposta da pessoa à sugestão de outra data (<c>POST /api/eu/ferias/{feriasId}/sugestao/aceitar</c> e <c>.../recusar</c>), no banco REAL. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). O pedido em <c>SUGESTAO_DE_DATA</c> é de 15 dias (16 a 30 de novembro) e o gestor sugeriu 7 a 21 de dezembro;
/// a sugestão feita em 06/10 vale até 12/10 (cinco dias depois).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class RespostaASugestaoTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(RespostaASugestaoTests));

    private static async Task<HttpResponseMessage> RespostaAsync(ApiDeAcesso api, PessoaDeFerias pessoa, int feriasId, string acao) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsync($"{Rota}/{feriasId}/sugestao/{acao}", content: null);

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    /// <summary>O pedido de 16 a 30 de novembro com a sugestão de 7 a 21 de dezembro esperando a resposta; <paramref name="sugeridaEm"/> define o prazo (cinco dias depois, no dia local).</summary>
    private static Task<int> EmSugestaoAsync(ApiDeAcesso api, int pessoaId, int gestorId, string sugeridaEm = "2026-10-06 10:00:00", string sugestaoInicio = "2026-12-07", string sugestaoFim = "2026-12-21") =>
        SemearAsync(api, pessoaId, "2025-03-14", "2026-03-13", "2026-11-16", "2026-11-30", "SOLICITADA", sugeridaEm, gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: sugestaoInicio, sugestaoFim: sugestaoFim, observacao: "Queria emendar.");

    // ---------------------------------------------------------------- aceitar

    [Fact]
    public async Task Aceitar_troca_as_datas_vira_COM_O_RH_com_a_decisao_do_gestor_e_a_reserva_dos_dias_e_nao_manda_e_mail()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, pessoa, _) = await EquipeAsync(api, "Aceitar");
        var pedido = await EmSugestaoAsync(api, pessoa.FuncionarioId, gestorId);

        var resposta = await RespostaAsync(api, pessoa, pedido, "aceitar");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("aceitarSugestaoDeDataDasFerias");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("2026-12-07", "2026-12-21", 15, "SOLICITADA", "RH", "COM_O_RH", "Aguardando a análise do RH.", "Queria emendar."), (
            corpo.GetProperty("dataInicio").GetString(),
            corpo.GetProperty("dataFim").GetString(),
            corpo.GetProperty("dias").GetInt32(),
            corpo.GetProperty("status").GetString(),
            corpo.GetProperty("etapa").GetString(),
            corpo.GetProperty("situacao").GetString(),
            corpo.GetProperty("motivoNaoPodeDecidir").GetString(),
            corpo.GetProperty("observacao").GetString()));
        var decisao = corpo.GetProperty("decisaoGestor");
        Assert.Equal(("APROVADA", gestor.FuncionarioId, "2026-10-07T12:42:00Z", false), (decisao.GetProperty("decisao").GetString(), decisao.GetProperty("decididaPor").GetProperty("id").GetInt32(), decisao.GetProperty("decididaEmUtc").GetString(), decisao.GetProperty("automatica").GetBoolean()));
        var sugestao = corpo.GetProperty("sugestao");
        Assert.Equal(("ACEITOU", "2026-10-07T12:42:00Z"), (sugestao.GetProperty("respostaDaPessoa").GetString(), sugestao.GetProperty("respondidaEmUtc").GetString()));
        Assert.Equal(("2026-12-07", "2026-12-21", "SUGERIU_DATA", "ACEITOU", "2026-10-07 09:42:00", "2026-12-07", "SOLICITADA"), (
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_INICIO, '%Y-%m-%d') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_FIM, '%Y-%m-%d') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT RESPOSTA_SUGESTAO FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_RESPOSTA_SUGESTAO, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(SUGESTAO_DATA_INICIO, '%Y-%m-%d') FROM FERIAS WHERE ID_FERIAS = {pedido}"), // as sugeridas ficam (o CHECK exige)
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal(0, EmailsDe(api, pessoa.FuncionarioId));

        // os 15 dias das datas sugeridas passam a ficar reservados
        var minhas = await CorpoAsync(await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(Rota));
        var periodo = minhas.GetProperty("periodosAquisitivos").EnumerateArray().Single(p => p.GetProperty("inicio").GetString() == "2025-03-14");
        Assert.Equal((15, 30), (periodo.GetProperty("diasEmPedido").GetInt32(), periodo.GetProperty("diasDisponiveis").GetInt32()));

        // as datas originais só o log guarda
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "FERIAS", pedido));
        Assert.Contains("16/11/2026", evento.Descricao, StringComparison.Ordinal);
        Assert.Contains("07/12/2026", evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain("Queria emendar", evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aceitar_quando_as_datas_sugeridas_ja_nao_cabem_no_saldo_da_422_com_a_lista_e_nada_muda()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, pessoa, _) = await EquipeAsync(api, "Aceitar sem saldo");
        var pedido = await EmSugestaoAsync(api, pessoa.FuncionarioId, gestorId);
        await SemearAsync(api, pessoa.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-27", "SOLICITADA", gestorId: gestorId, gestorAprovou: true); // 20 dias reservados: sobram 10

        var resposta = await RespostaAsync(api, pessoa, pedido, "aceitar");

        Assert.Equal(422, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("aceitarSugestaoDeDataDasFerias");
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("FERIAS_REGRAS_NAO_ATENDIDAS", "Pedido de férias recusado."), (Codigo(problema), problema.GetProperty("title").GetString()));
        Assert.Equal(["CABE_NO_SALDO"], problema.GetProperty("regras").EnumerateArray().Where(r => !r.GetProperty("atendida").GetBoolean()).Select(r => r.GetProperty("codigo").GetString()!));
        Assert.DoesNotContain(problema.GetProperty("regras").EnumerateArray(), r => r.GetProperty("codigo").GetString() == "ANTECEDENCIA_DE_30_DIAS"); // a data foi escolhida pelo gestor
        Assert.Equal(("SUGERIU_DATA", (string?)null, "2026-11-16"), (
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT RESPOSTA_SUGESTAO FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_INICIO, '%Y-%m-%d') FROM FERIAS WHERE ID_FERIAS = {pedido}")));
    }

    // ---------------------------------------------------------------- recusar

    [Fact]
    public async Task Recusar_cancela_o_pedido_com_as_datas_originais_sem_decisao_do_gestor_e_sem_e_mail()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, pessoa, _) = await EquipeAsync(api, "Recusar sugestao");
        var pedido = await EmSugestaoAsync(api, pessoa.FuncionarioId, gestorId);

        var resposta = await RespostaAsync(api, pessoa, pedido, "recusar");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("recusarSugestaoDeDataDasFerias");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("2026-11-16", "2026-11-30", "CANCELADA", "CONCLUIDA", "CANCELADA", "Este pedido já foi decidido.", JsonValueKind.Null, JsonValueKind.Null), (
            corpo.GetProperty("dataInicio").GetString(),
            corpo.GetProperty("dataFim").GetString(),
            corpo.GetProperty("status").GetString(),
            corpo.GetProperty("etapa").GetString(),
            corpo.GetProperty("situacao").GetString(),
            corpo.GetProperty("motivoNaoPodeDecidir").GetString(),
            corpo.GetProperty("decisaoGestor").ValueKind,
            corpo.GetProperty("decisaoRh").ValueKind));
        Assert.Equal("RECUSOU", corpo.GetProperty("sugestao").GetProperty("respostaDaPessoa").GetString());
        Assert.Equal(("CANCELADA", "RECUSOU", "2026-10-07 09:42:00"), (
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT RESPOSTA_SUGESTAO FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_RESPOSTA_SUGESTAO, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal(0, EmailsDe(api, pessoa.FuncionarioId));
    }

    // ---------------------------------------------------------------- escopo, etapa e prazo

    [Fact]
    public async Task Pedido_de_outra_pessoa_ou_que_nao_existe_da_404_nas_duas_rotas()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, dona, outra) = await EquipeAsync(api, "Escopo da resposta");
        var pedido = await EmSugestaoAsync(api, dona.FuncionarioId, gestorId);

        foreach (var acao in new[] { "aceitar", "recusar" })
        {
            var deOutra = await RespostaAsync(api, outra, pedido, acao);
            var inexistente = await RespostaAsync(api, outra, 999999, acao);

            Assert.Equal((404, 404), ((int)deOutra.StatusCode, (int)inexistente.StatusCode));
            await deOutra.AfirmarConformeAoContratoAsync(acao == "aceitar" ? "aceitarSugestaoDeDataDasFerias" : "recusarSugestaoDeDataDasFerias");
        }

        Assert.Equal("SUGERIU_DATA", await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"));
    }

    [Fact]
    public async Task Fora_de_SUGESTAO_DE_DATA_da_409_ETAPA_INCORRETA_e_pedido_terminado_da_409_JA_DECIDIDO_nas_duas_rotas()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, pessoa, _) = await EquipeAsync(api, "Etapa da resposta");
        var rh = api.Elenco.Rh.FuncionarioId;
        var comOGestor = await PedidoComOGestorAsync(api, pessoa.FuncionarioId, gestorId);
        var comORh = await SemearAsync(api, pessoa.FuncionarioId, "2025-03-14", "2026-03-13", "2027-01-11", "2027-01-25", "SOLICITADA", gestorId: gestorId, gestorAprovou: true);
        var aprovada = await SemearAsync(api, pessoa.FuncionarioId, "2025-03-14", "2026-03-13", "2027-03-15", "2027-03-29", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: rh);
        var cancelada = await SemearAsync(api, pessoa.FuncionarioId, "2025-03-14", "2026-03-13", "2027-05-10", "2027-05-24", "CANCELADA", gestorId: gestorId);

        foreach (var acao in new[] { "aceitar", "recusar" })
        {
            foreach (var id in new[] { comOGestor, comORh })
            {
                var resposta = await RespostaAsync(api, pessoa, id, acao);
                Assert.Equal(409, (int)resposta.StatusCode);
                await resposta.AfirmarConformeAoContratoAsync(acao == "aceitar" ? "aceitarSugestaoDeDataDasFerias" : "recusarSugestaoDeDataDasFerias");
                Assert.Equal("PEDIDO_ETAPA_INCORRETA", Codigo(await CorpoAsync(resposta)));
            }

            foreach (var id in new[] { aprovada, cancelada })
            {
                var resposta = await RespostaAsync(api, pessoa, id, acao);
                Assert.Equal(409, (int)resposta.StatusCode);
                Assert.Equal("PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(resposta)));
            }
        }
    }

    [Fact]
    public async Task Depois_do_prazo_a_resposta_expira_o_pedido_e_da_409_JA_DECIDIDO_nas_duas_rotas()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, pessoa, _) = await EquipeAsync(api, "Prazo da sugestao");
        var vencida1 = await EmSugestaoAsync(api, pessoa.FuncionarioId, gestorId, sugeridaEm: "2026-09-28 10:00:00"); // a sugestão é de 29/09: vale até 04/10
        var vencida2 = await EmSugestaoAsync(api, pessoa.FuncionarioId, gestorId, sugeridaEm: "2026-09-28 10:00:00", sugestaoInicio: "2027-02-08", sugestaoFim: "2027-02-22");
        var noUltimoDia = await EmSugestaoAsync(api, pessoa.FuncionarioId, gestorId, sugeridaEm: "2026-10-01 10:00:00", sugestaoInicio: "2027-04-12", sugestaoFim: "2027-04-26"); // a sugestão é de 02/10: vale até 07/10, hoje

        var aceitar = await RespostaAsync(api, pessoa, vencida1, "aceitar");
        var recusar = await RespostaAsync(api, pessoa, vencida2, "recusar");
        var aindaNoPrazo = await RespostaAsync(api, pessoa, noUltimoDia, "aceitar");

        Assert.Equal((409, 409, 200), ((int)aceitar.StatusCode, (int)recusar.StatusCode, (int)aindaNoPrazo.StatusCode));
        Assert.Equal("PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(aceitar)));
        Assert.Equal("PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(recusar)));
        foreach (var id in new[] { vencida1, vencida2 })
        {
            Assert.Equal(("CANCELADA", "EXPIROU"), (await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {id}"), await TextoAsync(api, $"SELECT RESPOSTA_SUGESTAO FROM FERIAS WHERE ID_FERIAS = {id}")));
        }
    }

    // ---------------------------------------------------------------- corrida

    [Fact]
    public async Task Aceitar_contra_recusar_e_aceitar_contra_um_pedido_novo_geram_um_efeito_sem_500_nem_saldo_estourado()
    {
        var api = await ApiAsync();
        var (setor, _, gestorId, _, _) = await EquipeAsync(api, "Corrida da resposta");

        for (var rodada = 0; rodada < 4; rodada++)
        {
            var pessoa = await PessoaAsync(api, "Rodada da resposta", new DateOnly(2025, 3, 14), setor); // um período fechado com 30 dias
            var sugerido = await EmSugestaoAsync(api, pessoa.FuncionarioId, gestorId); // 15 dias sugeridos, ainda não reservados
            var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);

            var aceitarOuRecusar = await Task.WhenAll(cliente.PostAsync($"{Rota}/{sugerido}/sugestao/aceitar", null), cliente.PostAsync($"{Rota}/{sugerido}/sugestao/recusar", null));
            Assert.Equal([200, 409], aceitarOuRecusar.Select(r => (int)r.StatusCode).Order());

            var outra = await PessoaAsync(api, "Rodada do pedido", new DateOnly(2025, 3, 14), setor);
            var sugerido2 = await EmSugestaoAsync(api, outra.FuncionarioId, gestorId);
            var clienteDaOutra = await api.ClienteAgoraAsync(outra.UsuarioId);
            // 15 sugeridos + 20 pedidos = 35 > 30: exatamente um dos dois entra (qualquer ordem)
            var disputa = await Task.WhenAll(clienteDaOutra.PostAsync($"{Rota}/{sugerido2}/sugestao/aceitar", null), clienteDaOutra.PostAsJsonAsync(Rota, Pedido("2027-02-15", "2027-03-06")));
            var statuses = disputa.Select(r => (int)r.StatusCode).Order().ToList();

            Assert.Equal(1, statuses.Count(s => s == 422));
            Assert.Contains(statuses.First(s => s != 422), new[] { 200, 201 });
            var reservados = Contar(api, $"SELECT COALESCE(SUM(DATEDIFF(DATA_FIM, DATA_INICIO) + 1), 0) FROM FERIAS WHERE ID_FUNCIONARIO = {outra.FuncionarioId} AND STATUS_FERIAS = 'SOLICITADA' AND (RESULTADO_GESTOR IS NULL OR RESULTADO_GESTOR <> 'SUGERIU_DATA' OR RESPOSTA_SUGESTAO IS NOT NULL)");
            Assert.True(reservados is 15 or 20, $"reservados = {reservados}");
        }
    }
}
