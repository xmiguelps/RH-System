using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// O espelho de ponto do mês pelas três rotas (<c>/api/eu/ponto/espelho</c>, <c>/api/gestor/equipe/{id}/ponto</c> e <c>/api/rh/funcionarios/{id}/ponto</c>), no banco REAL, com o relógio em 2026-10-07T12:42Z (quarta) e a empresa em 08:00 com 10
/// minutos de tolerância. Diego Farias (<c>MembroA</c>) tem o outubro do exemplo do contrato; cada outro teste usa a pessoa e o mês dele.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EspelhoDoPontoTests(MySqlFixture mysql)
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static bool _semeado;
    private static int _pedidoDoDia2;
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(EspelhoDoPontoTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
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
            var cargo = await SementeDoCadastro.CargoAsync(conexao, "Cargo do espelho", 44m);
            await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO IN ({e.MembroA.FuncionarioId}, {e.MembroB.FuncionarioId}, {e.DeOutroSetor.FuncionarioId}, {e.GestorSemSetor.FuncionarioId})");

            // Outubro de Diego, o do exemplo do contrato: 1600 minutos no mês, 1 atraso, 1 pedido em análise, saldo 380.
            var diego = e.MembroA.FuncionarioId;
            await PontoDeTeste.BaterEmAsync(conexao, diego, new DateOnly(2026, 10, 1), (TipoDeMarcacao.Entrada, "2026-10-01T10:56:00Z"), (TipoDeMarcacao.Saida, "2026-10-01T15:01:00Z"), (TipoDeMarcacao.Entrada, "2026-10-01T16:00:00Z"), (TipoDeMarcacao.Saida, "2026-10-01T20:49:00Z"));
            await PontoDeTeste.BaterEmAsync(conexao, diego, new DateOnly(2026, 10, 2), (TipoDeMarcacao.Entrada, "2026-10-02T11:03:00Z"), (TipoDeMarcacao.Saida, "2026-10-02T15:00:00Z"), (TipoDeMarcacao.Entrada, "2026-10-02T16:02:00Z"));
            var gestorCarlos = (int)await Sql.ContarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {e.Gestor.FuncionarioId}");
            _pedidoDoDia2 = await PontoDeTeste.NovoPedidoPendenteAsync(conexao, diego, new DateOnly(2026, 10, 2), gestorCarlos);
            await PontoDeTeste.NovoPedidoRejeitadoAsync(conexao, diego, new DateOnly(2026, 10, 1), gestorCarlos); // um pedido já recusado não é "em análise": o dia 1 continua NORMAL e o resumo conta só o do dia 2
            await PontoDeTeste.BaterEmAsync(conexao, diego, new DateOnly(2026, 10, 5), (TipoDeMarcacao.Entrada, "2026-10-05T11:14:00Z"), (TipoDeMarcacao.Saida, "2026-10-05T15:05:00Z"), (TipoDeMarcacao.Entrada, "2026-10-05T16:05:00Z"), (TipoDeMarcacao.Saida, "2026-10-05T21:00:00Z"));
            await PontoDeTeste.BaterEmAsync(conexao, diego, new DateOnly(2026, 10, 6), (TipoDeMarcacao.Entrada, "2026-10-06T10:58:00Z"), (TipoDeMarcacao.Saida, "2026-10-06T15:00:00Z"), (TipoDeMarcacao.Entrada, "2026-10-06T16:00:00Z"));
            await PontoDeTeste.BaterCorrigidaPeloRhAsync(conexao, diego, new DateOnly(2026, 10, 6), TipoDeMarcacao.Saida, "2026-10-06T20:58:00Z", "2026-10-07T12:20:00Z");
            await PontoDeTeste.NovoAtestadoAsync(conexao, diego, "2026-10-07", "2026-10-07", "VALIDADO", e.Rh.FuncionarioId);
            await PontoDeTeste.NovoLancamentoAsync(conexao, diego, "2026-10-06", "CREDITO", "6.33"); // 6,33 h = 379,8 min: 380 por linha

            // Setembro de Bruno (DeOutroSetor): batida no feriado de 07/09 (Independência) e férias aprovadas de 14 a 18.
            await PontoDeTeste.BaterEmAsync(conexao, e.DeOutroSetor.FuncionarioId, new DateOnly(2026, 9, 7), (TipoDeMarcacao.Entrada, "2026-09-07T12:00:00Z"), (TipoDeMarcacao.Saida, "2026-09-07T16:00:00Z"));
            await PontoDeTeste.NovasFeriasAsync(conexao, e.DeOutroSetor.FuncionarioId, "2026-09-14", "2026-09-18", "APROVADA");

            // Paulo (GestorSemSetor): atestado de 2 horas no dia 02/09 e uma entrada sem saída em 05/10.
            await PontoDeTeste.NovoAtestadoAsync(conexao, e.GestorSemSetor.FuncionarioId, "2026-09-02", "2026-09-02", "VALIDADO", e.Rh.FuncionarioId, "09:00", "11:00");
            await PontoDeTeste.NovoAtestadoAsync(conexao, e.GestorSemSetor.FuncionarioId, "2026-09-01", "2026-09-01", "VALIDADO", e.Rh.FuncionarioId); // de dia inteiro, no primeiro dia do mês pedido
            await PontoDeTeste.BaterEmAsync(conexao, e.GestorSemSetor.FuncionarioId, new DateOnly(2026, 10, 5), (TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"));
            _semeado = true;
        }
        finally
        {
            Trava.Release();
        }
    }

    private static async Task<HttpResponseMessage> EuAsync(ApiDeAcesso api, PessoaDeTeste pessoa, string consulta = "") =>
        await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync($"/api/eu/ponto/espelho{consulta}");

    private static async Task<HttpResponseMessage> RhAsync(ApiDeAcesso api, int funcionarioId, string consulta = "") =>
        await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{funcionarioId}/ponto{consulta}");

    private static async Task<HttpResponseMessage> GestorAsync(ApiDeAcesso api, PessoaDeTeste gestor, int funcionarioId, string consulta = "") =>
        await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync($"/api/gestor/equipe/{funcionarioId}/ponto{consulta}");

    private static JsonElement Dia(JsonElement espelho, string data) => espelho.GetProperty("dias").EnumerateArray().Single(d => d.GetProperty("data").GetString() == data);

    private static int? Inteiro(JsonElement dia, string campo) => dia.GetProperty(campo).ValueKind == JsonValueKind.Null ? null : dia.GetProperty(campo).GetInt32();

    // ---------------------------------------------------------------- o espelho do contrato

    [Fact]
    public async Task O_espelho_de_outubro_de_Diego_e_o_do_exemplo_do_contrato()
    {
        var api = await ApiAsync();

        var resposta = await EuAsync(api, api.Elenco.MembroA, "?ano=2026&mes=10");

        Assert.Equal(200, (int)resposta.StatusCode);
        var espelho = await CorpoAsync(resposta);
        Assert.Equal((2026, 10), (espelho.GetProperty("ano").GetInt32(), espelho.GetProperty("mes").GetInt32()));
        var jornada = espelho.GetProperty("jornada");
        Assert.Equal((2640, 528, "08:00", "17:00", 10), (jornada.GetProperty("cargaSemanalMinutos").GetInt32(), jornada.GetProperty("cargaDiariaMinutos").GetInt32(), jornada.GetProperty("horarioEntrada").GetString(), jornada.GetProperty("horarioSaida").GetString(), jornada.GetProperty("toleranciaAtrasoMinutos").GetInt32()));
        var resumo = espelho.GetProperty("resumo");
        Assert.Equal((1600, 380, 1, 1, 0), (resumo.GetProperty("horasTrabalhadasNoMesMinutos").GetInt32(), resumo.GetProperty("saldoBancoHorasMinutos").GetInt32(), resumo.GetProperty("pedidosEmAnalise").GetInt32(), resumo.GetProperty("atrasos").GetInt32(), resumo.GetProperty("faltas").GetInt32()));
        Assert.Equal(["2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04", "2026-10-05", "2026-10-06", "2026-10-07"], espelho.GetProperty("dias").EnumerateArray().Select(d => d.GetProperty("data").GetString()!)); // até hoje, em ordem
        (string, string, bool, string, int?, int, int?, int?, bool)[] esperados =
        [
            ("2026-10-01", "QUINTA", false, "NORMAL", 534, 528, 6, null, true),
            ("2026-10-02", "SEXTA", false, "AJUSTE_EM_ANALISE", null, 528, null, null, false),
            ("2026-10-03", "SABADO", false, "FOLGA", null, 0, null, null, false),
            ("2026-10-04", "DOMINGO", false, "FOLGA", null, 0, null, null, false),
            ("2026-10-05", "SEGUNDA", false, "ATRASO", 526, 528, -2, 14, true),
            ("2026-10-06", "TERCA", false, "NORMAL", 540, 528, 12, null, true),
            ("2026-10-07", "QUARTA", true, "FALTA_COMPENSADA", null, 0, null, null, false),
        ];
        var vistos = esperados.Select(x => Dia(espelho, x.Item1)).Select(d => (d.GetProperty("data").GetString()!, d.GetProperty("diaDaSemana").GetString()!, d.GetProperty("ehHoje").GetBoolean(), d.GetProperty("situacao").GetString()!, Inteiro(d, "horasMinutos"), d.GetProperty("cargaMinutos").GetInt32(), Inteiro(d, "saldoMinutos"), Inteiro(d, "atrasoMinutos"), d.GetProperty("podePedirAjuste").GetBoolean())).ToArray();
        Assert.Equal(esperados, vistos);
        Assert.Equal(_pedidoDoDia2, Inteiro(Dia(espelho, "2026-10-02"), "pedidoEmAnaliseId"));
    }

    [Fact]
    public async Task Os_periodos_do_dia_levam_ordem_rotulo_origem_e_a_data_da_correcao_do_RH()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await EuAsync(api, api.Elenco.MembroA, "?ano=2026&mes=10"));

        var periodos = Dia(espelho, "2026-10-06").GetProperty("periodos").EnumerateArray().ToList();
        Assert.Equal(2, periodos.Count);
        var batidas = periodos.SelectMany(p => new[] { p.GetProperty("entrada"), p.GetProperty("saida") }).ToList();
        Assert.Equal(["Entrada", "Saída almoço", "Volta", "Saída"], batidas.Select(b => b.GetProperty("rotulo").GetString()!));
        Assert.Equal([1, 2, 3, 4], batidas.Select(b => b.GetProperty("ordem").GetInt32()));
        Assert.Equal(["SISTEMA", "SISTEMA", "SISTEMA", "CORRIGIDA_RH"], batidas.Select(b => b.GetProperty("origem").GetString()!));
        Assert.Equal(("2026-10-06T20:58:00Z", "2026-10-07T12:20:00Z", JsonValueKind.Null), (batidas[3].GetProperty("instanteUtc").GetString(), batidas[3].GetProperty("corrigidaEmUtc").GetString(), batidas[0].GetProperty("corrigidaEmUtc").ValueKind));
        Assert.Equal(JsonValueKind.Null, Dia(espelho, "2026-10-02").GetProperty("periodos")[1].GetProperty("saida").ValueKind); // a volta sem saída
        Assert.Equal(0, Dia(espelho, "2026-10-03").GetProperty("periodos").GetArrayLength());
    }

    // ---------------------------------------------------------------- o mês

    [Fact]
    public async Task Mes_futuro_devolve_dias_vazios_e_o_resumo_so_com_o_saldo_e_os_pedidos()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await EuAsync(api, api.Elenco.MembroA, "?ano=2026&mes=12"));

        Assert.Equal(0, espelho.GetProperty("dias").GetArrayLength());
        var resumo = espelho.GetProperty("resumo");
        Assert.Equal((0, 380, 1, 0, 0), (resumo.GetProperty("horasTrabalhadasNoMesMinutos").GetInt32(), resumo.GetProperty("saldoBancoHorasMinutos").GetInt32(), resumo.GetProperty("pedidosEmAnalise").GetInt32(), resumo.GetProperty("atrasos").GetInt32(), resumo.GetProperty("faltas").GetInt32()));
    }

    [Fact]
    public async Task Mes_passado_vai_ate_o_ultimo_dia_e_o_feriado_com_batida_mantem_o_nome_e_vira_hora_extra()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await RhAsync(api, api.Elenco.DeOutroSetor.FuncionarioId, "?ano=2026&mes=9"));

        Assert.Equal(30, espelho.GetProperty("dias").GetArrayLength());
        var feriado = Dia(espelho, "2026-09-07");
        Assert.Equal(("Independencia do Brasil", "NORMAL", 240, 0, 240, (int?)null), (feriado.GetProperty("nomeFeriado").GetString(), feriado.GetProperty("situacao").GetString(), Inteiro(feriado, "horasMinutos"), feriado.GetProperty("cargaMinutos").GetInt32(), Inteiro(feriado, "saldoMinutos"), Inteiro(feriado, "atrasoMinutos")));
        var semBatida = Dia(espelho, "2026-09-08");
        Assert.Equal(("SEM_REGISTRO", 0, 528, -528), (semBatida.GetProperty("situacao").GetString(), Inteiro(semBatida, "horasMinutos"), semBatida.GetProperty("cargaMinutos").GetInt32(), Inteiro(semBatida, "saldoMinutos")));
        // Setembro tem 22 dias úteis; um é o feriado (com batida) e cinco são férias: 16 faltas.
        Assert.Equal(16, espelho.GetProperty("resumo").GetProperty("faltas").GetInt32());
    }

    [Fact]
    public async Task Ferias_aprovadas_sao_FERIAS_sem_carga_e_nao_contam_como_falta()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await RhAsync(api, api.Elenco.DeOutroSetor.FuncionarioId, "?ano=2026&mes=9"));

        foreach (var data in new[] { "2026-09-14", "2026-09-16", "2026-09-18" })
        {
            var dia = Dia(espelho, data);
            Assert.Equal(("FERIAS", 0, JsonValueKind.Null), (dia.GetProperty("situacao").GetString(), dia.GetProperty("cargaMinutos").GetInt32(), dia.GetProperty("horasMinutos").ValueKind));
        }

        Assert.Equal("SEM_REGISTRO", Dia(espelho, "2026-09-11").GetProperty("situacao").GetString()); // a véspera
        Assert.Equal("SEM_REGISTRO", Dia(espelho, "2026-09-21").GetProperty("situacao").GetString()); // o dia seguinte ao fim
    }

    [Fact]
    public async Task Atestado_de_horas_validado_tira_as_horas_cobertas_da_carga_do_dia()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await RhAsync(api, api.Elenco.GestorSemSetor.FuncionarioId, "?ano=2026&mes=9"));

        var dia = Dia(espelho, "2026-09-02");
        Assert.Equal(("SEM_REGISTRO", 408, -408), (dia.GetProperty("situacao").GetString(), dia.GetProperty("cargaMinutos").GetInt32(), Inteiro(dia, "saldoMinutos")));
        Assert.Equal(528, Dia(espelho, "2026-09-03").GetProperty("cargaMinutos").GetInt32()); // o dia seguinte não tem atestado
    }

    [Fact]
    public async Task Atestado_de_dia_inteiro_validado_no_primeiro_dia_do_mes_cobre_o_dia()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await RhAsync(api, api.Elenco.GestorSemSetor.FuncionarioId, "?ano=2026&mes=9"));

        var dia = Dia(espelho, "2026-09-01");
        Assert.Equal(("FALTA_COMPENSADA", 0), (dia.GetProperty("situacao").GetString(), dia.GetProperty("cargaMinutos").GetInt32()));
    }

    [Fact]
    public async Task Hoje_sem_batida_e_SEM_REGISTRO_mas_ainda_nao_e_falta_no_resumo()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await EuAsync(api, api.Elenco.GestorSemSetor, "?ano=2026&mes=10"));

        // Paulo não bateu em 01, 02 e 06 (faltas), a entrada de 05 ficou sem saída (INCOMPLETO) e hoje (07) ainda pode bater.
        Assert.Equal("SEM_REGISTRO", Dia(espelho, "2026-10-07").GetProperty("situacao").GetString());
        Assert.Equal(3, espelho.GetProperty("resumo").GetProperty("faltas").GetInt32());
    }

    [Fact]
    public async Task Dia_passado_com_a_sequencia_quebrada_e_INCOMPLETO_sem_horas_e_com_botao()
    {
        var api = await ApiAsync();

        var espelho = await CorpoAsync(await EuAsync(api, api.Elenco.GestorSemSetor, "?ano=2026&mes=10"));

        var dia = Dia(espelho, "2026-10-05");
        Assert.Equal(("INCOMPLETO", JsonValueKind.Null, JsonValueKind.Null, true), (dia.GetProperty("situacao").GetString(), dia.GetProperty("horasMinutos").ValueKind, dia.GetProperty("saldoMinutos").ValueKind, dia.GetProperty("podePedirAjuste").GetBoolean()));
    }

    [Fact]
    public async Task Nenhum_dia_antes_da_admissao_nem_depois_do_desligamento_e_quem_ainda_nao_entrou_nao_tem_dias()
    {
        var api = await ApiAsync();
        var n = Interlocked.Increment(ref _sequencia);
        var desligado = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Desligado do espelho {n}", $"desligado.espelho.{n}@teste.com", new DateOnly(2026, 9, 10), ativo: false, desligamento: new DateOnly(2026, 9, 20));

        var setembro = await CorpoAsync(await RhAsync(api, desligado, "?ano=2026&mes=9"));
        var outubroDeQuemEntraDepois = await CorpoAsync(await RhAsync(api, api.Elenco.EmAdmissao.FuncionarioId, "?ano=2026&mes=10"));

        var datas = setembro.GetProperty("dias").EnumerateArray().Select(d => d.GetProperty("data").GetString()!).ToList();
        Assert.Equal(("2026-09-10", "2026-09-20", 11), (datas[0], datas[^1], datas.Count));
        Assert.False(setembro.GetProperty("funcionario").GetProperty("ativo").GetBoolean()); // o RH vê o espelho de quem está inativo
        Assert.Equal(0, outubroDeQuemEntraDepois.GetProperty("dias").GetArrayLength()); // Rafael entra em 13/10 e hoje é 07/10
    }

    [Fact]
    public async Task Sem_ano_e_mes_vale_o_mes_atual_e_so_um_dos_dois_completa_com_o_atual()
    {
        var api = await ApiAsync();

        var atual = await CorpoAsync(await EuAsync(api, api.Elenco.MembroA));
        var soOMes = await CorpoAsync(await EuAsync(api, api.Elenco.MembroA, "?mes=9"));
        var soOAno = await CorpoAsync(await EuAsync(api, api.Elenco.MembroA, "?ano=2025"));

        Assert.Equal(((2026, 10), (2026, 9), (2025, 10)), ((atual.GetProperty("ano").GetInt32(), atual.GetProperty("mes").GetInt32()), (soOMes.GetProperty("ano").GetInt32(), soOMes.GetProperty("mes").GetInt32()), (soOAno.GetProperty("ano").GetInt32(), soOAno.GetProperty("mes").GetInt32())));
    }

    // ---------------------------------------------------------------- quem vê o quê

    [Fact]
    public async Task O_gestor_ve_o_espelho_de_quem_e_da_equipe_com_o_funcionario_e_sem_o_botao_de_pedir_ajuste()
    {
        var api = await ApiAsync();

        var resposta = await GestorAsync(api, api.Elenco.Gestor, api.Elenco.MembroA.FuncionarioId, "?ano=2026&mes=10");

        Assert.Equal(200, (int)resposta.StatusCode);
        var espelho = await CorpoAsync(resposta);
        var pessoa = espelho.GetProperty("funcionario");
        Assert.Equal((api.Elenco.MembroA.FuncionarioId, "Diego Farias", "Comercial", true), (pessoa.GetProperty("id").GetInt32(), pessoa.GetProperty("nome").GetString(), pessoa.GetProperty("setor").GetString(), pessoa.GetProperty("ativo").GetBoolean()));
        Assert.Equal(1600, espelho.GetProperty("resumo").GetProperty("horasTrabalhadasNoMesMinutos").GetInt32());
        Assert.All(espelho.GetProperty("dias").EnumerateArray(), d => Assert.False(d.TryGetProperty("podePedirAjuste", out _))); // o gestor só vê
    }

    [Fact]
    public async Task Gestor_pedindo_pessoa_de_outra_equipe_o_proprio_outro_gestor_inativo_ou_inexistente_recebe_o_mesmo_404()
    {
        var api = await ApiAsync();
        var e = api.Elenco;

        var respostas = new List<int>();
        foreach (var id in new[] { e.DeOutroSetor.FuncionarioId, e.Gestor.FuncionarioId, e.GestorDoFinanceiro.FuncionarioId, e.Inativo.FuncionarioId, e.EmAdmissao.FuncionarioId, 999999 })
        {
            respostas.Add((int)(await GestorAsync(api, e.Gestor, id, "?ano=2026&mes=10")).StatusCode);
        }

        Assert.All(respostas, status => Assert.Equal(404, status));
        Assert.Equal(404, (int)(await GestorAsync(api, e.GestorSemSetor, e.MembroA.FuncionarioId)).StatusCode); // gestor sem setor não tem equipe
    }

    [Fact]
    public async Task RH_ve_qualquer_pessoa_e_pessoa_inexistente_e_404()
    {
        var api = await ApiAsync();

        Assert.Equal(200, (int)(await RhAsync(api, api.Elenco.Inativo.FuncionarioId)).StatusCode);
        Assert.Equal(404, (int)(await RhAsync(api, 999999)).StatusCode);
    }

    [Fact]
    public async Task Cada_rota_e_do_seu_perfil_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var funcionario = await api.ClienteAgoraAsync(e.MembroA.UsuarioId);
        var gestor = await api.ClienteAgoraAsync(e.Gestor.UsuarioId);
        var rh = await api.ClienteDoRhAgoraAsync();

        Assert.Equal(403, (int)(await funcionario.GetAsync($"/api/rh/funcionarios/{e.MembroA.FuncionarioId}/ponto")).StatusCode);
        Assert.Equal(403, (int)(await funcionario.GetAsync($"/api/gestor/equipe/{e.MembroA.FuncionarioId}/ponto")).StatusCode);
        Assert.Equal(403, (int)(await rh.GetAsync($"/api/gestor/equipe/{e.MembroA.FuncionarioId}/ponto")).StatusCode);
        Assert.Equal(403, (int)(await gestor.GetAsync($"/api/rh/funcionarios/{e.MembroA.FuncionarioId}/ponto")).StatusCode);
        Assert.Equal(200, (int)(await gestor.GetAsync("/api/eu/ponto/espelho")).StatusCode); // o gestor tem o próprio espelho como qualquer pessoa
        Assert.Equal(200, (int)(await rh.GetAsync("/api/eu/ponto/espelho")).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync("/api/eu/ponto/espelho")).StatusCode);
    }

    [Theory]
    [InlineData("?ano=1999", "ano")]
    [InlineData("?ano=2101", "ano")]
    [InlineData("?mes=0", "mes")]
    [InlineData("?mes=13", "mes")]
    public async Task Ano_ou_mes_fora_da_faixa_e_400_com_o_nome_do_campo_nas_tres_rotas(string consulta, string campo)
    {
        var api = await ApiAsync();
        var e = api.Elenco;

        var respostas = new[]
        {
            await EuAsync(api, e.MembroA, consulta),
            await GestorAsync(api, e.Gestor, e.MembroA.FuncionarioId, consulta),
            await RhAsync(api, e.MembroA.FuncionarioId, consulta),
        };

        Assert.All(respostas, r => Assert.Equal(400, (int)r.StatusCode));
        foreach (var resposta in respostas)
        {
            Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
        }
    }

    [Fact]
    public async Task Mes_invalido_vence_o_404_e_valor_que_nao_e_numero_e_400()
    {
        var api = await ApiAsync();

        Assert.Equal(400, (int)(await GestorAsync(api, api.Elenco.Gestor, api.Elenco.DeOutroSetor.FuncionarioId, "?mes=13")).StatusCode);
        Assert.Equal(400, (int)(await EuAsync(api, api.Elenco.MembroA, "?ano=abc")).StatusCode);
    }

    [Fact]
    public async Task As_respostas_do_espelho_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var e = api.Elenco;

        await (await EuAsync(api, e.MembroA, "?ano=2026&mes=10")).AfirmarConformeAoContratoAsync("obterMeuEspelhoDePonto");
        await (await EuAsync(api, e.MembroA, "?mes=13")).AfirmarConformeAoContratoAsync("obterMeuEspelhoDePonto");
        await (await GestorAsync(api, e.Gestor, e.MembroA.FuncionarioId, "?ano=2026&mes=10")).AfirmarConformeAoContratoAsync("obterEspelhoDePontoDaPessoaDaEquipe");
        await (await GestorAsync(api, e.Gestor, e.DeOutroSetor.FuncionarioId)).AfirmarConformeAoContratoAsync("obterEspelhoDePontoDaPessoaDaEquipe"); // 404
        await (await RhAsync(api, e.DeOutroSetor.FuncionarioId, "?ano=2026&mes=9")).AfirmarConformeAoContratoAsync("obterEspelhoDePontoDeFuncionario");
        await (await RhAsync(api, 999999)).AfirmarConformeAoContratoAsync("obterEspelhoDePontoDeFuncionario"); // 404
    }
}
