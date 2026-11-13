using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Ferias;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Beneficios.BeneficiosDeTeste;

namespace RHSystem.Api.IntegrationTests.Beneficios;

/// <summary>
/// A lista do RH e as seis decisões (<c>/api/rh/beneficios-adesoes</c>) no banco REAL. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília); 12/10 e 07/09 são feriados nacionais do banco de teste. Cada teste cria as próprias pessoas e os próprios itens (a lista do RH filtra por <c>beneficioId</c> para olhar só os dele).
/// Os dependentes de teste levam o marcador <c>Zyx</c> para os testes provarem que o log nunca o leva.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AdesoesParaRhTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/beneficios-adesoes";

    private Task<ApiDeAcesso> ApiAsync() => BeneficiosDeTeste.ApiAsync(mysql, nameof(AdesoesParaRhTests));

    private static Task<PessoaDeFerias> NovaAsync(ApiDeAcesso api, string nome = "Pessoa", Perfil perfil = Perfil.Funcionario, bool ativa = true) =>
        FeriasDeTeste.PessoaAsync(api, nome, new DateOnly(2024, 1, 2), perfil: perfil, ativa: ativa);

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static Task<HttpResponseMessage> PostAsync(HttpClient cliente, string rota, object? corpo = null) =>
        corpo is null ? cliente.PostAsync(rota, content: null) : cliente.PostAsJsonAsync(rota, corpo);

    private static string Aprovar(int adesao) => $"{Rota}/{adesao}/aprovar";

    private static string Recusar(int adesao) => $"{Rota}/{adesao}/recusar";

    private static string DoDependente(int adesao, int dependente, string acao) => $"{Rota}/{adesao}/dependentes/{dependente}/{acao}";

    private static string DoTrajeto(int adesao, int trajeto, string acao) => $"{Rota}/{adesao}/trajetos/{trajeto}/{acao}";

    private static string Linha(ApiDeAcesso api, int adesaoId) =>
        TextoAsync(api, $"SELECT CONCAT_WS('|', STATUS_PEDIDO, BENEFICIO_ATIVO, IFNULL(ID_FUNCIONARIO_RH, '-'), IFNULL(DATE_FORMAT(DATA_RESPOSTA, '%Y-%m-%d %H:%i:%s'), '-'), IFNULL(MOTIVO_RECUSA, '-')) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}").GetAwaiter().GetResult()!;

    private static string Filhos(ApiDeAcesso api, string tabela, string chave, int adesaoId) =>
        TextoAsync(api, $"SELECT IFNULL(GROUP_CONCAT(CONCAT(STATUS_PEDIDO, ':', IFNULL(ID_FUNCIONARIO_RH, '-'), ':', IFNULL(DATE_FORMAT(DATA_RESPOSTA, '%H:%i:%s'), '-')) ORDER BY {chave} SEPARATOR ','), '') FROM {tabela} WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}").GetAwaiter().GetResult()!;

    private static long TotalDeEmails(ApiDeAcesso api) => Contar(api, "SELECT COUNT(*) FROM FILA_EMAILS");

    private static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("adesaoId").GetInt32())];

    /// <summary>Um item novo e uma adesão <c>PENDENTE</c> da pessoa, pedida em 2026-10-07 09:41 (12:41Z).</summary>
    private static async Task<(int Item, int Adesao)> PendenteAsync(ApiDeAcesso api, PessoaDeFerias pessoa, bool dependentes = true, bool trajeto = false, string pedidoEm = "2026-10-07 09:41:00")
    {
        var item = await ItemAsync(api, dependentes: dependentes, trajeto: trajeto);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item, "PENDENTE", ativo: false, pedidoEm: pedidoEm, inicio: "2026-11-01", autoriza: true);
        return (item, adesao);
    }

    // ---------------------------------------------------------------- aprovar a adesão

    [Fact]
    public async Task Aprovar_ativa_o_beneficio_e_aprova_junto_os_filhos_pendentes_com_o_mesmo_RH_e_a_mesma_data_sem_e_mail_e_o_log_sem_os_dados()
    {
        var api = await ApiAsync();
        var bruno = await NovaAsync(api, "Bruno Lima");
        var (_, adesao) = await PendenteAsync(api, bruno, dependentes: true, trajeto: true);
        var helena = await DependenteAsync(api, adesao, "Zyx Helena", pedidoEm: "2026-10-07 09:41:00");
        var pedro = await DependenteAsync(api, adesao, "Zyx Pedro", "FILHO", "2021-09-03", status: "RECUSADO", pedidoEm: "2026-09-01 09:00:00", respondidoEm: "2026-09-02 10:00:00");
        var trajeto = await TrajetoAsync(api, adesao, origem: "Zyx Rua Secreta, 1", pedidoEm: "2026-10-07 09:41:00");
        var emails = TotalDeEmails(api);

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), Aprovar(adesao));

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("aprovarAdesaoABeneficio");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((adesao, bruno.FuncionarioId, "APROVADO", "2026-10-07T12:41:00Z", "2026-10-15", "2026-11-01", true, api.Elenco.Rh.FuncionarioId, "2026-10-07T12:42:00Z", JsonValueKind.Null), (
            corpo.GetProperty("adesaoId").GetInt32(),
            corpo.GetProperty("funcionario").GetProperty("id").GetInt32(),
            corpo.GetProperty("situacao").GetString()!,
            Utc(corpo.GetProperty("pedidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            corpo.GetProperty("respostaPrevistaAte").GetString()!,
            corpo.GetProperty("dataInicioDesejada").GetString()!,
            corpo.GetProperty("autorizaDescontoEmFolha").GetBoolean(),
            corpo.GetProperty("respondidoPor").GetProperty("id").GetInt32(),
            Utc(corpo.GetProperty("respondidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            corpo.GetProperty("motivoRecusa").ValueKind));
        Assert.Equal(
            [(helena, "APROVADO", "2026-10-07T12:42:00Z"), (pedro, "RECUSADO", "2026-09-02T13:00:00Z")],
            corpo.GetProperty("dependentes").EnumerateArray().Select(d => (d.GetProperty("id").GetInt32(), d.GetProperty("situacao").GetString()!, Utc(d.GetProperty("respondidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ"))));
        var oTrajeto = Assert.Single(corpo.GetProperty("trajetos").EnumerateArray());
        Assert.Equal((trajeto, "APROVADO"), (oTrajeto.GetProperty("id").GetInt32(), oTrajeto.GetProperty("situacao").GetString()!));

        var rh = api.Elenco.Rh.FuncionarioId;
        Assert.Equal($"APROVADO|1|{rh}|2026-10-07 09:42:00|-", Linha(api, adesao));
        Assert.Equal($"APROVADO:{rh}:09:42:00,RECUSADO:{rh}:10:00:00", Filhos(api, "DEPENDENTES", "ID_DEPENDENTE", adesao));
        Assert.Equal($"APROVADO:{rh}:09:42:00", Filhos(api, "TRAJETOS_VALE_TRANSPORTE", "ID_TRAJETO", adesao));
        Assert.Equal(emails, TotalDeEmails(api));

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesao));
        Assert.Equal((AuditoriaArea.Beneficios, AuditoriaAcao.Aprovou, api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.FuncionarioAtorId));
        Assert.Equal([("Situação", "Pendente", "Aprovado"), ("Benefício ativo", "Não", "Sim")], evento.Mudancas.OrderBy(m => m.Ordem).Select(m => (m.Campo, m.ValorAntes!, m.ValorDepois!)));
        Assert.DoesNotContain("Zyx", (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_RH_decide_ate_o_proprio_pedido()
    {
        var api = await ApiAsync();
        var ana = new PessoaDeFerias(api.Elenco.Rh.FuncionarioId, api.Elenco.Rh.UsuarioId, "Ana");
        var (_, adesao) = await PendenteAsync(api, ana, dependentes: false);

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), Aprovar(adesao));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.StartsWith("APROVADO|1|", Linha(api, adesao), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aprovar_adesao_de_pessoa_inativa_e_422_e_nada_muda_mas_recusar_vale()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Inativa pendente");
        var (_, paraAprovar) = await PendenteAsync(api, pessoa);
        var (_, paraRecusar) = await PendenteAsync(api, pessoa);
        await AdvertenciasDeTeste.InativarAsync(api, pessoa.FuncionarioId);
        var rh = await api.ClienteDoRhAgoraAsync();

        var aprovar = await PostAsync(rh, Aprovar(paraAprovar));
        var recusar = await PostAsync(rh, Recusar(paraRecusar), new { motivo = "Pessoa desligada." });

        Assert.Equal(422, (int)aprovar.StatusCode);
        await aprovar.AfirmarConformeAoContratoAsync("aprovarAdesaoABeneficio");
        Assert.Equal("FUNCIONARIO_INATIVO", Codigo(await CorpoAsync(aprovar)));
        Assert.StartsWith("PENDENTE|0|-|-|-", Linha(api, paraAprovar), StringComparison.Ordinal);
        Assert.Equal(200, (int)recusar.StatusCode);
        Assert.StartsWith("RECUSADO|0|", Linha(api, paraRecusar), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- recusar a adesão

    [Fact]
    public async Task Recusar_grava_o_motivo_aparado_cancela_os_filhos_pendentes_sem_resposta_e_o_item_volta_para_a_pessoa_com_o_motivo()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Recusada");
        var (item, adesao) = await PendenteAsync(api, pessoa, dependentes: true, trajeto: true);
        var helena = await DependenteAsync(api, adesao, "Zyx Helena", pedidoEm: "2026-10-07 09:41:00");
        var aprovadoAntes = await DependenteAsync(api, adesao, "Zyx Antigo", status: "APROVADO", pedidoEm: "2026-08-01 09:00:00", respondidoEm: "2026-08-02 10:00:00");
        _ = await TrajetoAsync(api, adesao, pedidoEm: "2026-10-07 09:41:00");
        var emails = TotalDeEmails(api);

        var resposta = await PostAsync(await api.ClienteDoRhAgoraAsync(), Recusar(adesao), new { motivo = "  Só aceita adesão no primeiro mês de cada trimestre.  " });

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("recusarAdesaoABeneficio");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("RECUSADO", "Só aceita adesão no primeiro mês de cada trimestre.", api.Elenco.Rh.FuncionarioId), (corpo.GetProperty("situacao").GetString()!, corpo.GetProperty("motivoRecusa").GetString()!, corpo.GetProperty("respondidoPor").GetProperty("id").GetInt32()));
        Assert.Equal(
            [(helena, "CANCELADO", JsonValueKind.Null), (aprovadoAntes, "APROVADO", JsonValueKind.String)],
            corpo.GetProperty("dependentes").EnumerateArray().Select(d => (d.GetProperty("id").GetInt32(), d.GetProperty("situacao").GetString()!, d.GetProperty("respondidoEmUtc").ValueKind)).OrderBy(d => d.Item1 == helena ? 0 : 1));
        Assert.Equal(["CANCELADO"], corpo.GetProperty("trajetos").EnumerateArray().Select(t => t.GetProperty("situacao").GetString()!));
        var rh = api.Elenco.Rh.FuncionarioId;
        Assert.Equal($"RECUSADO|0|{rh}|2026-10-07 09:42:00|Só aceita adesão no primeiro mês de cada trimestre.", Linha(api, adesao));
        Assert.Equal($"APROVADO:{rh}:10:00:00,CANCELADO:-:-", Filhos(api, "DEPENDENTES", "ID_DEPENDENTE", adesao).Split(',').Order().Aggregate((a, b) => a + "," + b));
        Assert.Equal("CANCELADO:-:-", Filhos(api, "TRAJETOS_VALE_TRANSPORTE", "ID_TRAJETO", adesao));
        Assert.Equal(emails, TotalDeEmails(api));

        var tela = await CorpoAsync(await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(RotaEu));
        var disponivel = tela.GetProperty("disponiveis").EnumerateArray().Single(d => d.GetProperty("beneficio").GetProperty("id").GetInt32() == item);
        Assert.Equal(("RECUSADO", "Só aceita adesão no primeiro mês de cada trimestre."), (disponivel.GetProperty("ultimoPedido").GetProperty("situacao").GetString()!, disponivel.GetProperty("ultimoPedido").GetProperty("motivoRecusa").GetString()!));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesao));
        Assert.Equal(AuditoriaAcao.Recusou, evento.Acao);
        var log = (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto;
        Assert.DoesNotContain("Zyx", log, StringComparison.Ordinal);
        Assert.DoesNotContain("trimestre", log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task Recusar_com_motivo_vazio_ou_so_de_espacos_e_400_e_nada_muda(string motivo)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Motivo vazio");
        var (_, adesao) = await PendenteAsync(api, pessoa);
        var dependente = await DependenteAsync(api, adesao);
        var rh = await api.ClienteDoRhAgoraAsync();

        var daAdesao = await PostAsync(rh, Recusar(adesao), new { motivo });
        var doDependente = await PostAsync(rh, DoDependente(adesao, dependente, "recusar"), new { motivo });
        var semCorpo = await rh.PostAsync(Recusar(adesao), new StringContent("{}", Encoding.UTF8, "application/json"));

        foreach (var resposta in new[] { daAdesao, doDependente, semCorpo })
        {
            Assert.Equal(400, (int)resposta.StatusCode);
        }

        await daAdesao.AfirmarConformeAoContratoAsync("recusarAdesaoABeneficio");
        Assert.True((await CorpoAsync(daAdesao)).GetProperty("errors").TryGetProperty("motivo", out _));
        Assert.StartsWith("PENDENTE|0|-|-|-", Linha(api, adesao), StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_motivo_de_500_caracteres_vale_e_o_de_501_e_400()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Motivo longo");
        var (_, longa) = await PendenteAsync(api, pessoa);
        var (_, ok) = await PendenteAsync(api, pessoa);
        var rh = await api.ClienteDoRhAgoraAsync();

        var demais = await PostAsync(rh, Recusar(longa), new { motivo = new string('m', 501) });
        var exato = await PostAsync(rh, Recusar(ok), new { motivo = " " + new string('m', 500) + " " });

        Assert.Equal((400, 200), ((int)demais.StatusCode, (int)exato.StatusCode));
        Assert.Equal(500, (await CorpoAsync(exato)).GetProperty("motivoRecusa").GetString()!.Length);
    }

    // ---------------------------------------------------------------- os 404 e os 409

    [Fact]
    public async Task Adesao_dependente_ou_trajeto_que_nao_existe_ou_e_de_outra_adesao_e_404()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Quatrocentos e quatro");
        var item = await ItemAsync(api, dependentes: true, trajeto: true, opcional: false);
        var minha = await AdesaoAsync(api, pessoa.FuncionarioId, item);
        var outraPessoa = await NovaAsync(api, "Outra pessoa 404");
        var daOutra = await AdesaoAsync(api, outraPessoa.FuncionarioId, item);
        var dependenteDaOutra = await DependenteAsync(api, daOutra);
        var trajetoDaOutra = await TrajetoAsync(api, daOutra);
        var rh = await api.ClienteDoRhAgoraAsync();
        var motivo = new { motivo = "Nao" };

        var respostas = new[]
        {
            await PostAsync(rh, Aprovar(999_999)),
            await PostAsync(rh, Recusar(999_999), motivo),
            await PostAsync(rh, DoDependente(minha, dependenteDaOutra, "aprovar")),
            await PostAsync(rh, DoDependente(minha, dependenteDaOutra, "recusar"), motivo),
            await PostAsync(rh, DoDependente(minha, 999_999, "aprovar")),
            await PostAsync(rh, DoTrajeto(minha, trajetoDaOutra, "aprovar")),
            await PostAsync(rh, DoTrajeto(minha, trajetoDaOutra, "recusar"), motivo),
            await PostAsync(rh, DoTrajeto(999_999, trajetoDaOutra, "aprovar")),
        };

        Assert.All(respostas, r => Assert.Equal(404, (int)r.StatusCode));
        await respostas[2].AfirmarConformeAoContratoAsync("aprovarDependenteDeBeneficio");
        Assert.Equal("PENDENTE", await TextoAsync(api, $"SELECT STATUS_PEDIDO FROM DEPENDENTES WHERE ID_DEPENDENTE = {dependenteDaOutra}"));
    }

    [Theory]
    [InlineData("APROVADO", true)]
    [InlineData("RECUSADO", false)]
    [InlineData("CANCELADO", false)]
    public async Task Decidir_adesao_que_ja_foi_respondida_e_409_BENEFICIO_PEDIDO_JA_DECIDIDO(string status, bool ativo)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Ja decidida " + status);
        var item = await ItemAsync(api);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item, status, ativo, pedidoEm: "2026-10-01 09:00:00", respondidoEm: status == "CANCELADO" ? null : "2026-10-02 10:00:00");
        var antes = Linha(api, adesao);
        var rh = await api.ClienteDoRhAgoraAsync();

        var aprovar = await PostAsync(rh, Aprovar(adesao));
        var recusar = await PostAsync(rh, Recusar(adesao), new { motivo = "Tarde demais." });

        foreach (var resposta in new[] { aprovar, recusar })
        {
            Assert.Equal(409, (int)resposta.StatusCode);
            Assert.Equal("BENEFICIO_PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(resposta)));
        }

        await aprovar.AfirmarConformeAoContratoAsync("aprovarAdesaoABeneficio");
        await recusar.AfirmarConformeAoContratoAsync("recusarAdesaoABeneficio");
        Assert.Equal(antes, Linha(api, adesao));
    }

    [Theory]
    [InlineData("PENDENTE", false, "PENDENTE")]
    [InlineData("RECUSADO", false, "PENDENTE")]
    [InlineData("CANCELADO", false, "PENDENTE")]
    [InlineData("APROVADO", false, "PENDENTE")]
    [InlineData("PENDENTE", false, "APROVADO")]
    public async Task Decidir_filho_de_adesao_que_nao_esta_ativa_e_409_ADESAO_NAO_ATIVA_mesmo_que_o_filho_ja_tenha_sido_decidido(string statusDaAdesao, bool ativo, string statusDoFilho)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, $"Adesao {statusDaAdesao} filho {statusDoFilho}");
        var item = await ItemAsync(api, dependentes: true, trajeto: true);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item, statusDaAdesao, ativo, pedidoEm: statusDaAdesao == "APROVADO" ? null : "2026-10-01 09:00:00");
        var dependente = await DependenteAsync(api, adesao, status: statusDoFilho, respondidoEm: statusDoFilho == "APROVADO" ? "2026-10-02 10:00:00" : null);
        var trajeto = await TrajetoAsync(api, adesao, status: statusDoFilho, respondidoEm: statusDoFilho == "APROVADO" ? "2026-10-02 10:00:00" : null);
        var rh = await api.ClienteDoRhAgoraAsync();
        var motivo = new { motivo = "Nao" };

        var respostas = new[]
        {
            await PostAsync(rh, DoDependente(adesao, dependente, "aprovar")),
            await PostAsync(rh, DoDependente(adesao, dependente, "recusar"), motivo),
            await PostAsync(rh, DoTrajeto(adesao, trajeto, "aprovar")),
            await PostAsync(rh, DoTrajeto(adesao, trajeto, "recusar"), motivo),
        };

        foreach (var resposta in respostas)
        {
            Assert.Equal(409, (int)resposta.StatusCode);
            Assert.Equal("BENEFICIO_ADESAO_NAO_ATIVA", Codigo(await CorpoAsync(resposta)));
        }

        await respostas[0].AfirmarConformeAoContratoAsync("aprovarDependenteDeBeneficio");
        await respostas[3].AfirmarConformeAoContratoAsync("recusarTrajetoDeBeneficio");
    }

    [Fact]
    public async Task Decidir_dependente_ou_trajeto_que_ja_foi_respondido_numa_adesao_ativa_e_409_BENEFICIO_PEDIDO_JA_DECIDIDO()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Filho ja decidido");
        var item = await ItemAsync(api, dependentes: true, trajeto: true, opcional: false);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item);
        var dependente = await DependenteAsync(api, adesao, status: "RECUSADO", respondidoEm: "2026-10-02 10:00:00");
        var trajeto = await TrajetoAsync(api, adesao, status: "APROVADO", respondidoEm: "2026-10-02 10:00:00");
        var rh = await api.ClienteDoRhAgoraAsync();
        var motivo = new { motivo = "Nao" };

        var respostas = new[]
        {
            await PostAsync(rh, DoDependente(adesao, dependente, "aprovar")),
            await PostAsync(rh, DoDependente(adesao, dependente, "recusar"), motivo),
            await PostAsync(rh, DoTrajeto(adesao, trajeto, "aprovar")),
            await PostAsync(rh, DoTrajeto(adesao, trajeto, "recusar"), motivo),
        };

        foreach (var resposta in respostas)
        {
            Assert.Equal(409, (int)resposta.StatusCode);
            Assert.Equal("BENEFICIO_PEDIDO_JA_DECIDIDO", Codigo(await CorpoAsync(resposta)));
        }

        await respostas[1].AfirmarConformeAoContratoAsync("recusarDependenteDeBeneficio");
        await respostas[2].AfirmarConformeAoContratoAsync("aprovarTrajetoDeBeneficio");
        Assert.Equal(("RECUSADO", "APROVADO"), (await TextoAsync(api, $"SELECT STATUS_PEDIDO FROM DEPENDENTES WHERE ID_DEPENDENTE = {dependente}"), await TextoAsync(api, $"SELECT STATUS_PEDIDO FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_TRAJETO = {trajeto}")));
    }

    // ---------------------------------------------------------------- dependente e trajeto

    [Fact]
    public async Task Aprovar_dependente_aprova_so_ele_e_a_pessoa_o_ve_nos_cadastrados_e_recusar_dependente_grava_o_motivo()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Dependentes decididos");
        var item = await ItemAsync(api, dependentes: true, opcional: false);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item);
        var aprovado = await DependenteAsync(api, adesao, "Zyx Aprovada", pedidoEm: "2026-10-07 09:38:00");
        var recusado = await DependenteAsync(api, adesao, "Zyx Recusado", "FILHO", "2021-09-03", pedidoEm: "2026-10-07 09:39:00");
        var intocado = await DependenteAsync(api, adesao, "Zyx Intocado", "FILHO", "2022-02-02", pedidoEm: "2026-10-07 09:40:00");
        var rh = await api.ClienteDoRhAgoraAsync();
        var emails = TotalDeEmails(api);

        var aprovou = await PostAsync(rh, DoDependente(adesao, aprovado, "aprovar"));
        var recusou = await PostAsync(rh, DoDependente(adesao, recusado, "recusar"), new { motivo = " Falta a certidão. " });

        Assert.Equal((200, 200), ((int)aprovou.StatusCode, (int)recusou.StatusCode));
        await aprovou.AfirmarConformeAoContratoAsync("aprovarDependenteDeBeneficio");
        await recusou.AfirmarConformeAoContratoAsync("recusarDependenteDeBeneficio");
        var corpo = await CorpoAsync(recusou);
        Assert.Equal(
            [(aprovado, "APROVADO", JsonValueKind.Null), (recusado, "RECUSADO", JsonValueKind.String), (intocado, "PENDENTE", JsonValueKind.Null)],
            corpo.GetProperty("dependentes").EnumerateArray().Select(d => (d.GetProperty("id").GetInt32(), d.GetProperty("situacao").GetString()!, d.GetProperty("motivoRecusa").ValueKind)));
        Assert.Equal("Falta a certidão.", corpo.GetProperty("dependentes")[1].GetProperty("motivoRecusa").GetString());
        Assert.Equal("APROVADO", corpo.GetProperty("situacao").GetString()); // a adesão continua como estava
        Assert.Equal(emails, TotalDeEmails(api));

        var tela = await CorpoAsync(await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(RotaEu));
        Assert.Equal(["Zyx Aprovada"], tela.GetProperty("dependentesCadastrados").EnumerateArray().Select(d => d.GetProperty("nome").GetString()));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "DEPENDENTES", aprovado));
        Assert.Equal((AuditoriaArea.Beneficios, AuditoriaAcao.Aprovou), (evento.Area, evento.Acao));
        Assert.Equal([("Situação", "Pendente", "Aprovado")], evento.Mudancas.Select(m => (m.Campo, m.ValorAntes!, m.ValorDepois!)));
        var eventoDaRecusa = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "DEPENDENTES", recusado));
        Assert.Equal((AuditoriaArea.Beneficios, AuditoriaAcao.Recusou), (eventoDaRecusa.Area, eventoDaRecusa.Acao));
        Assert.StartsWith("Recusou a inclusão de um dependente em ", eventoDaRecusa.Descricao, StringComparison.Ordinal);
        Assert.Equal([("Situação", "Pendente", "Recusado")], eventoDaRecusa.Mudancas.Select(m => (m.Campo, m.ValorAntes!, m.ValorDepois!)));
        var log = (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto;
        Assert.DoesNotContain("Zyx", log, StringComparison.Ordinal);
        Assert.DoesNotContain("certidão", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aprovar_trajeto_o_torna_o_em_vigor_e_recusar_trajeto_nao_tira_o_que_ja_vale()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Trajetos decididos");
        var vt = await ItemAsync(api, trajeto: true, opcional: false);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, vt);
        var antigo = await TrajetoAsync(api, adesao, origem: "Zyx Rua Antiga, 1", status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");
        var novo = await TrajetoAsync(api, adesao, origem: "Zyx Rua Nova, 2", pedidoEm: "2026-10-07 09:39:00");
        var outroNovo = await TrajetoAsync(api, adesao, origem: "Zyx Rua Pior, 3", pedidoEm: "2026-10-07 09:40:00");
        var rh = await api.ClienteDoRhAgoraAsync();
        async Task<int> EmVigorAsync() => (await CorpoAsync(await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(RotaEu))).GetProperty("beneficios")[0].GetProperty("trajetoAtual").GetProperty("id").GetInt32();

        Assert.Equal(antigo, await EmVigorAsync());
        var recusou = await PostAsync(rh, DoTrajeto(adesao, outroNovo, "recusar"), new { motivo = "Endereço incompleto." });
        Assert.Equal(antigo, await EmVigorAsync());
        var aprovou = await PostAsync(rh, DoTrajeto(adesao, novo, "aprovar"));

        Assert.Equal((200, 200), ((int)recusou.StatusCode, (int)aprovou.StatusCode));
        await recusou.AfirmarConformeAoContratoAsync("recusarTrajetoDeBeneficio");
        await aprovou.AfirmarConformeAoContratoAsync("aprovarTrajetoDeBeneficio");
        Assert.Equal(novo, await EmVigorAsync());
        Assert.Equal(["APROVADO", "APROVADO", "RECUSADO"], (await CorpoAsync(aprovou)).GetProperty("trajetos").EnumerateArray().Select(t => t.GetProperty("situacao").GetString()!));
        var eventoDoAprovado = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "TRAJETOS_VALE_TRANSPORTE", novo));
        var eventoDoRecusado = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "TRAJETOS_VALE_TRANSPORTE", outroNovo));
        Assert.StartsWith("Aprovou a atualização do trajeto em ", eventoDoAprovado.Descricao, StringComparison.Ordinal);
        Assert.StartsWith("Recusou a atualização do trajeto em ", eventoDoRecusado.Descricao, StringComparison.Ordinal);
        Assert.Equal((AuditoriaAcao.Aprovou, "Pendente", "Aprovado"), (eventoDoAprovado.Acao, eventoDoAprovado.Mudancas.Single().ValorAntes, eventoDoAprovado.Mudancas.Single().ValorDepois));
        Assert.Equal((AuditoriaAcao.Recusou, "Pendente", "Recusado"), (eventoDoRecusado.Acao, eventoDoRecusado.Mudancas.Single().ValorAntes, eventoDoRecusado.Mudancas.Single().ValorDepois));
        var log = (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto;
        Assert.DoesNotContain("Zyx", log, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- a corrida

    [Fact]
    public async Task Dois_RH_decidindo_a_mesma_adesao_ao_mesmo_tempo_dao_um_efeito_um_evento_e_um_409()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Corrida do RH");
        var (_, adesao) = await PendenteAsync(api, pessoa);
        var dependente = await DependenteAsync(api, adesao, pedidoEm: "2026-10-07 09:41:00");
        var rh = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(PostAsync(rh, Aprovar(adesao)), PostAsync(rh, Recusar(adesao), new { motivo = "Nao." }), PostAsync(rh, Aprovar(adesao)));

        Assert.Equal([200, 409, 409], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesao));
        var final = Linha(api, adesao);
        var situacaoDoFilho = await TextoAsync(api, $"SELECT STATUS_PEDIDO FROM DEPENDENTES WHERE ID_DEPENDENTE = {dependente}");
        Assert.True(final.StartsWith("APROVADO|1|", StringComparison.Ordinal) ? situacaoDoFilho == "APROVADO" : situacaoDoFilho == "CANCELADO", $"{final} / {situacaoDoFilho}");
    }

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_padrao_traz_as_pendentes_e_as_aprovadas_com_filho_pendente_pela_menor_data_PENDENTE()
    {
        var api = await ApiAsync();
        var item = await ItemAsync(api, dependentes: true, trajeto: true);
        var a = await NovaAsync(api, "Pessoa A da lista");
        var b = await NovaAsync(api, "Pessoa B da lista");
        var c = await NovaAsync(api, "Pessoa C da lista");
        var d = await NovaAsync(api, "Pessoa D da lista");
        var e = await NovaAsync(api, "Pessoa E da lista");
        var f = await NovaAsync(api, "Pessoa F da lista");
        var adesaoA = await AdesaoAsync(api, a.FuncionarioId, item, "PENDENTE", ativo: false, pedidoEm: "2026-10-05 08:00:00", inicio: "2026-11-01", autoriza: true);
        _ = await DependenteAsync(api, adesaoA, "Zyx A", pedidoEm: "2026-10-05 08:00:00");
        var adesaoB = await AdesaoAsync(api, b.FuncionarioId, item); // dada direto: sem pedido
        _ = await TrajetoAsync(api, adesaoB, pedidoEm: "2026-10-08 08:00:00"); // o que espera
        _ = await DependenteAsync(api, adesaoB, "Zyx B antigo", status: "APROVADO", pedidoEm: "2026-08-01 08:00:00", respondidoEm: "2026-08-02 10:00:00"); // já respondido: não conta para a fila
        var adesaoC = await AdesaoAsync(api, c.FuncionarioId, item, "APROVADO", pedidoEm: "2026-09-01 08:00:00", respondidoEm: "2026-09-02 10:00:00");
        _ = await DependenteAsync(api, adesaoC, status: "APROVADO", respondidoEm: "2026-09-02 10:00:00");
        var adesaoD = await AdesaoAsync(api, d.FuncionarioId, item, "PENDENTE", ativo: false, pedidoEm: "2026-10-06 08:00:00");
        var adesaoE = await AdesaoAsync(api, e.FuncionarioId, item);
        _ = await DependenteAsync(api, adesaoE, "Zyx E", pedidoEm: "2026-10-09 08:00:00"); // o que espera
        _ = await TrajetoAsync(api, adesaoE, status: "APROVADO", pedidoEm: "2026-08-01 08:00:00", respondidoEm: "2026-08-02 10:00:00"); // já respondido: não conta para a fila
        var adesaoF = await AdesaoAsync(api, f.FuncionarioId, item, "RECUSADO", ativo: false, pedidoEm: "2026-09-10 08:00:00", respondidoEm: "2026-09-11 10:00:00");
        var rh = await api.ClienteDoRhAgoraAsync();
        string Url(string extra = "") => $"{Rota}?beneficioId={item}{extra}";

        var padrao = await rh.GetAsync(Url());

        Assert.Equal(200, (int)padrao.StatusCode);
        await padrao.AfirmarConformeAoContratoAsync("listarPedidosDeBeneficio");
        var corpo = await CorpoAsync(padrao);
        Assert.Equal([adesaoA, adesaoD, adesaoB, adesaoE], Ids(corpo)); // espera desde 05/10 (a adesão), 06/10 (a adesão), 08/10 (o trajeto) e 09/10 (o dependente)
        Assert.Equal((1, 20, 4, 1), (corpo.GetProperty("pagina").GetInt32(), corpo.GetProperty("tamanhoPagina").GetInt32(), corpo.GetProperty("total").GetInt32(), corpo.GetProperty("totalPaginas").GetInt32()));
        var daB = corpo.GetProperty("itens")[2];
        Assert.Equal((b.FuncionarioId, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, "APROVADO", 1, 1), (
            daB.GetProperty("funcionario").GetProperty("id").GetInt32(),
            daB.GetProperty("pedidoEmUtc").ValueKind,
            daB.GetProperty("respostaPrevistaAte").ValueKind,
            daB.GetProperty("dataInicioDesejada").ValueKind,
            daB.GetProperty("situacao").GetString()!,
            daB.GetProperty("dependentes").GetArrayLength(),
            daB.GetProperty("trajetos").GetArrayLength()));
        var daA = corpo.GetProperty("itens")[0];
        Assert.Equal(("PENDENTE", "2026-10-05T11:00:00Z", "2026-10-13", "2026-11-01", true, JsonValueKind.Null, JsonValueKind.Null, 1), (
            daA.GetProperty("situacao").GetString()!,
            Utc(daA.GetProperty("pedidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            daA.GetProperty("respostaPrevistaAte").GetString()!,
            daA.GetProperty("dataInicioDesejada").GetString()!,
            daA.GetProperty("autorizaDescontoEmFolha").GetBoolean(),
            daA.GetProperty("respondidoPor").ValueKind,
            daA.GetProperty("motivoRecusa").ValueKind,
            daA.GetProperty("dependentes").GetArrayLength()));

        Assert.Equal([adesaoC, adesaoB, adesaoE], Ids(await CorpoAsync(await rh.GetAsync(Url("&situacao=APROVADO"))))); // a concedida direto (sem pedido) por último, em ordem de id
        Assert.Equal([adesaoF], Ids(await CorpoAsync(await rh.GetAsync(Url("&situacao=RECUSADO")))));
        Assert.Empty(Ids(await CorpoAsync(await rh.GetAsync(Url("&situacao=CANCELADO")))));
        Assert.Equal([adesaoA], Ids(await CorpoAsync(await rh.GetAsync(Url($"&busca={Uri.EscapeDataString(a.Nome)}")))));
        Assert.Empty(Ids(await CorpoAsync(await rh.GetAsync($"{Rota}?beneficioId={item + 1_000_000}"))));

        var segunda = await CorpoAsync(await rh.GetAsync(Url("&pagina=2&tamanhoPagina=3")));
        Assert.Equal([adesaoE], Ids(segunda));
        Assert.Equal((4, 2, 2), (segunda.GetProperty("total").GetInt32(), segunda.GetProperty("totalPaginas").GetInt32(), segunda.GetProperty("pagina").GetInt32()));
        var invalida = await rh.GetAsync(Url("&situacao=XYZ"));
        Assert.Equal(400, (int)invalida.StatusCode);
        await invalida.AfirmarConformeAoContratoAsync("listarPedidosDeBeneficio");
    }

    [Fact]
    public async Task O_pedido_que_so_tem_filho_pendente_aparece_na_fila_e_some_quando_o_RH_decide_o_filho()
    {
        var api = await ApiAsync();
        var item = await ItemAsync(api, dependentes: true, opcional: false);
        var pessoa = await NovaAsync(api, "So o filho");
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item);
        var dependente = await DependenteAsync(api, adesao, pedidoEm: "2026-10-07 09:41:00");
        var rh = await api.ClienteDoRhAgoraAsync();

        var antes = Ids(await CorpoAsync(await rh.GetAsync($"{Rota}?beneficioId={item}")));
        await PostAsync(rh, DoDependente(adesao, dependente, "aprovar"));
        var depois = Ids(await CorpoAsync(await rh.GetAsync($"{Rota}?beneficioId={item}")));

        Assert.Equal([adesao], antes);
        Assert.Empty(depois);
    }

    // ---------------------------------------------------------------- quem pode

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    public async Task So_o_RH_usa_estas_rotas(Perfil perfil)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Sem permissao " + perfil, perfil);
        var (_, adesao) = await PendenteAsync(api, pessoa);
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);

        var respostas = new[]
        {
            await cliente.GetAsync(Rota),
            await PostAsync(cliente, Aprovar(adesao)),
            await PostAsync(cliente, Recusar(adesao), new { motivo = "Nao" }),
            await PostAsync(cliente, DoDependente(adesao, 1, "aprovar")),
            await PostAsync(cliente, DoTrajeto(adesao, 1, "recusar"), new { motivo = "Nao" }),
        };

        Assert.All(respostas, r => Assert.Equal(403, (int)r.StatusCode));
        Assert.StartsWith("PENDENTE|", Linha(api, adesao), StringComparison.Ordinal);
    }
}
