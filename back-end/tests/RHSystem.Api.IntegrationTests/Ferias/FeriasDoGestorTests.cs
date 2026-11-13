using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>
/// A etapa do gestor nas férias, no banco REAL: <c>GET /api/gestor/ferias</c> e as três decisões (aprovar, recusar, sugerir outra data). Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). Cada teste monta o próprio setor com gestor e dois membros.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FeriasDoGestorTests(MySqlFixture mysql)
{
    private const string Lista = "/api/gestor/ferias";

    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(FeriasDoGestorTests));

    private static async Task<HttpClient> ClienteAsync(ApiDeAcesso api, PessoaDeFerias pessoa) => await api.ClienteAgoraAsync(pessoa.UsuarioId);

    private static Task<HttpResponseMessage> PostAsync(HttpClient cliente, int id, string acao, object? corpo = null) =>
        corpo is null ? cliente.PostAsync($"{Lista}/{id}/{acao}", content: null) : cliente.PostAsJsonAsync($"{Lista}/{id}/{acao}", corpo);

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32())];

    // ---------------------------------------------------------------- listarFeriasDaEquipe

    [Fact]
    public async Task A_lista_padrao_traz_so_os_pedidos_da_equipe_esperando_o_gestor_do_mais_antigo_ao_mais_novo()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, m1, m2) = await EquipeAsync(api, "Lista do gestor");
        var (_, _, outroGestorId, deOutroSetor, _) = await EquipeAsync(api, "Outro setor");
        var maisNovo = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId, solicitadaEm: "2026-10-07 09:00:00");
        var maisAntigo = await PedidoComOGestorAsync(api, m2.FuncionarioId, gestorId, solicitadaEm: "2026-10-06 08:00:00");
        var emSugestao = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2026-12-07", "2026-12-21", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2026-12-14", sugestaoFim: "2026-12-28");
        var comORh = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2027-01-11", "2027-01-25", "SOLICITADA", gestorId: gestorId, gestorAprovou: true);
        _ = await PedidoComOGestorAsync(api, deOutroSetor.FuncionarioId, outroGestorId);
        _ = await SemearAsync(api, gestor.FuncionarioId, "2021-01-02", "2022-01-01", "2026-12-01", "2026-12-15", "SOLICITADA"); // o do próprio gestor nasce sem etapa do gestor
        var cliente = await ClienteAsync(api, gestor);

        var padrao = await cliente.GetAsync(Lista);

        Assert.Equal(200, (int)padrao.StatusCode);
        await padrao.AfirmarConformeAoContratoAsync("listarFeriasDaEquipe");
        var corpo = await CorpoAsync(padrao);
        Assert.Equal([maisAntigo, maisNovo], Ids(corpo));
        Assert.Equal((1, 20, 2, 1), (corpo.GetProperty("pagina").GetInt32(), corpo.GetProperty("tamanhoPagina").GetInt32(), corpo.GetProperty("total").GetInt32(), corpo.GetProperty("totalPaginas").GetInt32()));
        var primeiro = corpo.GetProperty("itens")[0];
        Assert.Equal(("COM_O_GESTOR", "GESTOR", true, JsonValueKind.Null), (primeiro.GetProperty("situacao").GetString(), primeiro.GetProperty("etapa").GetString(), primeiro.GetProperty("podeDecidir").GetBoolean(), primeiro.GetProperty("motivoNaoPodeDecidir").ValueKind));

        Assert.Equal([emSugestao], Ids(await CorpoAsync(await cliente.GetAsync($"{Lista}?situacao=SUGESTAO_DE_DATA"))));
        var sugestao = (await CorpoAsync(await cliente.GetAsync($"{Lista}?situacao=SUGESTAO_DE_DATA"))).GetProperty("itens")[0];
        Assert.Equal((false, $"Esperando a resposta de {m1.Nome} à sugestão de outra data."), (sugestao.GetProperty("podeDecidir").GetBoolean(), sugestao.GetProperty("motivoNaoPodeDecidir").GetString()));
        Assert.Equal([comORh], Ids(await CorpoAsync(await cliente.GetAsync($"{Lista}?situacao=COM_O_RH"))));
        Assert.Empty(Ids(await CorpoAsync(await cliente.GetAsync($"{Lista}?situacao=APROVADA"))));
        var invalida = await cliente.GetAsync($"{Lista}?situacao=XYZ");
        Assert.Equal(400, (int)invalida.StatusCode);
        await invalida.AfirmarConformeAoContratoAsync("listarFeriasDaEquipe");
    }

    [Fact]
    public async Task Gestor_sem_setor_recebe_a_lista_vazia_e_o_funcionario_comum_recebe_403()
    {
        var api = await ApiAsync();

        var semSetor = await (await api.ClienteAgoraAsync(api.Elenco.GestorSemSetor.UsuarioId)).GetAsync(Lista);
        var comum = await (await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId)).GetAsync(Lista);

        Assert.Equal(200, (int)semSetor.StatusCode);
        Assert.Equal(0, (await CorpoAsync(semSetor)).GetProperty("total").GetInt32());
        Assert.Equal(403, (int)comum.StatusCode);
    }

    [Fact]
    public async Task Cada_item_traz_os_conflitos_com_ferias_ja_aprovadas_do_setor_e_quantas_pessoas_ficam_trabalhando()
    {
        var api = await ApiAsync();
        var (setor, gestor, gestorId, m1, m2) = await EquipeAsync(api, "Conflitos");
        var m3 = await PessoaAsync(api, "Membro 3", new DateOnly(2022, 3, 14), setor);
        var m4 = await PessoaAsync(api, "Membro 4", new DateOnly(2022, 3, 14), setor);
        _ = await PessoaAsync(api, "Inativo do setor", new DateOnly(2022, 3, 14), setor, ativa: false);
        var pedido = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId); // 16 a 30 de novembro
        await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-09", "2026-11-23", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: api.Elenco.Rh.FuncionarioId); // 8 dias em comum (16 a 23)
        await SemearAsync(api, m3.FuncionarioId, "2025-03-14", "2026-03-13", "2026-12-14", "2026-12-23", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: api.Elenco.Rh.FuncionarioId); // não coincide
        await SemearAsync(api, m4.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-20", "2026-11-27", "SOLICITADA", gestorId: gestorId); // só pedido: não é conflito

        var resposta = await (await ClienteAsync(api, gestor)).GetAsync(Lista);

        var item = (await CorpoAsync(resposta)).GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == pedido);
        var conflito = Assert.Single(item.GetProperty("conflitos").EnumerateArray());
        Assert.Equal((m2.FuncionarioId, "2026-11-09", "2026-11-23", 8), (conflito.GetProperty("funcionario").GetProperty("id").GetInt32(), conflito.GetProperty("dataInicio").GetString(), conflito.GetProperty("dataFim").GetString(), conflito.GetProperty("diasEmComum").GetInt32()));
        // do setor (gestor, 4 membros ativos): sem quem pede (m1) e sem quem tem férias aprovadas no período (m2) ficam o gestor, m3 e m4
        Assert.Equal(3, item.GetProperty("equipeDisponivel").GetInt32());
    }

    // ---------------------------------------------------------------- aprovar

    [Fact]
    public async Task Aprovar_passa_o_pedido_ao_RH_sem_mudar_o_status_nem_apagar_a_mensagem_e_enfileira_um_e_mail()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Aprovar");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId, observacao: "Queria emendar com o feriado de 20/11.");

        var resposta = await PostAsync(await ClienteAsync(api, gestor), pedido, "aprovar");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("aprovarFeriasComoGestor");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("SOLICITADA", "RH", "COM_O_RH", false, "Aguardando a análise do RH.", "Queria emendar com o feriado de 20/11."), (
            corpo.GetProperty("status").GetString(),
            corpo.GetProperty("etapa").GetString(),
            corpo.GetProperty("situacao").GetString(),
            corpo.GetProperty("podeDecidir").GetBoolean(),
            corpo.GetProperty("motivoNaoPodeDecidir").GetString(),
            corpo.GetProperty("observacao").GetString()));
        var decisao = corpo.GetProperty("decisaoGestor");
        Assert.Equal(("APROVADA", gestor.FuncionarioId, false, "2026-10-07T12:42:00Z", JsonValueKind.Null), (decisao.GetProperty("decisao").GetString(), decisao.GetProperty("decididaPor").GetProperty("id").GetInt32(), decisao.GetProperty("automatica").GetBoolean(), decisao.GetProperty("decididaEmUtc").GetString(), decisao.GetProperty("observacao").ValueKind));
        Assert.Equal(("SOLICITADA", "APROVOU", "2026-10-07 09:42:00", "Queria emendar com o feriado de 20/11."), (
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ETAPA_GESTOR, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal(1, EmailsDoPedido(api, pedido));
        var chave = await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}");
        Assert.Contains($"pedido={pedido}", chave, StringComparison.Ordinal);
        Assert.Contains("GESTOR_APROVOU", chave, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pedido_que_nao_existe_do_proprio_gestor_de_outro_gestor_e_de_outro_setor_dao_o_mesmo_404_e_nada_muda()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, _, _) = await EquipeAsync(api, "Escopo do gestor");
        var (_, _, outroGestorId, deOutroSetor, _) = await EquipeAsync(api, "Escopo alheio");
        var doProprioGestor = await SemearAsync(api, gestor.FuncionarioId, "2021-01-02", "2022-01-01", "2026-12-01", "2026-12-15", "SOLICITADA");
        var doOutroSetor = await PedidoComOGestorAsync(api, deOutroSetor.FuncionarioId, outroGestorId);
        var cliente = await ClienteAsync(api, gestor);

        var respostas = new List<HttpResponseMessage>();
        foreach (var id in new[] { 999999, doProprioGestor, doOutroSetor })
        {
            respostas.Add(await PostAsync(cliente, id, "aprovar"));
            respostas.Add(await PostAsync(cliente, id, "recusar", new { motivo = "Equipe desfalcada." }));
            respostas.Add(await PostAsync(cliente, id, "sugerir-outra-data", new { dataInicio = "2026-12-07", dataFim = "2026-12-21" }));
        }

        Assert.All(respostas, r => Assert.Equal(404, (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("aprovarFeriasComoGestor");
        var corpos = new List<string>();
        foreach (var r in respostas.Where((_, i) => i % 3 == 0))
        {
            var problema = await CorpoAsync(r);
            corpos.Add($"{problema.GetProperty("title").GetString()}|{problema.GetProperty("detail").GetString()}");
        }

        Assert.Single(corpos.Distinct());
        Assert.Equal(("SOLICITADA", null), (await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {doOutroSetor}"), await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {doOutroSetor}")));
        Assert.Equal(0, EmailsDoPedido(api, doOutroSetor) + EmailsDoPedido(api, doProprioGestor));
        Assert.NotEqual(gestorId, outroGestorId);
    }

    [Fact]
    public async Task Decidir_fora_da_vez_da_409_PEDIDO_ETAPA_INCORRETA_e_pedido_terminado_da_409_PEDIDO_JA_DECIDIDO()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Fora da vez");
        var rh = api.Elenco.Rh.FuncionarioId;
        var comORh = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-01-11", "2027-01-25", "SOLICITADA", gestorId: gestorId, gestorAprovou: true);
        var emSugestao = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-02-15", sugestaoFim: "2027-03-01");
        var aprovada = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-03-15", "2027-03-29", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: rh);
        var rejeitada = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-04-12", "2027-04-26", "REJEITADA", gestorId: gestorId, resultadoDoGestor: "RECUSOU", motivo: "Equipe desfalcada.");
        var cancelada = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-05-10", "2027-05-24", "CANCELADA", gestorId: gestorId);
        var cliente = await ClienteAsync(api, gestor);

        foreach (var id in new[] { comORh, emSugestao })
        {
            foreach (var resposta in new[] { await PostAsync(cliente, id, "aprovar"), await PostAsync(cliente, id, "recusar", new { motivo = "Equipe desfalcada." }), await PostAsync(cliente, id, "sugerir-outra-data", new { dataInicio = "2027-06-07", dataFim = "2027-06-21" }) })
            {
                Assert.Equal(409, (int)resposta.StatusCode);
                await resposta.AfirmarConformeAoContratoAsync("aprovarFeriasComoGestor");
                var problema = await CorpoAsync(resposta);
                Assert.Equal(("PEDIDO_ETAPA_INCORRETA", "Pedido na etapa errada."), (Codigo(problema), problema.GetProperty("title").GetString()));
            }
        }

        foreach (var id in new[] { aprovada, rejeitada, cancelada })
        {
            var resposta = await PostAsync(cliente, id, "aprovar");
            Assert.Equal(409, (int)resposta.StatusCode);
            var problema = await CorpoAsync(resposta);
            Assert.Equal(("PEDIDO_JA_DECIDIDO", "Este pedido já foi decidido."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        }

        Assert.Equal(0, EmailsDoPedido(api, comORh) + EmailsDoPedido(api, emSugestao) + EmailsDoPedido(api, aprovada));
    }

    // ---------------------------------------------------------------- recusar

    [Theory]
    [InlineData("Equip", true)] // 5 caracteres
    [InlineData("Equi", false)] // 4
    [InlineData("    ", false)] // só espaços
    [InlineData("  abc  ", false)] // 3 depois de aparar
    public async Task O_motivo_da_recusa_tem_de_5_a_500_caracteres_depois_de_aparado(string motivo, bool vale)
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Motivo");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId);

        var resposta = await PostAsync(await ClienteAsync(api, gestor), pedido, "recusar", new { motivo });

        Assert.Equal(vale ? 200 : 400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("recusarFeriasComoGestor");
        Assert.Equal(vale ? "REJEITADA" : "SOLICITADA", await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"));
    }

    [Fact]
    public async Task O_motivo_de_500_caracteres_vale_o_de_501_e_o_corpo_ausente_dao_400()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Motivo grande");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId);
        var cliente = await ClienteAsync(api, gestor);

        var sem = await PostAsync(cliente, pedido, "recusar", new { });
        var grande = await PostAsync(cliente, pedido, "recusar", new { motivo = new string('m', 501) });
        var limite = await PostAsync(cliente, pedido, "recusar", new { motivo = new string('m', 500) });

        Assert.Equal((400, 400, 200), ((int)sem.StatusCode, (int)grande.StatusCode, (int)limite.StatusCode));
    }

    [Fact]
    public async Task Recusar_encerra_na_hora_com_o_motivo_visivel_a_data_da_analise_e_um_e_mail_com_o_motivo()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Recusar");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId, observacao: "Quero descansar.");

        var resposta = await PostAsync(await ClienteAsync(api, gestor), pedido, "recusar", new { motivo = "  Equipe desfalcada no período.  " });

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("recusarFeriasComoGestor");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("REJEITADA", "CONCLUIDA", "REJEITADA", "Este pedido já foi decidido.", "Quero descansar."), (
            corpo.GetProperty("status").GetString(),
            corpo.GetProperty("etapa").GetString(),
            corpo.GetProperty("situacao").GetString(),
            corpo.GetProperty("motivoNaoPodeDecidir").GetString(),
            corpo.GetProperty("observacao").GetString()));
        var decisao = corpo.GetProperty("decisaoGestor");
        Assert.Equal(("REJEITADA", "Equipe desfalcada no período.", JsonValueKind.Null), (decisao.GetProperty("decisao").GetString(), decisao.GetProperty("observacao").GetString(), corpo.GetProperty("decisaoRh").ValueKind));
        Assert.Equal(("REJEITADA", "RECUSOU", "Equipe desfalcada no período.", "2026-10-07 09:42:00"), (
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT MOTIVO_RECUSA FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ANALISE, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal("Quero descansar.", await TextoAsync(api, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {pedido}"));
        Assert.Equal(1, EmailsDoPedido(api, pedido));
        var carga = await TextoAsync(api, $"SELECT CARGA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}");
        Assert.Contains("Equipe desfalcada no período.", carga, StringComparison.Ordinal);
        Assert.Contains("GESTOR_RECUSOU", await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- sugerir outra data

    [Fact]
    public async Task Sugerir_deixa_o_pedido_esperando_a_resposta_da_pessoa_com_o_prazo_e_enfileira_o_e_mail_com_as_datas()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Sugerir");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId, observacao: "Queria emendar.");

        var resposta = await PostAsync(await ClienteAsync(api, gestor), pedido, "sugerir-outra-data", new { dataInicio = "2026-12-07", dataFim = "2026-12-21" });

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("sugerirOutraDataDasFerias");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("SOLICITADA", "GESTOR", "SUGESTAO_DE_DATA", false, $"Esperando a resposta de {membro.Nome} à sugestão de outra data.", JsonValueKind.Null, "2026-11-16", "2026-11-30"), (
            corpo.GetProperty("status").GetString(),
            corpo.GetProperty("etapa").GetString(),
            corpo.GetProperty("situacao").GetString(),
            corpo.GetProperty("podeDecidir").GetBoolean(),
            corpo.GetProperty("motivoNaoPodeDecidir").GetString(),
            corpo.GetProperty("decisaoGestor").ValueKind,
            corpo.GetProperty("dataInicio").GetString(),
            corpo.GetProperty("dataFim").GetString()));
        var sugestao = corpo.GetProperty("sugestao");
        Assert.Equal(("2026-12-07", "2026-12-21", 15, gestor.FuncionarioId, "2026-10-07T12:42:00Z", JsonValueKind.Null, JsonValueKind.Null, "2026-10-12"), (
            sugestao.GetProperty("dataInicio").GetString(),
            sugestao.GetProperty("dataFim").GetString(),
            sugestao.GetProperty("dias").GetInt32(),
            sugestao.GetProperty("sugeridaPor").GetProperty("id").GetInt32(),
            sugestao.GetProperty("sugeridaEmUtc").GetString(),
            sugestao.GetProperty("respostaDaPessoa").ValueKind,
            sugestao.GetProperty("respondidaEmUtc").ValueKind,
            sugestao.GetProperty("expiraEm").GetString()));
        Assert.Equal(("SUGERIU_DATA", "2026-12-07", "2026-12-21", "Queria emendar."), (
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(SUGESTAO_DATA_INICIO, '%Y-%m-%d') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(SUGESTAO_DATA_FIM, '%Y-%m-%d') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal(1, EmailsDoPedido(api, pedido));
        var carga = await TextoAsync(api, $"SELECT CARGA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}");
        Assert.Contains("2026-12-07", carga, StringComparison.Ordinal);
        Assert.Contains("GESTOR_SUGERIU", await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sugestao_fora_das_regras_da_422_com_a_lista_e_nao_grava_e_sugestao_fora_de_prazo_da_400()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Sugestao invalida");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId);
        var cliente = await ClienteAsync(api, gestor);

        var curta = await PostAsync(cliente, pedido, "sugerir-outra-data", new { dataInicio = "2026-12-07", dataFim = "2026-12-10" }); // 4 dias
        var emFeriadoOuDescanso = await PostAsync(cliente, pedido, "sugerir-outra-data", new { dataInicio = "2026-12-10", dataFim = "2026-12-24" }); // quinta: 2 dias antes do sábado
        var antesDoPeriodo = await PostAsync(cliente, pedido, "sugerir-outra-data", new { dataInicio = "2026-03-09", dataFim = "2026-03-23" }); // antes do fim do período aquisitivo
        var noPassado = await PostAsync(cliente, pedido, "sugerir-outra-data", new { dataInicio = "2026-10-05", dataFim = "2026-10-19" });
        var invertida = await PostAsync(cliente, pedido, "sugerir-outra-data", new { dataInicio = "2026-12-21", dataFim = "2026-12-07" });

        Assert.Equal((422, 422, 400, 400, 400), ((int)curta.StatusCode, (int)emFeriadoOuDescanso.StatusCode, (int)antesDoPeriodo.StatusCode, (int)noPassado.StatusCode, (int)invertida.StatusCode));
        await curta.AfirmarConformeAoContratoAsync("sugerirOutraDataDasFerias");
        await antesDoPeriodo.AfirmarConformeAoContratoAsync("sugerirOutraDataDasFerias");
        var problema = await CorpoAsync(curta);
        Assert.Equal(("FERIAS_REGRAS_NAO_ATENDIDAS", "Sugestão recusada pelas regras.", "DURACAO_DA_PARTE"), (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("regras").EnumerateArray().First(r => !r.GetProperty("atendida").GetBoolean()).GetProperty("codigo").GetString()));
        Assert.DoesNotContain(problema.GetProperty("regras").EnumerateArray(), r => r.GetProperty("codigo").GetString() == "ANTECEDENCIA_DE_30_DIAS"); // o gestor não é barrado pela antecedência
        Assert.Equal(("SOLICITADA", (string?)null), (await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"), await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal(0, EmailsDoPedido(api, pedido));
    }

    // ---------------------------------------------------------------- corrida e e-mail

    [Fact]
    public async Task Dois_cliques_em_aprovar_e_aprovar_contra_sugerir_geram_um_efeito_um_e_mail_e_um_409_sem_deadlock()
    {
        var api = await ApiAsync();
        var (setor, gestor, gestorId, _, _) = await EquipeAsync(api, "Corrida do gestor");
        var cliente = await ClienteAsync(api, gestor);

        // várias rodadas, cada uma com uma pessoa nova (15 + 15 dias cabem nos 30 do período): o deadlock entre "travar a pessoa" e "atualizar o pedido" só aparece quando as duas decisões se cruzam
        for (var rodada = 0; rodada < 8; rodada++)
        {
            var membro = await PessoaAsync(api, "Rodada", new DateOnly(2022, 3, 14), setor);
            var duploClique = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId, "2027-01-11", "2027-01-25");
            var disputa = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId, "2027-03-08", "2027-03-22");

            var cliques = await Task.WhenAll(PostAsync(cliente, duploClique, "aprovar"), PostAsync(cliente, duploClique, "aprovar"));
            var aprovarOuSugerir = await Task.WhenAll(PostAsync(cliente, disputa, "aprovar"), PostAsync(cliente, disputa, "sugerir-outra-data", new { dataInicio = "2027-05-10", dataFim = "2027-05-24" }));

            Assert.Equal([200, 409], cliques.Select(r => (int)r.StatusCode).Order());
            Assert.Equal(1, EmailsDoPedido(api, duploClique));
            Assert.Equal([200, 409], aprovarOuSugerir.Select(r => (int)r.StatusCode).Order());
            Assert.Equal(1, EmailsDoPedido(api, disputa));
            Assert.Contains(await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {disputa}"), new[] { "APROVOU", "SUGERIU_DATA" });
        }
    }

    [Fact]
    public async Task Pessoa_sem_e_mail_corporativo_tem_a_decisao_registrada_e_nada_entra_na_fila()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Sem e-mail");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {membro.FuncionarioId}");

        var resposta = await PostAsync(await ClienteAsync(api, gestor), pedido, "aprovar");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("APROVOU", await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"));
        Assert.Equal(0, EmailsDoPedido(api, pedido));
    }

    // ---------------------------------------------------------------- as bordas que a mutação mostrou

    [Fact]
    public async Task Os_conflitos_pegam_as_ferias_que_terminam_no_primeiro_dia_e_as_que_comecam_no_ultimo_dia_do_pedido()
    {
        var api = await ApiAsync();
        var (setor, gestor, gestorId, m1, m2) = await EquipeAsync(api, "Conflitos nas pontas");
        var m3 = await PessoaAsync(api, "Membro 3 pontas", new DateOnly(2022, 3, 14), setor);
        var pedido = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId); // 16 a 30 de novembro
        await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-10", "2026-11-16", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: api.Elenco.Rh.FuncionarioId); // termina no 1º dia do pedido
        await SemearAsync(api, m3.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-30", "2026-12-04", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: api.Elenco.Rh.FuncionarioId); // começa no último dia do pedido

        var resposta = await (await ClienteAsync(api, gestor)).GetAsync(Lista);

        var item = (await CorpoAsync(resposta)).GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == pedido);
        Assert.Equal([(m2.FuncionarioId, 1), (m3.FuncionarioId, 1)], item.GetProperty("conflitos").EnumerateArray().Select(c => (c.GetProperty("funcionario").GetProperty("id").GetInt32(), c.GetProperty("diasEmComum").GetInt32())));
    }

    [Fact]
    public async Task A_lista_so_traz_o_pedido_que_aponta_para_este_gestor_E_e_de_gente_da_equipe_dele()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, m1, _) = await EquipeAsync(api, "Base da lista");
        var (_, _, outroGestorId, deOutroSetor, _) = await EquipeAsync(api, "Base da lista 2");
        var meu = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId);
        _ = await PedidoComOGestorAsync(api, m1.FuncionarioId, outroGestorId, "2027-02-08", "2027-02-22"); // gente da equipe, mas o pedido aponta para outro gestor
        _ = await PedidoComOGestorAsync(api, deOutroSetor.FuncionarioId, gestorId, "2027-03-08", "2027-03-22"); // pessoa de outro setor, o pedido aponta para este gestor

        var corpo = await CorpoAsync(await (await ClienteAsync(api, gestor)).GetAsync($"{Lista}?tamanhoPagina=100"));

        Assert.Equal([meu], Ids(corpo));
    }

    [Fact]
    public async Task A_sugestao_que_comeca_no_ultimo_dia_do_periodo_aquisitivo_e_recusada_por_isso_e_nao_por_estar_no_passado()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Sugestao no fim do periodo");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId); // período de 14/03/2025 a 13/03/2026

        var resposta = await PostAsync(await ClienteAsync(api, gestor), pedido, "sugerir-outra-data", new { dataInicio = "2026-03-13", dataFim = "2026-03-27" });

        Assert.Equal(400, (int)resposta.StatusCode);
        var erros = (await CorpoAsync(resposta)).GetProperty("errors");
        Assert.Equal(["A sugestão precisa começar depois do fim do período aquisitivo do pedido."], erros.GetProperty("dataInicio").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task A_sugestao_pode_comecar_hoje_mas_nao_ontem()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Sugestao hoje");
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId);
        var cliente = await ClienteAsync(api, gestor);

        var ontem = await PostAsync(cliente, pedido, "sugerir-outra-data", new { dataInicio = "2026-10-06", dataFim = "2026-10-20" });
        var hoje = await PostAsync(cliente, pedido, "sugerir-outra-data", new { dataInicio = "2026-10-07", dataFim = "2026-10-21" });

        Assert.Equal((400, 200), ((int)ontem.StatusCode, (int)hoje.StatusCode));
        Assert.Equal("2026-10-07", await TextoAsync(api, $"SELECT DATE_FORMAT(SUGESTAO_DATA_INICIO, '%Y-%m-%d') FROM FERIAS WHERE ID_FERIAS = {pedido}"));
    }

    [Fact]
    public async Task A_sugestao_confere_o_saldo_sem_contar_o_proprio_pedido_que_esta_sendo_trocado()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Sugestao sem o proprio pedido");
        await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2026-04-13", "2026-04-24", "CONCLUIDA", "2026-02-20 10:10:00", gestorId, true, api.Elenco.Rh.FuncionarioId); // 12 dias tirados: sobram 18
        var pedido = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId); // 15 dias esperando o gestor

        var resposta = await PostAsync(await ClienteAsync(api, gestor), pedido, "sugerir-outra-data", new { dataInicio = "2026-12-07", dataFim = "2026-12-24" }); // 18 dias: cabem só se o pedido de 15 não contar contra si mesmo

        Assert.Equal(200, (int)resposta.StatusCode);
    }
}
