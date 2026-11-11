using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// <c>GET /api/gestor/ponto/equipe</c>, no banco REAL. A equipe de Carlos Ribeiro (Comercial) é Diego Farias e Júlia Nunes; a de Marina Costa (Financeiro) é Bruno Lima. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília; empresa 08:00 +
/// 10 min). Diego entrou às 08:10 e está dentro; Júlia está de férias de 05 a 17/10; Diego tem pedido na etapa do gestor (dia 05) e Júlia tem pedido na etapa do RH (dia 05); Bruno tem pedido na etapa de Marina.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PontoDaEquipeTests(MySqlFixture mysql)
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static bool _semeado;

    private async Task<ApiDeAcesso> ApiAsync(DateTime? agora = null)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PontoDaEquipeTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(agora ?? ElencoDeTeste.Hoje);
        await SemearAsync(api);
        return api;
    }

    private static async Task SemearAsync(ApiDeAcesso api)
    {
        await Trava.WaitAsync();
        try
        {
            if (_semeado)
            {
                return;
            }

            var e = api.Elenco;
            var conexao = api.Conexao;
            var cargo = await SementeDoCadastro.CargoAsync(conexao, "Cargo da equipe", 44m);
            await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO IN ({e.MembroA.FuncionarioId}, {e.MembroB.FuncionarioId}, {e.DeOutroSetor.FuncionarioId})");
            var carlos = (int)await Sql.ContarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {e.Gestor.FuncionarioId}");
            var marina = (int)await Sql.ContarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {e.GestorDoFinanceiro.FuncionarioId}");

            await PontoDeTeste.BaterEmAsync(conexao, e.MembroA.FuncionarioId, new DateOnly(2026, 10, 7), (TipoDeMarcacao.Entrada, "2026-10-07T11:10:00Z"));
            await PontoDeTeste.BaterEmAsync(conexao, e.MembroA.FuncionarioId, new DateOnly(2026, 10, 6), (TipoDeMarcacao.Entrada, "2026-10-06T11:00:00Z"), (TipoDeMarcacao.Saida, "2026-10-06T15:00:00Z"), (TipoDeMarcacao.Entrada, "2026-10-06T16:00:00Z"), (TipoDeMarcacao.Saida, "2026-10-06T20:48:00Z"));
            await PontoDeTeste.NovasFeriasAsync(conexao, e.MembroB.FuncionarioId, "2026-10-05", "2026-10-17", "APROVADA");
            await PontoDeTeste.NovoLancamentoAsync(conexao, e.MembroA.FuncionarioId, "2026-10-05", "CREDITO", "1.00");
            await PontoDeTeste.NovoLancamentoAsync(conexao, e.MembroA.FuncionarioId, "2026-10-06", "CREDITO", "5.33"); // 60 + 320 = 380 por linha
            await PontoDeTeste.NovoPedidoPendenteAsync(conexao, e.MembroA.FuncionarioId, new DateOnly(2026, 10, 5), carlos); // etapa do gestor
            await PontoDeTeste.NovoPedidoPendenteAsync(conexao, e.MembroB.FuncionarioId, new DateOnly(2026, 10, 5)); // etapa do RH (sem gestor): não conta para ele
            await PontoDeTeste.NovoPedidoPendenteAsync(conexao, e.DeOutroSetor.FuncionarioId, new DateOnly(2026, 10, 5), marina); // etapa de OUTRO gestor
            await PontoDeTeste.NovoPedidoPendenteAsync(conexao, e.MembroB.FuncionarioId, new DateOnly(2026, 10, 1), carlos, gestorJaAprovou: true); // o gestor já aprovou: está no RH e não conta como "para analisar"
            await PontoDeTeste.NovoPedidoPendenteAsync(conexao, e.MembroB.FuncionarioId, new DateOnly(2026, 10, 2), marina); // pessoa da equipe do Carlos com o ID_GESTOR de outra pessoa (o gestor antigo): não conta para o Carlos
            _semeado = true;
        }
        finally
        {
            Trava.Release();
        }
    }

    private static async Task<HttpResponseMessage> PontoAsync(ApiDeAcesso api, PessoaDeTeste gestor, string consulta = "") =>
        await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync($"/api/gestor/ponto/equipe{consulta}");

    private static JsonElement Linha(JsonElement dia, string nome) => dia.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("funcionario").GetProperty("nome").GetString() == nome);

    private static int?[] Contagens(JsonElement dia)
    {
        var c = dia.GetProperty("resumo").GetProperty("contagensHoje");
        return [.. new[] { "trabalhando", "ferias", "atestado", "semRegistro", "aindaNaoComecou", "semExpediente" }.Select(n => (int?)c.GetProperty(n).GetInt32())];
    }

    [Fact]
    public async Task Hoje_Diego_esta_dentro_ha_92_minutos_e_Julia_esta_de_ferias()
    {
        var api = await ApiAsync();

        var resposta = await PontoAsync(api, api.Elenco.Gestor);

        Assert.Equal(200, (int)resposta.StatusCode);
        var dia = await CorpoAsync(resposta);
        Assert.Equal("2026-10-07", dia.GetProperty("data").GetString());
        Assert.Equal(["Diego Farias", "Júlia Nunes"], dia.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("nome").GetString()!)); // por nome, sem o gestor
        var diego = Linha(dia, "Diego Farias");
        Assert.Equal((92, 380, "TRABALHANDO", "EM_ANDAMENTO", true), (diego.GetProperty("horasAteAgoraMinutos").GetInt32(), diego.GetProperty("saldoBancoHorasMinutos").GetInt32(), diego.GetProperty("situacaoHoje").GetString(), diego.GetProperty("situacaoDoDia").GetString(), diego.GetProperty("periodoAberto").GetBoolean()));
        Assert.Equal(1, diego.GetProperty("periodos").GetArrayLength());
        var julia = Linha(dia, "Júlia Nunes");
        Assert.Equal((JsonValueKind.Null, 0, "FERIAS", "FERIAS", false, 0), (julia.GetProperty("horasAteAgoraMinutos").ValueKind, julia.GetProperty("saldoBancoHorasMinutos").GetInt32(), julia.GetProperty("situacaoHoje").GetString(), julia.GetProperty("situacaoDoDia").GetString(), julia.GetProperty("periodoAberto").GetBoolean(), julia.GetProperty("periodos").GetArrayLength()));
    }

    [Fact]
    public async Task O_resumo_de_hoje_tem_as_contagens_a_lista_de_ferias_com_o_ultimo_dia_e_so_os_pedidos_da_etapa_deste_gestor()
    {
        var api = await ApiAsync();

        var resumo = (await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor))).GetProperty("resumo");

        Assert.Equal(new int?[] { 1, 1, 0, 0, 0, 0 }, Contagens(await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor)))); // trabalhando, férias, atestado, sem registro, ainda não começou, sem expediente
        Assert.True(resumo.GetProperty("diaUtil").GetBoolean());
        Assert.Equal(0, resumo.GetProperty("pessoasSemRegistro").GetArrayLength());
        var ferias = resumo.GetProperty("pessoasDeFerias").EnumerateArray().Single();
        Assert.Equal(("Júlia Nunes", "2026-10-17"), (ferias.GetProperty("funcionario").GetProperty("nome").GetString(), ferias.GetProperty("ate").GetString()));
        Assert.Equal(1, resumo.GetProperty("pedidosParaAnalisar").GetInt32()); // o de Diego (etapa do gestor); o de Júlia está no RH e o de Bruno é de outra equipe
    }

    [Fact]
    public async Task As_contagens_de_hoje_sao_as_mesmas_da_lista_da_equipe_da_fatia_1()
    {
        var api = await ApiAsync();
        var ponto = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor));

        var equipe = await CorpoAsync(await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).GetAsync("/api/gestor/equipe"));

        var daLista = equipe.GetProperty("contagensHoje");
        Assert.Equal(Contagens(ponto), new[] { "trabalhando", "ferias", "atestado", "semRegistro", "aindaNaoComecou", "semExpediente" }.Select(n => (int?)daLista.GetProperty(n).GetInt32()));
        Assert.Equal(equipe.GetProperty("diaUtil").GetBoolean(), ponto.GetProperty("resumo").GetProperty("diaUtil").GetBoolean());
    }

    [Fact]
    public async Task Quem_nao_bateu_depois_do_horario_mais_a_tolerancia_entra_em_pessoasSemRegistro()
    {
        var api = await ApiAsync(new DateTime(2026, 10, 8, 12, 42, 0, DateTimeKind.Utc)); // quinta, 09:42: Diego não bateu

        var dia = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor));

        var semRegistro = dia.GetProperty("resumo").GetProperty("pessoasSemRegistro").EnumerateArray().Single();
        Assert.Equal("Diego Farias", semRegistro.GetProperty("nome").GetString());
        Assert.Equal(new int?[] { 0, 1, 0, 1, 0, 0 }, Contagens(dia));
        Assert.Equal(("SEM_REGISTRO", "SEM_REGISTRO", JsonValueKind.Null), (Linha(dia, "Diego Farias").GetProperty("situacaoHoje").GetString(), Linha(dia, "Diego Farias").GetProperty("situacaoDoDia").GetString(), Linha(dia, "Diego Farias").GetProperty("horasAteAgoraMinutos").ValueKind));
    }

    [Fact]
    public async Task Num_dia_sem_expediente_todos_ficam_SEM_EXPEDIENTE_e_as_listas_do_resumo_ficam_vazias()
    {
        var api = await ApiAsync(new DateTime(2026, 10, 10, 12, 42, 0, DateTimeKind.Utc)); // sábado

        var dia = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor));

        var resumo = dia.GetProperty("resumo");
        Assert.False(resumo.GetProperty("diaUtil").GetBoolean());
        Assert.Equal(new int?[] { 0, 0, 0, 0, 0, 2 }, Contagens(dia)); // a férias de Júlia vem depois de SEM_EXPEDIENTE
        Assert.Equal((0, 0), (resumo.GetProperty("pessoasSemRegistro").GetArrayLength(), resumo.GetProperty("pessoasDeFerias").GetArrayLength()));
        Assert.All(dia.GetProperty("itens").EnumerateArray(), i => Assert.Equal("FOLGA", i.GetProperty("situacaoDoDia").GetString()));
    }

    // ---------------------------------------------------------------- dia passado

    [Fact]
    public async Task Dia_passado_nao_tem_situacaoHoje_nem_contagensHoje_e_o_chip_vem_da_situacaoDoDia()
    {
        var api = await ApiAsync();

        var resposta = await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-06");

        Assert.Equal(200, (int)resposta.StatusCode);
        var dia = await CorpoAsync(resposta);
        Assert.Equal("2026-10-06", dia.GetProperty("data").GetString());
        Assert.Equal(JsonValueKind.Null, dia.GetProperty("resumo").GetProperty("contagensHoje").ValueKind);
        Assert.All(dia.GetProperty("itens").EnumerateArray(), i => Assert.Equal(JsonValueKind.Null, i.GetProperty("situacaoHoje").ValueKind));
        var diego = Linha(dia, "Diego Farias");
        Assert.Equal(("NORMAL", 528, false, 2), (diego.GetProperty("situacaoDoDia").GetString(), diego.GetProperty("horasAteAgoraMinutos").GetInt32(), diego.GetProperty("periodoAberto").GetBoolean(), diego.GetProperty("periodos").GetArrayLength()));
        Assert.Equal(("Júlia Nunes", "2026-10-17"), (dia.GetProperty("resumo").GetProperty("pessoasDeFerias")[0].GetProperty("funcionario").GetProperty("nome").GetString(), dia.GetProperty("resumo").GetProperty("pessoasDeFerias")[0].GetProperty("ate").GetString()));
    }

    [Fact]
    public async Task Dia_passado_com_pedido_em_analise_e_AJUSTE_EM_ANALISE_mesmo_sem_batida()
    {
        var api = await ApiAsync();

        var dia = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-05"));

        Assert.Equal(("AJUSTE_EM_ANALISE", JsonValueKind.Null), (Linha(dia, "Diego Farias").GetProperty("situacaoDoDia").GetString(), Linha(dia, "Diego Farias").GetProperty("horasAteAgoraMinutos").ValueKind));
        Assert.Equal(1, dia.GetProperty("resumo").GetProperty("pedidosParaAnalisar").GetInt32());
    }

    [Fact]
    public async Task Quem_foi_admitido_no_proprio_dia_consultado_aparece_nele_e_quem_entra_depois_fica_de_fora()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB.FuncionarioId;
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '2026-10-06' WHERE ID_FUNCIONARIO = {julia}");
        try
        {
            var noDiaDaAdmissao = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-06"));
            var antesDaAdmissao = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-05"));

            Assert.Contains(noDiaDaAdmissao.GetProperty("itens").EnumerateArray(), i => i.GetProperty("funcionario").GetProperty("nome").GetString() == "Júlia Nunes");
            Assert.DoesNotContain(antesDaAdmissao.GetProperty("itens").EnumerateArray(), i => i.GetProperty("funcionario").GetProperty("nome").GetString() == "Júlia Nunes");
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '2026-01-02' WHERE ID_FUNCIONARIO = {julia}");
        }
    }

    [Fact]
    public async Task Data_de_hoje_explicita_e_igual_a_padrao_e_data_futura_ou_invalida_e_400()
    {
        var api = await ApiAsync();

        var padrao = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor));
        var explicita = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-07"));
        var futura = await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-08");
        var invalida = await PontoAsync(api, api.Elenco.Gestor, "?data=2026-13-45");

        Assert.Equal(padrao.ToString(), explicita.ToString());
        Assert.Equal(400, (int)futura.StatusCode);
        Assert.True((await CorpoAsync(futura)).GetProperty("errors").TryGetProperty("data", out _));
        Assert.Equal(400, (int)invalida.StatusCode);
    }

    [Fact]
    public async Task Feriado_passado_e_dia_sem_expediente_e_a_situacaoDoDia_e_FOLGA_ou_FERIADO()
    {
        var api = await ApiAsync();

        var dia = await CorpoAsync(await PontoAsync(api, api.Elenco.Gestor, "?data=2026-09-07")); // Independência, segunda

        Assert.False(dia.GetProperty("resumo").GetProperty("diaUtil").GetBoolean());
        Assert.All(dia.GetProperty("itens").EnumerateArray(), i => Assert.Equal("FERIADO", i.GetProperty("situacaoDoDia").GetString()));
    }

    // ---------------------------------------------------------------- quem vê

    [Fact]
    public async Task Cada_gestor_ve_a_propria_equipe_e_o_gestor_sem_setor_ve_a_lista_vazia_sem_erro()
    {
        var api = await ApiAsync();

        var marina = await CorpoAsync(await PontoAsync(api, api.Elenco.GestorDoFinanceiro));
        var semSetor = await CorpoAsync(await PontoAsync(api, api.Elenco.GestorSemSetor));

        Assert.Equal(["Bruno Lima"], marina.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("nome").GetString()!));
        Assert.Equal(1, marina.GetProperty("resumo").GetProperty("pedidosParaAnalisar").GetInt32()); // o de Bruno, na etapa dela
        Assert.Equal((0, true, 0), (semSetor.GetProperty("itens").GetArrayLength(), semSetor.GetProperty("resumo").GetProperty("diaUtil").GetBoolean(), semSetor.GetProperty("resumo").GetProperty("pedidosParaAnalisar").GetInt32()));
        Assert.Equal(new int?[] { 0, 0, 0, 0, 0, 0 }, Contagens(semSetor));
    }

    [Fact]
    public async Task So_o_gestor_acessa_e_sem_login_e_401()
    {
        var api = await ApiAsync();

        Assert.Equal(403, (int)(await PontoAsync(api, api.Elenco.MembroA)).StatusCode);
        Assert.Equal(403, (int)(await PontoAsync(api, api.Elenco.Rh)).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync("/api/gestor/ponto/equipe")).StatusCode);
    }

    [Fact]
    public async Task As_respostas_do_ponto_da_equipe_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();

        await (await PontoAsync(api, api.Elenco.Gestor)).AfirmarConformeAoContratoAsync("obterPontoDaEquipe");
        await (await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-06")).AfirmarConformeAoContratoAsync("obterPontoDaEquipe");
        await (await PontoAsync(api, api.Elenco.Gestor, "?data=2026-10-08")).AfirmarConformeAoContratoAsync("obterPontoDaEquipe"); // 400
        await (await PontoAsync(api, api.Elenco.MembroA)).AfirmarConformeAoContratoAsync("obterPontoDaEquipe"); // 403
    }
}
