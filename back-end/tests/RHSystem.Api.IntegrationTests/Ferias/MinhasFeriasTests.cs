using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Ferias;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Ferias;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>
/// <c>GET /api/eu/ferias</c>, <c>POST /api/eu/ferias/conferencia</c> e <c>POST /api/eu/ferias</c> no banco REAL. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). O Bruno do contrato foi admitido em 2022-03-14 e tem 18 dias de saldo no período de 2025 a 2026; o gestor dele é Carlos Ribeiro (Comercial).
/// Cada teste cria as próprias pessoas (o banco é um por classe e o saldo é por pessoa).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MinhasFeriasTests(MySqlFixture mysql)
{
    private static readonly DateOnly AdmissaoDoBruno = new(2022, 3, 14);

    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(MinhasFeriasTests));

    private static async Task<HttpResponseMessage> PedirAsync(ApiDeAcesso api, PessoaDeFerias pessoa, object corpo) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(Rota, corpo);

    private static async Task<HttpResponseMessage> ConferirAsync(ApiDeAcesso api, PessoaDeFerias pessoa, object corpo) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync($"{Rota}/conferencia", corpo);

    private static async Task<(PessoaDeFerias Bruno, int GestorId)> BrunoAsync(ApiDeAcesso api, bool com340)
    {
        var carlos = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.Gestor.FuncionarioId);
        var bruno = await PessoaAsync(api, "Bruno Lima", AdmissaoDoBruno, api.Elenco.SetorComercialId);
        await SemearBrunoAsync(api, bruno.FuncionarioId, carlos, api.Elenco.Rh.FuncionarioId, com340);
        return (bruno, carlos);
    }

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    // ---------------------------------------------------------------- obterMinhasFerias

    [Fact]
    public async Task Minhas_ferias_devolvem_o_exemplo_do_Bruno_do_contrato_saldo_periodos_e_historico()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, com340: true);

        var resposta = await (await api.ClienteAgoraAsync(bruno.UsuarioId)).GetAsync(Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterMinhasFerias");
        var corpo = await CorpoAsync(resposta);
        var resumo = corpo.GetProperty("resumo");
        Assert.Equal((18, "2025-03-14", "2026-03-13", "2027-03-13", "2026-03-14", "2027-03-13", 1), (
            resumo.GetProperty("diasDisponiveis").GetInt32(),
            resumo.GetProperty("periodoDoSaldo").GetProperty("inicio").GetString(),
            resumo.GetProperty("periodoDoSaldo").GetProperty("fim").GetString(),
            resumo.GetProperty("prazoParaTirarSaldo").GetString(),
            resumo.GetProperty("periodoAquisitivoAtual").GetProperty("inicio").GetString(),
            resumo.GetProperty("periodoAquisitivoAtual").GetProperty("fim").GetString(),
            resumo.GetProperty("pedidosEmAnalise").GetInt32()));
        Assert.Equal(JsonValueKind.Null, resumo.GetProperty("proximasFerias").ValueKind);
        Assert.Equal(
            [("2022-03-14", "SEM_SALDO", 30, 0, 0), ("2023-03-14", "SEM_SALDO", 30, 0, 0), ("2024-03-14", "SEM_SALDO", 30, 0, 0), ("2025-03-14", "COM_SALDO", 12, 15, 18), ("2026-03-14", "EM_AQUISICAO", 0, 0, 0)],
            corpo.GetProperty("periodosAquisitivos").EnumerateArray().Select(p => (p.GetProperty("inicio").GetString()!, p.GetProperty("situacao").GetString()!, p.GetProperty("diasTirados").GetInt32(), p.GetProperty("diasEmPedido").GetInt32(), p.GetProperty("diasDisponiveis").GetInt32())));
        var atual = corpo.GetProperty("periodosAquisitivos")[4];
        Assert.Equal((6, "2027-03-14", "2028-03-13", 30), (atual.GetProperty("mesesTrabalhados").GetInt32(), atual.GetProperty("disponivelAPartirDe").GetString(), atual.GetProperty("prazoParaTirar").GetString(), atual.GetProperty("diasDeDireito").GetInt32()));
        Assert.Equal((api.Elenco.Gestor.FuncionarioId, "Carlos Ribeiro"), (corpo.GetProperty("gestorQueAnalisa").GetProperty("id").GetInt32(), corpo.GetProperty("gestorQueAnalisa").GetProperty("nome").GetString()));

        var historico = corpo.GetProperty("historico").EnumerateArray().ToList();
        Assert.Equal(["2026-11-16", "2026-04-13", "2025-04-14", "2024-04-15", "2023-04-17"], historico.Select(h => h.GetProperty("dataInicio").GetString()!));
        var pedido = historico[0];
        Assert.Equal(("SOLICITADA", "GESTOR", "COM_O_GESTOR", false, "Aguardando a análise do gestor.", 15, "2026-10-07T12:20:00Z", "Queria emendar com o feriado de 20/11."), (
            pedido.GetProperty("status").GetString(),
            pedido.GetProperty("etapa").GetString(),
            pedido.GetProperty("situacao").GetString(),
            pedido.GetProperty("podeDecidir").GetBoolean(),
            pedido.GetProperty("motivoNaoPodeDecidir").GetString(),
            pedido.GetProperty("dias").GetInt32(),
            pedido.GetProperty("solicitadaEmUtc").GetString(),
            pedido.GetProperty("observacao").GetString()));
        Assert.Equal(("Carlos Ribeiro", JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (pedido.GetProperty("gestorResponsavel").GetProperty("nome").GetString(), pedido.GetProperty("decisaoGestor").ValueKind, pedido.GetProperty("decisaoRh").ValueKind, pedido.GetProperty("sugestao").ValueKind));
        Assert.Equal(("2025-03-14", "2026-03-13", bruno.FuncionarioId), (pedido.GetProperty("periodoAquisitivo").GetProperty("inicio").GetString(), pedido.GetProperty("periodoAquisitivo").GetProperty("fim").GetString(), pedido.GetProperty("funcionario").GetProperty("id").GetInt32()));
        var concluido = historico[1];
        Assert.Equal(("CONCLUIDA", "CONCLUIDA", "CONCLUIDA", "Este pedido já foi decidido.", "APROVADA", "Carlos Ribeiro", "APROVADA", "Ana Souza", false), (
            concluido.GetProperty("status").GetString(),
            concluido.GetProperty("etapa").GetString(),
            concluido.GetProperty("situacao").GetString(),
            concluido.GetProperty("motivoNaoPodeDecidir").GetString(),
            concluido.GetProperty("decisaoGestor").GetProperty("decisao").GetString(),
            concluido.GetProperty("decisaoGestor").GetProperty("decididaPor").GetProperty("nome").GetString(),
            concluido.GetProperty("decisaoRh").GetProperty("decisao").GetString(),
            concluido.GetProperty("decisaoRh").GetProperty("decididaPor").GetProperty("nome").GetString(),
            concluido.GetProperty("decisaoGestor").GetProperty("automatica").GetBoolean()));
    }

    [Fact]
    public async Task So_a_propria_pessoa_ve_e_os_tres_perfis_chamam_a_rota()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, com340: true);
        var outra = await PessoaAsync(api, "Outra Pessoa", AdmissaoDoBruno, api.Elenco.SetorComercialId);

        var deOutra = await CorpoAsync(await (await api.ClienteAgoraAsync(outra.UsuarioId)).GetAsync(Rota));

        Assert.Equal(0, deOutra.GetProperty("historico").GetArrayLength());
        Assert.Equal(30 * 4, deOutra.GetProperty("resumo").GetProperty("diasDisponiveis").GetInt32());
        foreach (var cliente in new[] { await api.ClienteDoRhAgoraAsync(), await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId), await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId) })
        {
            Assert.Equal(200, (int)(await cliente.GetAsync(Rota)).StatusCode);
        }

        Assert.Equal(5, (await CorpoAsync(await (await api.ClienteAgoraAsync(bruno.UsuarioId)).GetAsync(Rota))).GetProperty("historico").GetArrayLength());
    }

    [Fact]
    public async Task O_gestor_que_analisa_e_nulo_para_quem_e_gestor_para_setor_sem_gestor_para_gestor_desligado_e_para_quem_nao_tem_setor()
    {
        var api = await ApiAsync();
        var semGestor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, $"Setor sem gestor {Guid.NewGuid():N}");
        var (setorDoDesligado, desligado, _) = await SetorComGestorAsync(api, "Setor do desligado");
        await AdvertenciasDeTeste_InativarAsync(api, desligado.FuncionarioId);
        var noSetorSemGestor = await PessoaAsync(api, "Sem gestor", AdmissaoDoBruno, semGestor);
        var comGestorDesligado = await PessoaAsync(api, "Gestor desligado", AdmissaoDoBruno, setorDoDesligado);
        var semSetor = await PessoaAsync(api, "Sem setor", AdmissaoDoBruno);
        var comGestorAtivo = await PessoaAsync(api, "Com gestor", AdmissaoDoBruno, api.Elenco.SetorComercialId);

        foreach (var pessoa in new[] { noSetorSemGestor, comGestorDesligado, semSetor })
        {
            var corpo = await CorpoAsync(await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(Rota));
            Assert.Equal(JsonValueKind.Null, corpo.GetProperty("gestorQueAnalisa").ValueKind);
        }

        var ele = await CorpoAsync(await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).GetAsync(Rota));
        var comGestor = await CorpoAsync(await (await api.ClienteAgoraAsync(comGestorAtivo.UsuarioId)).GetAsync(Rota));
        Assert.Equal(JsonValueKind.Null, ele.GetProperty("gestorQueAnalisa").ValueKind);
        Assert.Equal("Carlos Ribeiro", comGestor.GetProperty("gestorQueAnalisa").GetProperty("nome").GetString());
    }

    private static Task AdvertenciasDeTeste_InativarAsync(ApiDeAcesso api, int funcionarioId) => Comunicacao.AdvertenciasDeTeste.InativarAsync(api, funcionarioId);

    // ---------------------------------------------------------------- conferirPedidoDeFerias

    [Fact]
    public async Task A_conferencia_do_exemplo_do_contrato_lista_as_sete_regras_com_os_textos_e_nao_grava_nada()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, com340: false);
        var antes = LinhasDe(api, bruno.FuncionarioId);

        var resposta = await ConferirAsync(api, bruno, new { dataInicio = "2026-11-16", dataFim = "2026-11-30", abonoPecuniario = false, diasAbono = 0 });

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("conferirPedidoDeFerias");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((15, 3, true, "2026-11-14"), (corpo.GetProperty("totalDias").GetInt32(), corpo.GetProperty("diasQueSobram").GetInt32(), corpo.GetProperty("podeEnviar").GetBoolean(), corpo.GetProperty("pagamentoAte").GetString()));
        Assert.Equal(
            [
                ("DURACAO_DA_PARTE", true, "A parte tem entre 5 e 30 dias."),
                ("CABE_NO_SALDO", true, "Cabe no saldo disponível."),
                ("PARTE_DE_14_DIAS", true, "Tem pelo menos 14 dias seguidos."),
                ("MAXIMO_DE_3_PARTES", true, "É a 2ª parte do período."),
                ("ANTECEDENCIA_DE_30_DIAS", true, "Começa daqui a mais de 30 dias."),
                ("NAO_COMECA_PERTO_DE_FERIADO_OU_DESCANSO", true, "Não começa nos 2 dias antes de um feriado ou do fim de semana."),
                ("SEM_SOBREPOSICAO", true, "Não coincide com outras férias suas."),
            ],
            corpo.GetProperty("regras").EnumerateArray().Select(r => (r.GetProperty("codigo").GetString()!, r.GetProperty("atendida").GetBoolean(), r.GetProperty("mensagem").GetString()!)));
        Assert.Equal(antes, LinhasDe(api, bruno.FuncionarioId));
    }

    [Fact]
    public async Task A_conferencia_com_regra_falha_responde_200_com_podeEnviar_falso_e_a_mensagem_de_quantos_dias_faltam()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, com340: false);

        var resposta = await ConferirAsync(api, bruno, new { dataInicio = "2026-11-16", dataFim = "2026-12-07", abonoPecuniario = false, diasAbono = 0 }); // 22 dias para 18

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((22, -4, false), (corpo.GetProperty("totalDias").GetInt32(), corpo.GetProperty("diasQueSobram").GetInt32(), corpo.GetProperty("podeEnviar").GetBoolean()));
        var cabe = corpo.GetProperty("regras").EnumerateArray().Single(r => r.GetProperty("codigo").GetString() == "CABE_NO_SALDO");
        Assert.Equal((false, "Passa do saldo disponível: faltam 4 dias."), (cabe.GetProperty("atendida").GetBoolean(), cabe.GetProperty("mensagem").GetString()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"dataInicio\":\"2026-11-16\"}")]
    [InlineData("{\"dataInicio\":\"2026-11-30\",\"dataFim\":\"2026-11-16\"}")]
    [InlineData("{\"dataInicio\":\"2026-11-16\",\"dataFim\":\"2026-11-30\",\"abonoPecuniario\":true,\"diasAbono\":11}")]
    [InlineData("{\"dataInicio\":\"2026-11-16\",\"dataFim\":\"2026-11-30\",\"abonoPecuniario\":true,\"diasAbono\":-1}")]
    [InlineData("{\"dataInicio\":\"16/11/2026\",\"dataFim\":\"2026-11-30\"}")]
    public async Task A_conferencia_com_corpo_invalido_responde_400(string corpo)
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Conferencia 400", AdmissaoDoBruno, api.Elenco.SetorComercialId);

        var resposta = await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsync($"{Rota}/conferencia", new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("conferirPedidoDeFerias");
    }

    // ---------------------------------------------------------------- pedirFerias: a etapa em que nasce

    [Fact]
    public async Task O_pedido_de_quem_tem_gestor_nasce_SOLICITADA_na_etapa_do_gestor_sem_e_mail_e_com_a_data_em_menos_3_horas()
    {
        var api = await ApiAsync();
        var (bruno, carlos) = await BrunoAsync(api, com340: false);

        var resposta = await PedirAsync(api, bruno, Pedido("2026-11-16", "2026-11-30", mensagem: "Queria emendar com o feriado de 20/11."));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirFerias");
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("SOLICITADA", "GESTOR", "COM_O_GESTOR", false, "Aguardando a análise do gestor.", 15, "2026-10-07T12:42:00Z", "Queria emendar com o feriado de 20/11.", "2025-03-14", "2026-03-13"), (
            pedido.GetProperty("status").GetString(),
            pedido.GetProperty("etapa").GetString(),
            pedido.GetProperty("situacao").GetString(),
            pedido.GetProperty("podeDecidir").GetBoolean(),
            pedido.GetProperty("motivoNaoPodeDecidir").GetString(),
            pedido.GetProperty("dias").GetInt32(),
            pedido.GetProperty("solicitadaEmUtc").GetString(),
            pedido.GetProperty("observacao").GetString(),
            pedido.GetProperty("periodoAquisitivo").GetProperty("inicio").GetString(),
            pedido.GetProperty("periodoAquisitivo").GetProperty("fim").GetString()));
        Assert.Equal(("Carlos Ribeiro", JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (pedido.GetProperty("gestorResponsavel").GetProperty("nome").GetString(), pedido.GetProperty("decisaoGestor").ValueKind, pedido.GetProperty("decisaoRh").ValueKind, pedido.GetProperty("sugestao").ValueKind));
        var id = pedido.GetProperty("id").GetInt32();
        Assert.Equal(("SOLICITADA", "2026-10-07 09:42:00", carlos.ToString(System.Globalization.CultureInfo.InvariantCulture)), (
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {id}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_SOLICITACAO, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {id}"),
            await TextoAsync(api, $"SELECT ID_GESTOR FROM FERIAS WHERE ID_FERIAS = {id}")));
        Assert.Equal(0, EmailsDe(api, bruno.FuncionarioId));
    }

    [Fact]
    public async Task O_pedido_de_quem_e_gestor_de_setor_sem_gestor_ativo_ou_sem_setor_nasce_direto_na_etapa_do_RH_sem_gestor_responsavel()
    {
        var api = await ApiAsync();
        var semGestor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, $"Setor sem gestor {Guid.NewGuid():N}");
        var (setorDoDesligado, desligado, _) = await SetorComGestorAsync(api, "Setor do desligado");
        await AdvertenciasDeTeste_InativarAsync(api, desligado.FuncionarioId);
        var gestorComPeriodo = await PessoaAsync(api, "Gestor com ferias", AdmissaoDoBruno, perfil: Perfil.Gestor);
        await ApoioDeTeste.GarantirGestorAsync(api.Conexao, gestorComPeriodo.FuncionarioId);
        var pessoas = new[]
        {
            gestorComPeriodo,
            await PessoaAsync(api, "Sem gestor", AdmissaoDoBruno, semGestor),
            await PessoaAsync(api, "Gestor desligado", AdmissaoDoBruno, setorDoDesligado),
            await PessoaAsync(api, "Sem setor", AdmissaoDoBruno),
        };

        foreach (var pessoa in pessoas)
        {
            var resposta = await PedirAsync(api, pessoa, Pedido("2026-11-16", "2026-11-25"));

            Assert.Equal(201, (int)resposta.StatusCode);
            var pedido = await CorpoAsync(resposta);
            Assert.Equal(("RH", "COM_O_RH", JsonValueKind.Null, JsonValueKind.Null, "Aguardando a análise do RH."), (
                pedido.GetProperty("etapa").GetString(),
                pedido.GetProperty("situacao").GetString(),
                pedido.GetProperty("gestorResponsavel").ValueKind,
                pedido.GetProperty("decisaoGestor").ValueKind,
                pedido.GetProperty("motivoNaoPodeDecidir").GetString()));
            Assert.Equal(("SOLICITADA", (string?)null, (string?)null), (
                await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {pedido.GetProperty("id").GetInt32()}"),
                await TextoAsync(api, $"SELECT ID_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido.GetProperty("id").GetInt32()}"),
                await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {pedido.GetProperty("id").GetInt32()}")));
            Assert.Equal(0, EmailsDe(api, pessoa.FuncionarioId));
        }
    }

    [Fact]
    public async Task Com_o_gestor_ausente_a_etapa_dele_ja_nasce_aprovada_automaticamente_sem_e_mail_e_o_pedido_vai_para_o_RH()
    {
        var api = await ApiAsync();
        var (setor, gestor, gestorId) = await SetorComGestorAsync(api, "Setor do ausente");
        var pessoa = await PessoaAsync(api, "Do ausente", AdmissaoDoBruno, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE WHERE ID_GESTOR = {gestorId}");

        var resposta = await PedirAsync(api, pessoa, Pedido("2026-11-16", "2026-11-25"));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirFerias");
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("RH", "COM_O_RH", gestor.FuncionarioId), (pedido.GetProperty("etapa").GetString(), pedido.GetProperty("situacao").GetString(), pedido.GetProperty("gestorResponsavel").GetProperty("id").GetInt32()));
        var decisao = pedido.GetProperty("decisaoGestor");
        Assert.Equal(("APROVADA", true, JsonValueKind.Null, "2026-10-07T12:42:00Z"), (decisao.GetProperty("decisao").GetString(), decisao.GetProperty("automatica").GetBoolean(), decisao.GetProperty("decididaPor").ValueKind, decisao.GetProperty("decididaEmUtc").GetString()));
        var id = pedido.GetProperty("id").GetInt32();
        Assert.Equal(("APROVOU", "1", "2026-10-07 09:42:00"), (
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {id}"),
            await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA + 0 FROM FERIAS WHERE ID_FERIAS = {id}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ETAPA_GESTOR, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {id}")));
        Assert.Equal(0, EmailsDe(api, pessoa.FuncionarioId));
        var minhas = await CorpoAsync(await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(Rota));
        Assert.Equal(gestor.FuncionarioId, minhas.GetProperty("gestorQueAnalisa").GetProperty("id").GetInt32()); // o contrato só põe nulo nos três casos: o gestor ausente continua sendo "quem analisa"
    }

    // ---------------------------------------------------------------- pedirFerias: período, regras e erros

    [Fact]
    public async Task O_pedido_entra_no_periodo_mais_antigo_que_ainda_tem_saldo_para_os_dias_pedidos()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Periodo mais antigo", AdmissaoDoBruno, api.Elenco.SetorComercialId); // quatro períodos fechados, nenhum usado

        var resposta = await PedirAsync(api, pessoa, Pedido("2026-11-16", "2026-11-25"));

        Assert.Equal(201, (int)resposta.StatusCode);
        var periodo = (await CorpoAsync(resposta)).GetProperty("periodoAquisitivo");
        Assert.Equal(("2022-03-14", "2023-03-13"), (periodo.GetProperty("inicio").GetString(), periodo.GetProperty("fim").GetString()));
    }

    [Fact]
    public async Task Regra_nao_atendida_vira_422_com_a_lista_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, com340: false);
        var antes = LinhasDe(api, bruno.FuncionarioId);

        var passaDoSaldo = await PedirAsync(api, bruno, Pedido("2026-11-16", "2026-12-07")); // 22 dias para 18
        var semAntecedencia = await PedirAsync(api, bruno, Pedido("2026-11-04", "2026-11-18")); // 28 dias depois de hoje (quarta: não cai na regra do descanso)
        var abonoIncoerente = await PedirAsync(api, bruno, Pedido("2026-11-16", "2026-11-30", abono: true, diasAbono: 0));

        foreach (var resposta in new[] { passaDoSaldo, semAntecedencia, abonoIncoerente })
        {
            Assert.Equal(422, (int)resposta.StatusCode);
            await resposta.AfirmarConformeAoContratoAsync("pedirFerias");
            var problema = await CorpoAsync(resposta);
            Assert.Equal(("FERIAS_REGRAS_NAO_ATENDIDAS", "Pedido de férias recusado.", "O pedido não cumpre uma ou mais regras de férias."), (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString()));
        }

        var regrasQueFalharam = async (HttpResponseMessage r) => (await CorpoAsync(r)).GetProperty("regras").EnumerateArray().Where(x => !x.GetProperty("atendida").GetBoolean()).Select(x => x.GetProperty("codigo").GetString()!).ToList();
        Assert.Equal(["CABE_NO_SALDO"], await regrasQueFalharam(passaDoSaldo));
        Assert.Equal(["ANTECEDENCIA_DE_30_DIAS"], await regrasQueFalharam(semAntecedencia));
        Assert.Equal(["ABONO_ATE_10_DIAS"], await regrasQueFalharam(abonoIncoerente));
        Assert.Equal(antes, LinhasDe(api, bruno.FuncionarioId));
    }

    [Fact]
    public async Task Quem_ainda_nao_completou_um_ano_recebe_422_FERIAS_SEM_PERIODO_AQUISITIVO_e_pessoa_inativa_recebe_422_FUNCIONARIO_INATIVO()
    {
        var api = await ApiAsync();

        var semPeriodo = await PedirAsync(api, new PessoaDeFerias(api.Elenco.MembroA.FuncionarioId, api.Elenco.MembroA.UsuarioId, "Diego"), Pedido("2026-11-16", "2026-11-25"));
        var inativa = await PedirAsync(api, new PessoaDeFerias(api.Elenco.Inativo.FuncionarioId, api.Elenco.Inativo.UsuarioId, "Henrique"), Pedido("2026-11-16", "2026-11-25"));

        Assert.Equal(422, (int)semPeriodo.StatusCode);
        await semPeriodo.AfirmarConformeAoContratoAsync("pedirFerias");
        Assert.Equal("FERIAS_SEM_PERIODO_AQUISITIVO", Codigo(await CorpoAsync(semPeriodo)));
        Assert.Equal(422, (int)inativa.StatusCode);
        await inativa.AfirmarConformeAoContratoAsync("pedirFerias");
        var problema = await CorpoAsync(inativa);
        Assert.Equal(("FUNCIONARIO_INATIVO", "Funcionario inativo nao pode solicitar ferias."), (Codigo(problema), problema.GetProperty("regraDoBanco").GetString()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"dataInicio\":\"2026-11-30\",\"dataFim\":\"2026-11-16\"}")]
    [InlineData("{\"dataInicio\":\"2026-11-16\",\"dataFim\":\"2026-11-30\",\"abonoPecuniario\":true,\"diasAbono\":11}")]
    [InlineData("{\"dataInicio\":\"2026-11-16\",\"dataFim\":\"2026-11-30\",\"mensagem\":\"__501__\"}")]
    public async Task O_corpo_invalido_do_pedido_responde_400_e_nao_grava(string corpo)
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, com340: false);
        var antes = LinhasDe(api, bruno.FuncionarioId);
        var enviado = corpo.Replace("__501__", new string('x', 501), StringComparison.Ordinal);

        var resposta = await (await api.ClienteAgoraAsync(bruno.UsuarioId)).PostAsync(Rota, new StringContent(enviado, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirFerias");
        Assert.Equal(antes, LinhasDe(api, bruno.FuncionarioId));
    }

    [Fact]
    public async Task A_mensagem_de_500_caracteres_vale_e_o_pedido_audita_sem_a_mensagem()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, com340: false);
        var mensagem = "Preciso de uma semana para tratar de assunto pessoal. " + new string('y', 440);
        Assert.True(mensagem.Length <= 500);

        var resposta = await PedirAsync(api, bruno, Pedido("2026-11-16", "2026-11-30", mensagem: mensagem));

        Assert.Equal(201, (int)resposta.StatusCode);
        var id = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "FERIAS", id));
        Assert.Equal((RHSystem.Domain.Auditoria.AuditoriaArea.Ferias, RHSystem.Domain.Auditoria.AuditoriaAcao.Criou, bruno.FuncionarioId), (evento.Area, evento.Acao, evento.FuncionarioAtorId));
        Assert.DoesNotContain("assunto pessoal", evento.Descricao, StringComparison.Ordinal);
    }

    /// <summary>Segura os dois pedidos no ponto entre conferir e gravar: com a pessoa travada só um chega lá e o outro espera o commit; sem a trava os dois chegam juntos e os dois gravam.</summary>
    private sealed class Barreira
    {
        private readonly TaskCompletionSource _aberta = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _chegaram;

        public async Task AguardarAsync()
        {
            if (Interlocked.Increment(ref _chegaram) >= 2)
            {
                _aberta.TrySetResult();
            }

            await Task.WhenAny(_aberta.Task, Task.Delay(1500));
        }
    }

    [Fact]
    public async Task Dois_pedidos_ao_mesmo_tempo_nunca_reservam_mais_de_30_dias_e_nenhum_da_500()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Corrida", new DateOnly(2025, 3, 14), api.Elenco.SetorComercialId); // um período fechado com 30 dias
        var barreira = new Barreira();
        using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFeriasRepositorio>();
            s.AddScoped<FeriasRepositorio>();
            s.AddScoped<IFeriasRepositorio>(sp => new RepositorioDeFeriasComGancho(sp.GetRequiredService<FeriasRepositorio>()) { AntesDeCriar = barreira.AguardarAsync });
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(pessoa.UsuarioId)).DefaultRequestHeaders.Authorization;

        // 16 + 16 = 32 > 30: pela regra só um dos dois pode entrar (a pessoa é travada antes de conferir)
        var respostas = await Task.WhenAll(cliente.PostAsJsonAsync(Rota, Pedido("2026-11-16", "2026-12-01")), cliente.PostAsJsonAsync(Rota, Pedido("2027-01-11", "2027-01-26")));

        Assert.Equal([201, 422], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(1, LinhasDe(api, pessoa.FuncionarioId));
        Assert.Equal(16, Contar(api, $"SELECT SUM(DATEDIFF(DATA_FIM, DATA_INICIO) + 1) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
    }
}
