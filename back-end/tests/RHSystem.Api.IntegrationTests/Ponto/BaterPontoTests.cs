using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// <c>GET /api/eu/ponto/hoje</c> e <c>POST /api/eu/ponto/marcacoes</c> pela API de verdade, no banco REAL. A empresa é 08:00 a 17:00 com 10 minutos de tolerância. Cada teste fixa o relógio num dia só dele (março de 2026, o dia é o número do
/// teste) e as batidas de um teste nunca aparecem no outro; a pessoa é a do elenco que o teste escolhe.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class BaterPontoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/ponto/marcacoes";
    private const string Cartao = "/api/eu/ponto/hoje";
    private static int _cargo;

    /// <summary>12:42:40Z (09:42:40 em Brasília) do dia <paramref name="n"/> de março de 2026.</summary>
    private static DateTime Dia(int n, int hora = 12, int minuto = 42, int segundo = 40) => new(2026, 3, n, hora, minuto, segundo, DateTimeKind.Utc);

    private async Task<ApiDeAcesso> ApiAsync(DateTime agora)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(BaterPontoTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(agora);
        return api;
    }

    private static async Task<HttpResponseMessage> BaterAsync(ApiDeAcesso api, PessoaDeTeste pessoa, string tipoEsperado) =>
        await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(Rota, new { tipoEsperado });

    private static async Task<JsonElement> CartaoAsync(ApiDeAcesso api, PessoaDeTeste pessoa) =>
        await CorpoAsync(await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(Cartao));

    private static async Task<long> ContarBatidasAsync(string conexao, int funcionarioId) =>
        await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {funcionarioId}");

    /// <summary>As batidas do ponto do dia, com o tipo e o instante em UTC (o banco guarda -03:00: soma 3 horas), por horário.</summary>
    private static async Task<List<(string Tipo, string InstanteUtc)>> InstantesDoPontoAsync(string conexao, int funcionarioId, string dia)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var data = DateOnly.Parse(dia, CultureInfo.InvariantCulture);
        var batidas = await db.Marcacoes.AsNoTracking()
            .Where(m => db.PontosDosDias.Any(p => p.Id == m.PontoId && p.FuncionarioId == funcionarioId && p.Dia == data))
            .OrderBy(m => m.InstanteUtc)
            .ToListAsync();
        return [.. batidas.Select(m => (m.Tipo == TipoDeMarcacao.Entrada ? "ENTRADA" : "SAIDA", m.InstanteUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))];
    }

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    // ---------------------------------------------------------------- a primeira batida e o cartão

    [Fact]
    public async Task Primeira_batida_cria_o_dia_com_a_carga_e_devolve_o_cartao()
    {
        var api = await ApiAsync(Dia(1));
        var pessoa = api.Elenco.MembroA;
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo ponto {Interlocked.Increment(ref _cargo)}", 44m);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");

        var resposta = await BaterAsync(api, pessoa, "ENTRADA");

        Assert.Equal(201, (int)resposta.StatusCode);
        var cartao = await CorpoAsync(resposta);
        Assert.Equal(("2026-03-01", "2026-03-01T12:42:40Z"), (cartao.GetProperty("dataDoPonto").GetString(), cartao.GetProperty("agoraUtc").GetString()));
        var jornada = cartao.GetProperty("jornada");
        Assert.Equal((2640, 528, "08:00", "17:00", 10), (jornada.GetProperty("cargaSemanalMinutos").GetInt32(), jornada.GetProperty("cargaDiariaMinutos").GetInt32(), jornada.GetProperty("horarioEntrada").GetString(), jornada.GetProperty("horarioSaida").GetString(), jornada.GetProperty("toleranciaAtrasoMinutos").GetInt32()));
        var periodo = cartao.GetProperty("periodos").EnumerateArray().Single();
        var entrada = periodo.GetProperty("entrada");
        Assert.Equal((1, "ENTRADA", "Entrada", "2026-03-01T12:42:40Z", "SISTEMA", JsonValueKind.Null), (entrada.GetProperty("ordem").GetInt32(), entrada.GetProperty("tipo").GetString(), entrada.GetProperty("rotulo").GetString(), entrada.GetProperty("instanteUtc").GetString(), entrada.GetProperty("origem").GetString(), entrada.GetProperty("corrigidaEmUtc").ValueKind));
        Assert.Equal(JsonValueKind.Null, periodo.GetProperty("saida").ValueKind);
        var proxima = cartao.GetProperty("proximaMarcacao");
        Assert.Equal(("SAIDA", "Saída almoço", "Registrar saída para almoço"), (proxima.GetProperty("tipo").GetString(), proxima.GetProperty("rotulo").GetString(), proxima.GetProperty("textoDoBotao").GetString()));
        Assert.Equal((0, JsonValueKind.Null), (cartao.GetProperty("bancoHoras").GetProperty("saldoMinutos").GetInt32(), cartao.GetProperty("bancoHoras").GetProperty("atualizadoAte").ValueKind));
        // O dia foi criado pelo banco com a carga do cargo (44 h ÷ 5 = 8,80) e a batida guardada em -03:00.
        Assert.Equal(8.80m, Convert.ToDecimal(await Sql.EscalarAsync(api.Conexao, $"SELECT CARGA_HORARIA FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DATA = '2026-03-01'"), CultureInfo.InvariantCulture));
        Assert.Equal("2026-03-01 09:42:40", Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT m.DATA_HORA_MARCACAO FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {pessoa.FuncionarioId}"), CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task As_quatro_batidas_do_dia_tem_rotulo_e_proxima_de_cada_posicao()
    {
        var api = await ApiAsync(Dia(2, 11, 0, 0));
        var pessoa = api.Elenco.MembroB;
        var esperados = new[] { ("ENTRADA", "Entrada", "SAIDA", "Saída almoço"), ("SAIDA", "Saída almoço", "ENTRADA", "Volta"), ("ENTRADA", "Volta", "SAIDA", "Saída"), ("SAIDA", "Saída", "ENTRADA", "Entrada") };

        var vistos = new List<(string, string, string, string)>();
        for (var i = 0; i < 4; i++)
        {
            api.Api.Relogio.Fixar(Dia(2, 11 + i, 0, 0));
            var resposta = await BaterAsync(api, pessoa, esperados[i].Item1);
            Assert.Equal(201, (int)resposta.StatusCode);
            var cartao = await CorpoAsync(resposta);
            var batida = cartao.GetProperty("periodos").EnumerateArray().SelectMany(p => new[] { p.GetProperty("entrada"), p.GetProperty("saida") }).Where(m => m.ValueKind == JsonValueKind.Object).Last();
            vistos.Add((batida.GetProperty("tipo").GetString()!, batida.GetProperty("rotulo").GetString()!, cartao.GetProperty("proximaMarcacao").GetProperty("tipo").GetString()!, cartao.GetProperty("proximaMarcacao").GetProperty("rotulo").GetString()!));
        }

        Assert.Equal(esperados, vistos);
        Assert.Equal(4, await ContarBatidasAsync(api.Conexao, pessoa.FuncionarioId));
    }

    [Fact]
    public async Task Hora_vem_do_servidor_e_o_corpo_so_diz_o_tipo()
    {
        var api = await ApiAsync(Dia(3));
        var pessoa = api.Elenco.GestorDoFinanceiro;
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);

        var resposta = await cliente.PostAsJsonAsync(Rota, new { tipoEsperado = "ENTRADA", instanteUtc = "2020-01-01T00:00:00Z", dataDoPonto = "2020-01-01" });

        Assert.Equal(201, (int)resposta.StatusCode);
        var entrada = (await CorpoAsync(resposta)).GetProperty("periodos")[0].GetProperty("entrada");
        Assert.Equal("2026-03-03T12:42:40Z", entrada.GetProperty("instanteUtc").GetString()); // o relógio do servidor, nunca o do corpo
    }

    [Fact]
    public async Task tipoEsperado_errado_da_409_PONTO_SEQUENCIA_MUDOU_e_nada_e_gravado()
    {
        var api = await ApiAsync(Dia(4));
        var pessoa = api.Elenco.DeOutroSetor;

        var resposta = await BaterAsync(api, pessoa, "SAIDA"); // o dia está vazio: a próxima é ENTRADA

        Assert.Equal(409, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("PONTO_SEQUENCIA_MUDOU", "A sequência do ponto mudou.", "O seu ponto mudou desde a última vez que você abriu a tela. Confira e tente de novo."), (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString()));
        Assert.Equal(0, await ContarBatidasAsync(api.Conexao, pessoa.FuncionarioId));
    }

    // ---------------------------------------------------------------- duplo clique e duas abas

    [Fact]
    public async Task Duas_batidas_iguais_em_paralelo_gravam_uma_so_e_a_outra_e_409_com_codigo_nunca_500()
    {
        var api = await ApiAsync(Dia(5));
        var pessoa = api.Elenco.GestorSemSetor;
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);
        var portao = new TaskCompletionSource();

        var tentativas = Enumerable.Range(0, 3).Select(async _ =>
        {
            await portao.Task; // todas largam juntas
            return await cliente.PostAsJsonAsync(Rota, new { tipoEsperado = "ENTRADA" });
        }).ToList();
        portao.SetResult();
        var respostas = await Task.WhenAll(tentativas);

        Assert.Equal([201, 409, 409], respostas.Select(r => (int)r.StatusCode).Order());
        foreach (var recusada in respostas.Where(r => (int)r.StatusCode == 409))
        {
            Assert.Equal("PONTO_BATIDA_DUPLICADA", Codigo(await CorpoAsync(recusada))); // o mesmo segundo: o duplo clique
        }

        Assert.Equal(1, await ContarBatidasAsync(api.Conexao, pessoa.FuncionarioId));
    }

    [Fact]
    public async Task Duplo_clique_a_0_4_s_e_a_0_6_s_cai_no_mesmo_segundo_e_a_segunda_e_PONTO_BATIDA_DUPLICADA()
    {
        var api = await ApiAsync(Dia(6).AddMilliseconds(400));
        var pessoa = api.Elenco.Gestor;

        var primeira = await BaterAsync(api, pessoa, "ENTRADA");
        api.Api.Relogio.Fixar(Dia(6).AddMilliseconds(600));
        var segunda = await BaterAsync(api, pessoa, "ENTRADA");

        Assert.Equal(201, (int)primeira.StatusCode);
        Assert.Equal(409, (int)segunda.StatusCode);
        Assert.Equal("PONTO_BATIDA_DUPLICADA", Codigo(await CorpoAsync(segunda)));
        Assert.Equal(1, await ContarBatidasAsync(api.Conexao, pessoa.FuncionarioId));
    }

    [Fact]
    public async Task Duas_abas_com_o_mesmo_tipoEsperado_em_segundos_diferentes_gravam_uma_so_e_a_segunda_e_PONTO_SEQUENCIA_MUDOU()
    {
        var api = await ApiAsync(Dia(7));
        var pessoa = api.Elenco.Rh;

        var abaA = await BaterAsync(api, pessoa, "ENTRADA");
        api.Api.Relogio.Fixar(Dia(7, 12, 42, 55));
        var abaB = await BaterAsync(api, pessoa, "ENTRADA"); // a aba B ainda mostra "Registrar entrada"

        Assert.Equal(201, (int)abaA.StatusCode);
        Assert.Equal(409, (int)abaB.StatusCode);
        Assert.Equal("PONTO_SEQUENCIA_MUDOU", Codigo(await CorpoAsync(abaB)));
        Assert.Equal(1, await ContarBatidasAsync(api.Conexao, pessoa.FuncionarioId));
        Assert.Equal("SAIDA", (await CartaoAsync(api, pessoa)).GetProperty("proximaMarcacao").GetProperty("tipo").GetString()); // o cartão recarregado já mostra a saída
    }

    // ---------------------------------------------------------------- fuso e meia-noite

    [Fact]
    public async Task Batida_as_22h30_locais_cai_no_dia_certo_e_nao_no_de_UTC()
    {
        var api = await ApiAsync(Dia(9, 12, 0, 0).AddHours(13).AddMinutes(30)); // 2026-03-10T01:30:00Z = 22:30 do dia 9 em Brasília
        var pessoa = api.Elenco.MembroA;

        var resposta = await BaterAsync(api, pessoa, "ENTRADA");

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal("2026-03-09", (await CorpoAsync(resposta)).GetProperty("dataDoPonto").GetString());
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DATA = '2026-03-09'"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DATA = '2026-03-10'"));
    }

    [Fact]
    public async Task As_00h10_fecha_a_entrada_das_22h_do_dia_anterior_no_dia_da_entrada()
    {
        var api = await ApiAsync(new DateTime(2026, 3, 12, 1, 0, 0, DateTimeKind.Utc)); // 22:00 do dia 11 em Brasília
        var pessoa = api.Elenco.MembroB;
        var entrada = await BaterAsync(api, pessoa, "ENTRADA");
        api.Api.Relogio.Fixar(new DateTime(2026, 3, 12, 3, 10, 0, DateTimeKind.Utc)); // 00:10 do dia 12

        var cartaoDaMadrugada = await CartaoAsync(api, pessoa);
        var saida = await BaterAsync(api, pessoa, "SAIDA");

        Assert.Equal(201, (int)entrada.StatusCode);
        Assert.Equal(("2026-03-11", "SAIDA"), (cartaoDaMadrugada.GetProperty("dataDoPonto").GetString(), cartaoDaMadrugada.GetProperty("proximaMarcacao").GetProperty("tipo").GetString())); // o dia do ponto é o da ENTRADA
        Assert.Equal(201, (int)saida.StatusCode);
        var cartaoDepois = await CorpoAsync(saida); // a jornada fechada é do dia 11: o cartão devolvido é o do dia 12, vazio, pronto para a próxima entrada
        Assert.Equal(("2026-03-12", 0, "ENTRADA"), (cartaoDepois.GetProperty("dataDoPonto").GetString(), cartaoDepois.GetProperty("periodos").GetArrayLength(), cartaoDepois.GetProperty("proximaMarcacao").GetProperty("tipo").GetString()));
        Assert.Equal(
            [("ENTRADA", "2026-03-12 01:00:00"), ("SAIDA", "2026-03-12 03:10:00")],
            await InstantesDoPontoAsync(api.Conexao, pessoa.FuncionarioId, "2026-03-11"));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DATA = '2026-03-11'"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DATA = '2026-03-12'")); // nada foi aberto no dia 12
    }

    [Fact]
    public async Task Com_12_horas_ou_mais_de_entrada_aberta_a_proxima_e_a_entrada_do_dia_novo()
    {
        var api = await ApiAsync(new DateTime(2026, 3, 15, 1, 0, 0, DateTimeKind.Utc)); // 22:00 do dia 14
        var pessoa = api.Elenco.GestorDoFinanceiro;
        await BaterAsync(api, pessoa, "ENTRADA");

        api.Api.Relogio.Fixar(new DateTime(2026, 3, 15, 12, 59, 0, DateTimeKind.Utc)); // 11 h 59 min depois
        var antes = await CartaoAsync(api, pessoa);
        api.Api.Relogio.Fixar(new DateTime(2026, 3, 15, 13, 0, 0, DateTimeKind.Utc)); // 12 h exatas depois
        var depois = await CartaoAsync(api, pessoa);

        Assert.Equal(("2026-03-14", "SAIDA"), (antes.GetProperty("dataDoPonto").GetString(), antes.GetProperty("proximaMarcacao").GetProperty("tipo").GetString()));
        Assert.Equal(("2026-03-15", "ENTRADA", 0), (depois.GetProperty("dataDoPonto").GetString(), depois.GetProperty("proximaMarcacao").GetProperty("tipo").GetString(), depois.GetProperty("periodos").GetArrayLength()));
    }

    // ---------------------------------------------------------------- quem não pode bater

    [Fact]
    public async Task Funcionario_inativo_com_token_valido_da_422_FUNCIONARIO_INATIVO_com_regraDoBanco()
    {
        var api = await ApiAsync(Dia(16));
        var pessoa = api.Elenco.Inativo;

        var resposta = await BaterAsync(api, pessoa, "ENTRADA");

        Assert.Equal(422, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("FUNCIONARIO_INATIVO", "Colaborador inativo.", "Este colaborador está inativo e não pode registrar ponto.", "Nao e possivel registrar ponto de funcionario inativo."),
            (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString(), problema.GetProperty("regraDoBanco").GetString()));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DATA = '2026-03-16'"));
    }

    [Fact]
    public async Task Antes_da_admissao_da_422_sem_codigo_com_a_regra_do_banco()
    {
        var api = await ApiAsync(ElencoDeTeste.Hoje); // 2026-10-07; Rafael Torres entra em 2026-10-13
        var pessoa = api.Elenco.EmAdmissao;

        var resposta = await BaterAsync(api, pessoa, "ENTRADA");

        Assert.Equal(422, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("(sem código)", "Operação recusada.", "A operação foi recusada por uma regra do banco de dados.", "A data do ponto e anterior a admissao."),
            (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString(), problema.GetProperty("regraDoBanco").GetString()));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DATA = '2026-10-07'"));
    }

    [Fact]
    public async Task No_dia_da_admissao_a_batida_e_aceita()
    {
        var api = await ApiAsync(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc)); // Rafael Torres entra em 2026-10-13

        var resposta = await BaterAsync(api, api.Elenco.EmAdmissao, "ENTRADA");

        Assert.Equal(201, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task Feriado_e_fim_de_semana_aceitam_a_batida()
    {
        var api = await ApiAsync(new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc)); // segunda, Independência
        var pessoa = api.Elenco.DeOutroSetor;
        var noFeriado = await BaterAsync(api, pessoa, "ENTRADA");
        api.Api.Relogio.Fixar(new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)); // sábado
        var noSabado = await BaterAsync(api, pessoa, "ENTRADA");

        Assert.Equal((201, 201), ((int)noFeriado.StatusCode, (int)noSabado.StatusCode)); // a hora vira extra no banco de horas (T17)
    }

    [Fact]
    public async Task Sem_empresa_e_sem_cargo_a_jornada_vem_toda_null_e_nao_da_422()
    {
        var api = await ApiAsync(Dia(18));
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        var pessoa = api.Elenco.GestorSemSetor; // sem cargo
        var resposta = await BaterAsync(api, pessoa, "ENTRADA");

        Assert.Equal(201, (int)resposta.StatusCode);
        var jornada = (await CorpoAsync(resposta)).GetProperty("jornada");
        Assert.All(new[] { "cargaSemanalMinutos", "cargaDiariaMinutos", "horarioEntrada", "horarioSaida", "toleranciaAtrasoMinutos" }, campo => Assert.Equal(JsonValueKind.Null, jornada.GetProperty(campo).ValueKind));
    }

    // ---------------------------------------------------------------- o cartão

    [Fact]
    public async Task O_cartao_vazio_manda_registrar_a_entrada_e_qualquer_perfil_pode_ver_o_proprio()
    {
        var api = await ApiAsync(Dia(19));

        foreach (var pessoa in new[] { api.Elenco.MembroA, api.Elenco.Gestor, api.Elenco.Rh })
        {
            var cartao = await CartaoAsync(api, pessoa);
            Assert.Equal(("2026-03-19", 0, "ENTRADA", "Registrar entrada"), (cartao.GetProperty("dataDoPonto").GetString(), cartao.GetProperty("periodos").GetArrayLength(), cartao.GetProperty("proximaMarcacao").GetProperty("tipo").GetString(), cartao.GetProperty("proximaMarcacao").GetProperty("textoDoBotao").GetString()));
        }
    }

    [Fact]
    public async Task O_saldo_do_banco_soma_os_minutos_de_cada_linha_e_nao_as_horas()
    {
        var api = await ApiAsync(Dia(20));
        var pessoa = api.Elenco.MembroB;
        // Três créditos de 0,33 h (19,8 min cada, 20 min por linha = 60) e um débito de 0,10 h (6 min): 54. Somando as horas daria 0,89 h = 53,4 min.
        foreach (var (dia, tipo, horas) in new[] { ("2026-02-02", "CREDITO", "0.33"), ("2026-02-03", "CREDITO", "0.33"), ("2026-02-04", "CREDITO", "0.33"), ("2026-02-05", "DEBITO", "0.10") })
        {
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO BANCOS_HORAS (ID_FUNCIONARIO, DATA_LANCAMENTO, TIPO_LANCAMENTO, QUANTIDADE, DIA_REFERENCIA) VALUES ({pessoa.FuncionarioId}, '2026-02-10', '{tipo}', {horas}, '{dia}')");
        }

        var cartao = await CartaoAsync(api, pessoa);

        Assert.Equal(54, cartao.GetProperty("bancoHoras").GetProperty("saldoMinutos").GetInt32());
    }

    // ---------------------------------------------------------------- auditoria e erros de entrada

    [Fact]
    public async Task Auditoria_registra_PONTO_REGISTROU_com_IP_e_sem_a_hora_nem_o_corpo()
    {
        var api = await ApiAsync(Dia(21));
        var pessoa = api.Elenco.Gestor;

        await BaterAsync(api, pessoa, "ENTRADA");

        var filtro = $"AREA = 'PONTO' AND ACAO = 'REGISTROU' AND ID_FUNCIONARIO_ATOR = {pessoa.FuncionarioId} AND ENTIDADE = 'PONTOS_MARCACOES' AND CANAL = 'PORTAL' AND IP IS NOT NULL AND DESCRICAO LIKE '%21/03/2026%'";
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE {filtro}"));
        var descricao = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT DESCRICAO FROM AUDITORIA_EVENTOS WHERE {filtro}"))!;
        Assert.Equal("Registrou uma entrada no ponto do dia 21/03/2026.", descricao);
        Assert.DoesNotContain("tipoEsperado", descricao, StringComparison.Ordinal);
        Assert.DoesNotContain("09:42", descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Corpo_sem_tipoEsperado_com_valor_invalido_ou_ausente_e_400_e_sem_login_e_401()
    {
        var api = await ApiAsync(Dia(22));
        var cliente = await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId);

        var semCampo = await cliente.PostAsJsonAsync(Rota, new { });
        var valorInvalido = await cliente.PostAsJsonAsync(Rota, new { tipoEsperado = "ALMOCO" });
        var semCorpo = await cliente.PostAsync(Rota, content: null);
        var anonimo = await api.Api.ClienteAnonimo().PostAsJsonAsync(Rota, new { tipoEsperado = "ENTRADA" });

        Assert.Equal([400, 400, 400, 401], new[] { semCampo, valorInvalido, semCorpo, anonimo }.Select(r => (int)r.StatusCode));
        Assert.True((await CorpoAsync(semCampo)).TryGetProperty("errors", out _)); // sem o campo obrigatório o corpo não é lido
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {api.Elenco.MembroA.FuncionarioId} AND DATA = '2026-03-22'")); // nenhum pedido recusado abriu o dia
    }

    [Fact]
    public async Task As_respostas_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync(Dia(23));
        var pessoa = api.Elenco.DeOutroSetor;

        await (await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(Cartao)).AfirmarConformeAoContratoAsync("obterPontoHoje");
        await (await BaterAsync(api, pessoa, "ENTRADA")).AfirmarConformeAoContratoAsync("registrarMarcacaoDePonto"); // 201
        await (await BaterAsync(api, pessoa, "ENTRADA")).AfirmarConformeAoContratoAsync("registrarMarcacaoDePonto"); // 409 do mesmo segundo
        api.Api.Relogio.Fixar(Dia(23, 12, 50, 0));
        await (await BaterAsync(api, pessoa, "ENTRADA")).AfirmarConformeAoContratoAsync("registrarMarcacaoDePonto"); // 409 da sequência
        await (await BaterAsync(api, api.Elenco.Inativo, "ENTRADA")).AfirmarConformeAoContratoAsync("registrarMarcacaoDePonto"); // 422 com regraDoBanco
        await (await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(Rota, new { })).AfirmarConformeAoContratoAsync("registrarMarcacaoDePonto"); // 400
    }
}
