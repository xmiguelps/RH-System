using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>
/// A etapa do RH nas férias, no banco REAL: <c>GET /api/rh/ferias</c>, confirmar e recusar. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). Ana Souza é do RH e tem linha em <c>GESTORES</c> (só para decidir os pedidos que não têm etapa do gestor); o segundo RH criado nos testes não tem.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FeriasDoRhTests(MySqlFixture mysql)
{
    private const string Lista = "/api/rh/ferias";

    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(FeriasDoRhTests));

    private static Task<HttpResponseMessage> PostAsync(HttpClient cliente, int id, string acao, object? corpo = null) =>
        corpo is null ? cliente.PostAsync($"{Lista}/{id}/{acao}", content: null) : cliente.PostAsJsonAsync($"{Lista}/{id}/{acao}", corpo);

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32())];

    private static async Task<int> AnaEmGestoresAsync(ApiDeAcesso api) => await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.Rh.FuncionarioId);

    /// <summary>Um pedido que o gestor já aprovou, esperando o RH.</summary>
    private static Task<int> ComORhAsync(ApiDeAcesso api, int pessoaId, int gestorId, string inicio = "2026-11-16", string fim = "2026-11-30", string solicitadaEm = "2026-10-06 09:00:00", string? observacao = null, bool doElenco = false) =>
        SemearAsync(api, pessoaId, doElenco ? "2026-01-02" : "2025-03-14", doElenco ? "2026-02-01" : "2026-03-13", inicio, fim, "SOLICITADA", solicitadaEm, gestorId, gestorAprovou: true, observacao: observacao);

    /// <summary>Um pedido sem etapa do gestor (de quem é gestor ou de setor sem gestor): <c>ID_GESTOR</c> nulo, direto no RH.</summary>
    private static Task<int> SemEtapaDoGestorAsync(ApiDeAcesso api, int pessoaId, string inicio = "2026-11-16", string fim = "2026-11-30", string solicitadaEm = "2026-10-06 09:00:00", bool doElenco = false) =>
        SemearAsync(api, pessoaId, doElenco ? "2026-01-02" : "2025-03-14", doElenco ? "2026-02-01" : "2026-03-13", inicio, fim, "SOLICITADA", solicitadaEm);

    // ---------------------------------------------------------------- listarFeriasParaORh

    [Fact]
    public async Task A_lista_padrao_traz_os_pedidos_na_etapa_do_RH_de_toda_a_empresa_do_mais_antigo_ao_mais_novo_com_podeDecidir_por_quem_consulta()
    {
        var api = await ApiAsync();
        _ = await AnaEmGestoresAsync(api);
        var (setor, _, gestorId, m1, m2) = await EquipeAsync(api, "Lista do RH");
        var (_, _, outroGestorId, deOutroSetor, _) = await EquipeAsync(api, "Lista do RH 2");
        var maisNovo = await ComORhAsync(api, m1.FuncionarioId, gestorId, solicitadaEm: "2026-10-07 08:00:00");
        var maisAntigo = await ComORhAsync(api, deOutroSetor.FuncionarioId, outroGestorId, solicitadaEm: "2026-10-05 08:00:00");
        var semEtapa = await SemEtapaDoGestorAsync(api, m2.FuncionarioId, solicitadaEm: "2026-10-06 08:00:00");
        var sugestaoAceita = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2027-01-11", "2027-01-25", "SOLICITADA", "2026-10-06 10:00:00", gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-01-18", sugestaoFim: "2027-02-01", respostaDaSugestao: "ACEITOU");
        _ = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId, "2027-04-12", "2027-04-26"); // ainda com o gestor
        _ = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2027-05-10", "2027-05-24", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-05-17", sugestaoFim: "2027-05-31"); // esperando a pessoa
        var cliente = await api.ClienteDoRhAgoraAsync();

        var resposta = await cliente.GetAsync($"{Lista}?tamanhoPagina=100");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarFeriasParaORh");
        var corpo = await CorpoAsync(resposta);
        var ids = Ids(corpo);
        Assert.Equal([maisAntigo, semEtapa, sugestaoAceita, maisNovo], ids.Where(i => new[] { maisAntigo, semEtapa, sugestaoAceita, maisNovo }.Contains(i)));
        Assert.All(corpo.GetProperty("itens").EnumerateArray(), i => Assert.Equal(("COM_O_RH", "RH"), (i.GetProperty("situacao").GetString(), i.GetProperty("etapa").GetString())));
        var primeiro = corpo.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == maisNovo);
        Assert.Equal((true, JsonValueKind.Null), (primeiro.GetProperty("podeDecidir").GetBoolean(), primeiro.GetProperty("motivoNaoPodeDecidir").ValueKind));
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == semEtapa).GetProperty("gestorResponsavel").ValueKind);

        Assert.Equal([sugestaoAceita, maisNovo], Ids(await CorpoAsync(await cliente.GetAsync($"{Lista}?tamanhoPagina=100&busca={Uri.EscapeDataString(m1.Nome)}"))).Where(i => i == sugestaoAceita || i == maisNovo));
        Assert.Equal([semEtapa, sugestaoAceita, maisNovo], Ids(await CorpoAsync(await cliente.GetAsync($"{Lista}?tamanhoPagina=100&setorId={setor}"))));
        Assert.Contains(maisAntigo, Ids(await CorpoAsync(await cliente.GetAsync($"{Lista}?tamanhoPagina=100&situacao=COM_O_RH"))));
        var invalida = await cliente.GetAsync($"{Lista}?situacao=XYZ");
        Assert.Equal(400, (int)invalida.StatusCode);
        await invalida.AfirmarConformeAoContratoAsync("listarFeriasParaORh");
    }

    [Fact]
    public async Task O_pedido_sem_etapa_do_gestor_mostra_podeDecidir_falso_com_o_motivo_certo_para_a_dona_e_para_quem_do_RH_nao_esta_em_GESTORES()
    {
        var api = await ApiAsync();
        _ = await AnaEmGestoresAsync(api);
        var semGestores = await PessoaAsync(api, "RH fora de gestores", new DateOnly(2020, 1, 2), perfil: Perfil.RH);
        var doAna = await SemEtapaDoGestorAsync(api, api.Elenco.Rh.FuncionarioId, doElenco: true);
        var deOutraPessoa = await SemEtapaDoGestorAsync(api, api.Elenco.MembroA.FuncionarioId, doElenco: true);
        var cliente = await api.ClienteDoRhAgoraAsync();
        var clienteSemGestores = await api.ClienteAgoraAsync(semGestores.UsuarioId);

        var paraAna = await CorpoAsync(await cliente.GetAsync($"{Lista}?tamanhoPagina=100"));
        var paraOutro = await CorpoAsync(await clienteSemGestores.GetAsync($"{Lista}?tamanhoPagina=100"));

        var proprio = paraAna.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == doAna);
        var alheioParaAna = paraAna.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == deOutraPessoa);
        var alheioParaOutro = paraOutro.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == deOutraPessoa);
        Assert.Equal((false, "Você não pode decidir o seu próprio pedido."), (proprio.GetProperty("podeDecidir").GetBoolean(), proprio.GetProperty("motivoNaoPodeDecidir").GetString()));
        Assert.True(alheioParaAna.GetProperty("podeDecidir").GetBoolean());
        Assert.False(alheioParaOutro.GetProperty("podeDecidir").GetBoolean());
        Assert.StartsWith("Este pedido não passa pelo gestor e o banco exige", alheioParaOutro.GetProperty("motivoNaoPodeDecidir").GetString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- confirmar

    [Fact]
    public async Task Confirmar_aprova_o_pedido_que_o_gestor_aprovou_com_a_decisao_do_RH_sem_mexer_na_do_gestor_nem_na_mensagem_e_manda_um_e_mail()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Confirmar");
        var pedido = await ComORhAsync(api, membro.FuncionarioId, gestorId, observacao: "Queria emendar com o feriado.");

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), pedido, "confirmar");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("confirmarFeriasComoRh");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("APROVADA", "CONCLUIDA", "APROVADA", false, "Este pedido já foi decidido.", "Queria emendar com o feriado."), (
            corpo.GetProperty("status").GetString(),
            corpo.GetProperty("etapa").GetString(),
            corpo.GetProperty("situacao").GetString(),
            corpo.GetProperty("podeDecidir").GetBoolean(),
            corpo.GetProperty("motivoNaoPodeDecidir").GetString(),
            corpo.GetProperty("observacao").GetString()));
        var rh = corpo.GetProperty("decisaoRh");
        Assert.Equal(("APROVADA", api.Elenco.Rh.FuncionarioId, "2026-10-07T12:42:00Z", JsonValueKind.Null), (rh.GetProperty("decisao").GetString(), rh.GetProperty("decididaPor").GetProperty("id").GetInt32(), rh.GetProperty("decididaEmUtc").GetString(), rh.GetProperty("observacao").ValueKind));
        Assert.Equal(("APROVADA", gestor.FuncionarioId), (corpo.GetProperty("decisaoGestor").GetProperty("decisao").GetString(), corpo.GetProperty("decisaoGestor").GetProperty("decididaPor").GetProperty("id").GetInt32()));
        Assert.Equal(("APROVADA", api.Elenco.Rh.FuncionarioId.ToString(System.Globalization.CultureInfo.InvariantCulture), "2026-10-07 09:42:00", "2026-10-07 09:42:00", "Queria emendar com o feriado.", gestorId.ToString(System.Globalization.CultureInfo.InvariantCulture)), (
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT ID_FUNCIONARIO_RH FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CONFIRMACAO_RH, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ANALISE, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT ID_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal(1, EmailsDoPedido(api, pedido));
        Assert.Contains("RH_CONFIRMOU", await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmar_pedido_sem_etapa_do_gestor_grava_a_linha_de_GESTORES_de_quem_decide_e_nao_a_mostra_como_gestor_responsavel()
    {
        var api = await ApiAsync();
        var anaGestorId = await AnaEmGestoresAsync(api);
        var pessoa = await PessoaAsync(api, "Sem gestor", new DateOnly(2022, 3, 14));
        var pedido = await SemEtapaDoGestorAsync(api, pessoa.FuncionarioId);

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), pedido, "confirmar");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, "APROVADA"), (corpo.GetProperty("gestorResponsavel").ValueKind, corpo.GetProperty("decisaoGestor").ValueKind, corpo.GetProperty("decisaoRh").GetProperty("decisao").GetString()));
        Assert.Equal(anaGestorId.ToString(System.Globalization.CultureInfo.InvariantCulture), await TextoAsync(api, $"SELECT ID_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"));
        Assert.Null(await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido}"));
    }

    [Fact]
    public async Task O_RH_confirma_o_proprio_pedido_quando_a_etapa_do_gestor_foi_feita_por_outra_pessoa()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, _, _) = await EquipeAsync(api, "Pedido do proprio RH");
        _ = await AnaEmGestoresAsync(api);
        var pedido = await ComORhAsync(api, api.Elenco.Rh.FuncionarioId, gestorId, doElenco: true);

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), pedido, "confirmar");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("APROVADA", await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"));
    }

    [Fact]
    public async Task Pedido_sem_etapa_do_gestor_da_422_PEDIDO_PROPRIO_para_a_dona_e_PEDIDO_RH_FORA_DE_GESTORES_para_quem_nao_esta_em_GESTORES_e_nada_muda()
    {
        var api = await ApiAsync();
        _ = await AnaEmGestoresAsync(api);
        var semGestores = await PessoaAsync(api, "RH sem gestores", new DateOnly(2020, 1, 2), perfil: Perfil.RH);
        var doAna = await SemEtapaDoGestorAsync(api, api.Elenco.Rh.FuncionarioId, doElenco: true);
        var deOutra = await SemEtapaDoGestorAsync(api, api.Elenco.MembroB.FuncionarioId, doElenco: true);

        var proprio = await PostAsync(await api.ClienteDoRhAgoraAsync(), doAna, "confirmar");
        var proprioRecusa = await PostAsync(await api.ClienteDoRhAgoraAsync(), doAna, "recusar", new { motivo = "Equipe desfalcada." });
        var fora = await PostAsync(await api.ClienteAgoraAsync(semGestores.UsuarioId), deOutra, "confirmar");

        Assert.Equal((422, 422, 422), ((int)proprio.StatusCode, (int)proprioRecusa.StatusCode, (int)fora.StatusCode));
        await proprio.AfirmarConformeAoContratoAsync("confirmarFeriasComoRh");
        var p1 = await CorpoAsync(proprio);
        Assert.Equal(("PEDIDO_PROPRIO", "Você não pode decidir o próprio pedido.", "O gestor nao pode analisar as proprias ferias."), (Codigo(p1), p1.GetProperty("title").GetString(), p1.GetProperty("regraDoBanco").GetString()));
        var p2 = await CorpoAsync(fora);
        Assert.Equal(("PEDIDO_RH_FORA_DE_GESTORES", "Informe o gestor responsavel pela analise das ferias."), (Codigo(p2), p2.GetProperty("regraDoBanco").GetString()));
        foreach (var id in new[] { doAna, deOutra })
        {
            Assert.Equal(("SOLICITADA", (string?)null), (await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {id}"), await TextoAsync(api, $"SELECT ID_FUNCIONARIO_RH FROM FERIAS WHERE ID_FERIAS = {id}")));
            Assert.Equal(0, EmailsDoPedido(api, id));
        }
    }

    [Fact]
    public async Task Decidir_na_etapa_errada_da_409_antes_de_qualquer_422_e_pedido_terminado_da_409_JA_DECIDIDO_nas_duas_rotas()
    {
        var api = await ApiAsync();
        _ = await AnaEmGestoresAsync(api);
        var semGestores = await PessoaAsync(api, "RH sem gestores 2", new DateOnly(2020, 1, 2), perfil: Perfil.RH);
        var (_, _, gestorId, membro, _) = await EquipeAsync(api, "Etapa do RH");
        var rh = api.Elenco.Rh.FuncionarioId;
        var comOGestor = await PedidoComOGestorAsync(api, membro.FuncionarioId, gestorId);
        var emSugestao = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-02-15", sugestaoFim: "2027-03-01");
        var aprovada = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-03-15", "2027-03-29", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: rh);
        var rejeitada = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-04-12", "2027-04-26", "REJEITADA", gestorId: gestorId, resultadoDoGestor: "RECUSOU", motivo: "Equipe desfalcada.");
        var cancelada = await SemearAsync(api, membro.FuncionarioId, "2025-03-14", "2026-03-13", "2027-05-10", "2027-05-24", "CANCELADA", gestorId: gestorId);
        var clienteForaDeGestores = await api.ClienteAgoraAsync(semGestores.UsuarioId);

        foreach (var cliente in new[] { await api.ClienteDoRhAgoraAsync(), clienteForaDeGestores })
        {
            foreach (var id in new[] { comOGestor, emSugestao })
            {
                foreach (var resposta in new[] { await PostAsync(cliente, id, "confirmar"), await PostAsync(cliente, id, "recusar", new { motivo = "Equipe desfalcada." }) })
                {
                    Assert.Equal(409, (int)resposta.StatusCode);
                    await resposta.AfirmarConformeAoContratoAsync("confirmarFeriasComoRh");
                    Assert.Equal("PEDIDO_ETAPA_INCORRETA", Codigo(await CorpoAsync(resposta)));
                }
            }

            foreach (var id in new[] { aprovada, rejeitada, cancelada })
            {
                var resposta = await PostAsync(cliente, id, "confirmar");
                Assert.Equal(409, (int)resposta.StatusCode);
                Assert.Equal("PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(resposta)));
            }
        }

        Assert.Equal(0, EmailsDoPedido(api, comOGestor) + EmailsDoPedido(api, emSugestao));
        var inexistente = await PostAsync(await api.ClienteDoRhAgoraAsync(), 999999, "confirmar");
        Assert.Equal(404, (int)inexistente.StatusCode);
        await inexistente.AfirmarConformeAoContratoAsync("confirmarFeriasComoRh");
    }

    // ---------------------------------------------------------------- recusar

    [Fact]
    public async Task O_RH_recusa_ate_o_que_o_gestor_aprovou_com_o_motivo_na_decisao_do_RH_e_no_e_mail_e_a_decisao_do_gestor_fica()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, membro, _) = await EquipeAsync(api, "Recusar do RH");
        var pedido = await ComORhAsync(api, membro.FuncionarioId, gestorId, observacao: "Quero descansar.");

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), pedido, "recusar", new { motivo = "  Fechamento do trimestre.  " });

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("recusarFeriasComoRh");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("REJEITADA", "CONCLUIDA", "REJEITADA", "Quero descansar."), (corpo.GetProperty("status").GetString(), corpo.GetProperty("etapa").GetString(), corpo.GetProperty("situacao").GetString(), corpo.GetProperty("observacao").GetString()));
        var rh = corpo.GetProperty("decisaoRh");
        Assert.Equal(("REJEITADA", "Fechamento do trimestre.", api.Elenco.Rh.FuncionarioId), (rh.GetProperty("decisao").GetString(), rh.GetProperty("observacao").GetString(), rh.GetProperty("decididaPor").GetProperty("id").GetInt32()));
        var doGestor = corpo.GetProperty("decisaoGestor");
        Assert.Equal(("APROVADA", JsonValueKind.Null, gestor.FuncionarioId), (doGestor.GetProperty("decisao").GetString(), doGestor.GetProperty("observacao").ValueKind, doGestor.GetProperty("decididaPor").GetProperty("id").GetInt32())); // o motivo é do RH, não do gestor
        Assert.Equal(("REJEITADA", "Fechamento do trimestre.", "Quero descansar."), (
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT MOTIVO_RECUSA FROM FERIAS WHERE ID_FERIAS = {pedido}"),
            await TextoAsync(api, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {pedido}")));
        Assert.Equal(1, EmailsDoPedido(api, pedido));
        Assert.Contains("Fechamento do trimestre.", await TextoAsync(api, $"SELECT CARGA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}"), StringComparison.Ordinal);
        Assert.Contains("RH_RECUSOU", await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {pedido}"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Equip", 200)]
    [InlineData("Equi", 400)]
    [InlineData("      ", 400)]
    [InlineData("MMMM", 400)]
    public async Task O_motivo_da_recusa_do_RH_tem_de_5_a_500_caracteres_depois_de_aparado(string motivo, int esperado)
    {
        var api = await ApiAsync();
        var (_, _, gestorId, membro, _) = await EquipeAsync(api, "Motivo do RH");
        var pedido = await ComORhAsync(api, membro.FuncionarioId, gestorId);

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), pedido, "recusar", new { motivo });

        Assert.Equal(esperado, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("recusarFeriasComoRh");
    }

    [Fact]
    public async Task O_motivo_de_501_caracteres_e_o_corpo_sem_motivo_dao_400_e_o_de_500_vale()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, membro, _) = await EquipeAsync(api, "Motivo do RH grande");
        var pedido = await ComORhAsync(api, membro.FuncionarioId, gestorId);
        var cliente = await api.ClienteDoRhAgoraAsync();

        var sem = await PostAsync(cliente, pedido, "recusar", new { });
        var grande = await PostAsync(cliente, pedido, "recusar", new { motivo = new string('m', 501) });
        var limite = await PostAsync(cliente, pedido, "recusar", new { motivo = new string('m', 500) });

        Assert.Equal((400, 400, 200), ((int)sem.StatusCode, (int)grande.StatusCode, (int)limite.StatusCode));
    }

    // ---------------------------------------------------------------- corrida

    [Fact]
    public async Task Confirmar_contra_confirmar_e_confirmar_contra_recusar_geram_um_efeito_um_e_mail_e_um_409_sem_deadlock()
    {
        var api = await ApiAsync();
        var (setor, _, gestorId, _, _) = await EquipeAsync(api, "Corrida do RH");
        var cliente = await api.ClienteDoRhAgoraAsync();

        for (var rodada = 0; rodada < 8; rodada++)
        {
            var membro = await PessoaAsync(api, "Rodada do RH", new DateOnly(2022, 3, 14), setor);
            var duploClique = await ComORhAsync(api, membro.FuncionarioId, gestorId, "2027-01-11", "2027-01-25");
            var disputa = await ComORhAsync(api, membro.FuncionarioId, gestorId, "2027-03-08", "2027-03-22");

            var cliques = await Task.WhenAll(PostAsync(cliente, duploClique, "confirmar"), PostAsync(cliente, duploClique, "confirmar"));
            var confirmarOuRecusar = await Task.WhenAll(PostAsync(cliente, disputa, "confirmar"), PostAsync(cliente, disputa, "recusar", new { motivo = "Equipe desfalcada." }));

            Assert.Equal([200, 409], cliques.Select(r => (int)r.StatusCode).Order());
            Assert.Equal(1, EmailsDoPedido(api, duploClique));
            Assert.Equal([200, 409], confirmarOuRecusar.Select(r => (int)r.StatusCode).Order());
            Assert.Equal(1, EmailsDoPedido(api, disputa));
        }
    }

    // ---------------------------------------------------------------- as bordas que a mutação mostrou

    [Fact]
    public async Task A_busca_e_os_filtros_de_situacao_so_trazem_o_que_casa()
    {
        var api = await ApiAsync();
        var rh = api.Elenco.Rh.FuncionarioId;
        _ = await AnaEmGestoresAsync(api);
        var (_, _, gestorId, m1, m2) = await EquipeAsync(api, "Filtros do RH");
        var emGozo = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2026-10-05", "2026-10-16", "EM_GOZO", gestorId: gestorId, gestorAprovou: true, rhId: rh);
        var rejeitada = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "REJEITADA", gestorId: gestorId, resultadoDoGestor: "RECUSOU", motivo: "Equipe desfalcada.");
        var cancelada = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2027-03-08", "2027-03-22", "CANCELADA", gestorId: gestorId);
        _ = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2026-10-05", "2026-10-16", "EM_GOZO", gestorId: gestorId, gestorAprovou: true, rhId: rh);
        var cliente = await api.ClienteDoRhAgoraAsync();

        async Task<List<int>> ListarAsync(string situacao) => Ids(await CorpoAsync(await cliente.GetAsync($"{Lista}?tamanhoPagina=100&busca={Uri.EscapeDataString(m1.Nome)}&situacao={situacao}")));

        Assert.Equal([emGozo], await ListarAsync("EM_GOZO"));
        Assert.Equal([rejeitada], await ListarAsync("REJEITADA"));
        Assert.Equal([cancelada], await ListarAsync("CANCELADA"));
    }
}
