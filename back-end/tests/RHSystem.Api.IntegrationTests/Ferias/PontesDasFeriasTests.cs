using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>
/// As pontes das férias com o resto do sistema, no banco REAL: "gestor ausente" ligado aprova a etapa do gestor dos pedidos parados nele (menos a sugestão de data), tirar o gestor do setor e trocar a pessoa de setor passam o pedido ao RH, o indicador da ficha é o mesmo saldo da rota de férias
/// e a bolinha do gestor soma férias e ajustes. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PontesDasFeriasTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(PontesDasFeriasTests));

    private static async Task<HttpResponseMessage> AusenciaAsync(ApiDeAcesso api, int gestorId, bool ausente) =>
        await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"/api/rh/gestores/{gestorId}/ausencia", new { ausente, ausenteAte = (string?)null });

    private static string Resultado(ApiDeAcesso api, int feriasId) => TextoAsync(api, $"SELECT COALESCE(RESULTADO_GESTOR, '-') FROM FERIAS WHERE ID_FERIAS = {feriasId}").GetAwaiter().GetResult()!;

    private static string GestorDoPedido(ApiDeAcesso api, int feriasId) => TextoAsync(api, $"SELECT COALESCE(ID_GESTOR, '-') FROM FERIAS WHERE ID_FERIAS = {feriasId}").GetAwaiter().GetResult()!;

    // ---------------------------------------------------------------- gestor ausente

    [Fact]
    public async Task Ligar_o_gestor_ausente_aprova_sozinha_a_etapa_do_gestor_dos_pedidos_parados_nele_menos_a_sugestao_e_devolve_a_contagem()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, m1, m2) = await EquipeAsync(api, "Ausencia");
        var (_, _, outroGestorId, deOutroSetor, _) = await EquipeAsync(api, "Ausencia alheia");
        var parado1 = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId, observacao: "Queria emendar.");
        var parado2 = await PedidoComOGestorAsync(api, m2.FuncionarioId, gestorId, "2026-12-07", "2026-12-21");
        var emSugestao = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-02-15", sugestaoFim: "2027-03-01");
        var jaComORh = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2027-04-12", "2027-04-26", "SOLICITADA", gestorId: gestorId, gestorAprovou: true);
        var deOutroGestor = await PedidoComOGestorAsync(api, deOutroSetor.FuncionarioId, outroGestorId);

        var resposta = await AusenciaAsync(api, gestorId, ausente: true);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(2, (await CorpoAsync(resposta)).GetProperty("pedidosAprovadosAgora").GetProperty("ferias").GetInt32());
        foreach (var id in new[] { parado1, parado2 })
        {
            Assert.Equal(("APROVOU", "1", "SOLICITADA", "2026-10-07 09:42:00"), (
                Resultado(api, id),
                await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA + 0 FROM FERIAS WHERE ID_FERIAS = {id}"),
                await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {id}"),
                await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ETAPA_GESTOR, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {id}")));
            Assert.Equal(0, EmailsDoPedido(api, id)); // a aprovação automática não é decisão de ninguém
        }

        Assert.Equal("Queria emendar.", await TextoAsync(api, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {parado1}"));
        Assert.Equal(("SUGERIU_DATA", "0", "-"), (Resultado(api, emSugestao), await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA + 0 FROM FERIAS WHERE ID_FERIAS = {emSugestao}"), Resultado(api, deOutroGestor)));
        Assert.Equal("0", await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA + 0 FROM FERIAS WHERE ID_FERIAS = {jaComORh}"));
        var daFila = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync("/api/rh/ferias?tamanhoPagina=100"));
        Assert.Contains(parado1, daFila.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
        var eventosDoRh = await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, api.Elenco.Rh.FuncionarioId);
        var resumo = Assert.Single(eventosDoRh, e => e is { Area: AuditoriaArea.Ferias } && e.Descricao.Contains("aprovou sozinha", StringComparison.Ordinal));
        Assert.Contains("2 pedido(s) de férias", resumo.Descricao, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- gestor sai do setor e pessoa troca de setor

    [Fact]
    public async Task Tirar_o_gestor_do_setor_passa_ao_RH_os_pedidos_que_esperavam_por_ele_e_so_eles()
    {
        var api = await ApiAsync();
        var (setor, _, gestorId, m1, m2) = await EquipeAsync(api, "Tirar gestor");
        var parado = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId);
        var emSugestao = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-02-15", sugestaoFim: "2027-03-01");
        var jaComORh = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2027-04-12", "2027-04-26", "SOLICITADA", gestorId: gestorId, gestorAprovou: true);
        var nomeDoSetor = await TextoAsync(api, $"SELECT NOME_SETOR FROM SETORES WHERE ID_SETOR = {setor}");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"/api/rh/setores/{setor}", new { nome = nomeDoSetor, descricao = (string?)null, gestorFuncionarioId = (int?)null });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("-", GestorDoPedido(api, parado)); // sem etapa do gestor: só o RH decide
        Assert.Equal(gestorId.ToString(System.Globalization.CultureInfo.InvariantCulture), GestorDoPedido(api, emSugestao)); // a sugestão continua com quem sugeriu
        Assert.Equal(gestorId.ToString(System.Globalization.CultureInfo.InvariantCulture), GestorDoPedido(api, jaComORh));
        var daFila = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync("/api/rh/ferias?tamanhoPagina=100"));
        var item = daFila.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == parado);
        Assert.Equal(("COM_O_RH", "RH", JsonValueKind.Null), (item.GetProperty("situacao").GetString(), item.GetProperty("etapa").GetString(), item.GetProperty("gestorResponsavel").ValueKind));
    }

    [Fact]
    public async Task Trocar_a_pessoa_de_setor_passa_ao_RH_os_pedidos_dela_que_esperavam_o_gestor_antigo_e_nao_os_das_outras_pessoas()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, m1, m2) = await EquipeAsync(api, "Troca de setor");
        var novoSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, $"Novo setor {Guid.NewGuid():N}");
        var dela = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId);
        var deOutro = await PedidoComOGestorAsync(api, m2.FuncionarioId, gestorId);
        var cargo = await ApoioDeTeste.CriarCargoAsync(api.Conexao, $"Cargo {Guid.NewGuid():N}", null, 44m);
        var email = await TextoAsync(api, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {m1.FuncionarioId}");
        var corpo = new Dictionary<string, object?>
        {
            ["nome"] = m1.Nome,
            ["cpf"] = CpfsDeTeste.Valido(940000 + m1.FuncionarioId),
            ["dataNascimento"] = "1990-06-18",
            ["telefone"] = "11987654410",
            ["emailCorporativo"] = email,
            ["emailPessoal"] = null,
            ["rg"] = null,
            ["rgOrgaoEmissor"] = null,
            ["endereco"] = new Dictionary<string, object?> { ["cep"] = null, ["logradouro"] = null, ["numero"] = null, ["complemento"] = null, ["bairro"] = null, ["cidade"] = null, ["uf"] = null },
            ["dataAdmissao"] = "2022-03-14",
            ["tipoContrato"] = "CLT",
            ["cargoId"] = cargo,
            ["setorId"] = novoSetor,
            ["perfil"] = "Funcionario",
            ["beneficioIds"] = Array.Empty<int>(),
        };

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"/api/rh/funcionarios/{m1.FuncionarioId}", corpo);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("-", GestorDoPedido(api, dela));
        Assert.Equal(gestorId.ToString(System.Globalization.CultureInfo.InvariantCulture), GestorDoPedido(api, deOutro));
    }

    // ---------------------------------------------------------------- indicador da ficha e bolinha do gestor

    [Fact]
    public async Task O_indicador_feriasDisponiveisDias_da_ficha_do_RH_e_da_equipe_e_o_mesmo_saldo_da_rota_de_ferias()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, bruno, _) = await EquipeAsync(api, "Indicador");
        await SemearBrunoAsync(api, bruno.FuncionarioId, gestorId, api.Elenco.Rh.FuncionarioId, com340: true);

        var minhas = await CorpoAsync(await (await api.ClienteAgoraAsync(bruno.UsuarioId)).GetAsync(Rota));
        var doRh = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{bruno.FuncionarioId}/indicadores"));
        var daEquipe = await CorpoAsync(await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync($"/api/gestor/equipe/{bruno.FuncionarioId}/indicadores"));

        Assert.Equal(18, minhas.GetProperty("resumo").GetProperty("diasDisponiveis").GetInt32());
        Assert.Equal((18, 18), (doRh.GetProperty("feriasDisponiveisDias").GetInt32(), daEquipe.GetProperty("feriasDisponiveisDias").GetInt32()));
    }

    [Fact]
    public async Task A_bolinha_do_gestor_soma_as_ferias_COM_O_GESTOR_e_nao_conta_a_sugestao_nem_o_que_ja_esta_com_o_RH()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, m1, m2) = await EquipeAsync(api, "Bolinha");
        var cliente = await api.ClienteAgoraAsync(gestor.UsuarioId);
        var antes = (await CorpoAsync(await cliente.GetAsync("/api/gestor/contadores-do-menu"))).GetProperty("pendenciasDaEquipe").GetInt32();
        _ = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId);
        _ = await PedidoComOGestorAsync(api, m2.FuncionarioId, gestorId, "2026-12-07", "2026-12-21");
        _ = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-02-15", sugestaoFim: "2027-03-01");
        _ = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2027-04-12", "2027-04-26", "SOLICITADA", gestorId: gestorId, gestorAprovou: true);

        var depois = await CorpoAsync(await cliente.GetAsync("/api/gestor/contadores-do-menu"));

        Assert.Equal(0, antes);
        Assert.Equal(2, depois.GetProperty("pendenciasDaEquipe").GetInt32());
        Assert.Equal(0, depois.GetProperty("pedidosDeAjusteDePonto").GetInt32()); // a bolinha do ponto não ganha férias
    }
}
