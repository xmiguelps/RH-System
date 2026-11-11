using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// A correção direta do RH (<c>PUT /api/rh/funcionarios/{id}/ponto/dias/{dia}</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. A lista do corpo SUBSTITUI o dia: sem <c>id</c> é batida nova, com <c>id</c> e mudança é correção, ausente é apagada.
/// Cada teste usa a pessoa e o dia dele (julho de 2026).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CorrigirDiaTests(MySqlFixture mysql)
{
    private static int _dia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(CorrigirDiaTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    /// <summary>Um dia útil de julho só deste teste (sábado e domingo têm outra situação).</summary>
    private static DateOnly NovoDia()
    {
        while (true)
        {
            var dia = new DateOnly(2026, 7, Interlocked.Increment(ref _dia) % 28 + 1);
            if (dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                return dia;
            }
        }
    }

    private static string Utc(DateOnly dia, int hora, int minuto = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00Z";

    private static async Task<HttpResponseMessage> CorrigirAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia, params object[] marcacoes) =>
        await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"/api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia:yyyy-MM-dd}", new { marcacoes });

    private static object Batida(string tipo, string utc, int? id = null) => new { id, tipo, instanteUtc = utc };

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static async Task<List<(int Id, string Tipo, string Instante, string Origem, bool ComCorrecao)>> BatidasAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var ponto = await db.PontosDosDias.AsNoTracking().FirstOrDefaultAsync(p => p.FuncionarioId == funcionarioId && p.Dia == dia);
        if (ponto is null)
        {
            return [];
        }

        var batidas = await db.Marcacoes.AsNoTracking().Where(m => m.PontoId == ponto.Id).OrderBy(m => m.InstanteUtc).ToListAsync();
        return [.. batidas.Select(m => (m.Id, m.Tipo == TipoDeMarcacao.Entrada ? "ENTRADA" : "SAIDA", m.InstanteUtc.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), m.Origem == OrigemDaMarcacao.Sistema ? "SISTEMA" : "CORRIGIDA_RH", m.CorrigidaEmUtc is not null))];
    }

    private static async Task<(int Entrada1, int Saida1, int Entrada2, int Saida2)> QuatroBatidasAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia)
    {
        await PontoDeTeste.BaterEmAsync(api.Conexao, funcionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)), (TipoDeMarcacao.Saida, Utc(dia, 15)), (TipoDeMarcacao.Entrada, Utc(dia, 16)), (TipoDeMarcacao.Saida, Utc(dia, 20)));
        var ids = (await BatidasAsync(api, funcionarioId, dia)).Select(b => b.Id).ToArray();
        return (ids[0], ids[1], ids[2], ids[3]);
    }

    // ---------------------------------------------------------------- os quatro casos

    [Fact]
    public async Task Os_quatro_casos_igual_corrigida_nova_e_ausente_e_o_dia_volta_calculado()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        var dia = NovoDia();
        var b = await QuatroBatidasAsync(api, pessoa.FuncionarioId, dia);

        var resposta = await CorrigirAsync(
            api, pessoa.FuncionarioId, dia,
            Batida("ENTRADA", Utc(dia, 11), b.Entrada1), // igual: não muda
            Batida("SAIDA", Utc(dia, 15, 5), b.Saida1), // corrigida
            Batida("ENTRADA", Utc(dia, 16, 10)), // nova
            Batida("SAIDA", Utc(dia, 20))); // nova no horário da saída que vai embora (a segunda entrada e a segunda saída antigas estão ausentes: apagadas)

        Assert.Equal(200, (int)resposta.StatusCode);
        var batidas = await BatidasAsync(api, pessoa.FuncionarioId, dia);
        Assert.Equal(["11:00", "15:05", "16:10", "20:00"], batidas.Select(x => x.Instante));
        Assert.Equal(["SISTEMA", "CORRIGIDA_RH", "CORRIGIDA_RH", "CORRIGIDA_RH"], batidas.Select(x => x.Origem));
        Assert.Equal([false, true, true, true], batidas.Select(x => x.ComCorrecao)); // CORRIGIDA_RH e DATA_CORRECAO andam juntas
        Assert.Equal([b.Entrada1, b.Saida1], batidas.Take(2).Select(x => x.Id)); // a igual e a corrigida mantêm o id (UPDATE no lugar)
        Assert.DoesNotContain(batidas, x => x.Id == b.Entrada2 || x.Id == b.Saida2);
        var dto = await CorpoAsync(resposta);
        Assert.Equal((dia.ToString("yyyy-MM-dd"), "NORMAL", 245 + 230), (dto.GetProperty("data").GetString(), dto.GetProperty("situacao").GetString(), dto.GetProperty("horasMinutos").GetInt32())); // 11:00-15:05 e 16:10-20:00
        Assert.Equal(["Entrada", "Saída almoço", "Volta", "Saída"], dto.GetProperty("periodos").EnumerateArray().SelectMany(p => new[] { p.GetProperty("entrada"), p.GetProperty("saida") }).Select(m => m.GetProperty("rotulo").GetString()!));
    }

    [Fact]
    public async Task Dia_sem_registro_recebe_a_lista_e_a_lista_vazia_limpa_o_dia()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB;
        var dia = NovoDia();

        var incluindo = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)), Batida("SAIDA", Utc(dia, 20)));
        var criadas = await BatidasAsync(api, pessoa.FuncionarioId, dia);
        var limpando = await CorrigirAsync(api, pessoa.FuncionarioId, dia);

        Assert.Equal((200, 2, 200, 0), ((int)incluindo.StatusCode, criadas.Count, (int)limpando.StatusCode, (await BatidasAsync(api, pessoa.FuncionarioId, dia)).Count));
        Assert.Equal("SEM_REGISTRO", (await CorpoAsync(limpando)).GetProperty("situacao").GetString()); // o dia útil passado sem batida é falta
    }

    [Fact]
    public async Task Corrigir_so_o_tipo_de_uma_batida_conserta_a_sequencia()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.GestorDoFinanceiro;
        var dia = NovoDia();
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)), (TipoDeMarcacao.Entrada, Utc(dia, 20)));
        var ids = (await BatidasAsync(api, pessoa.FuncionarioId, dia)).Select(x => x.Id).ToArray();

        var resposta = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11), ids[0]), Batida("SAIDA", Utc(dia, 20), ids[1]));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(["ENTRADA", "SAIDA"], (await BatidasAsync(api, pessoa.FuncionarioId, dia)).Select(x => x.Tipo));
    }

    // ---------------------------------------------------------------- recusas

    [Fact]
    public async Task Dia_com_pedido_em_analise_e_409_PONTO_DIA_COM_PEDIDO_EM_ANALISE_e_nada_muda()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.DeOutroSetor;
        var dia = NovoDia();
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)));
        await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, pessoa.FuncionarioId, dia);

        var resposta = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)), Batida("SAIDA", Utc(dia, 20)));

        Assert.Equal(409, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("PONTO_DIA_COM_PEDIDO_EM_ANALISE", "Responda o pedido de ajuste deste dia antes de editar as batidas."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Single(await BatidasAsync(api, pessoa.FuncionarioId, dia));
    }

    [Fact]
    public async Task Batida_que_ja_foi_alvo_de_um_pedido_nao_pode_ser_apagada_409_PONTO_MARCACAO_EM_USO()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        var dia = NovoDia();
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)), (TipoDeMarcacao.Saida, Utc(dia, 20)));
        var entrada = (await BatidasAsync(api, pessoa.FuncionarioId, dia))[0].Id;
        // O pedido corrigiu a entrada e foi RECUSADO: já não está em análise, mas o item continua apontando para a batida.
        var pedido = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, pessoa.FuncionarioId, dia, null, entrada);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE PONTOS_MANUTENCOES SET STATUS_SOLICITACAO = 'REJEITADA', ID_GESTOR = (SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {api.Elenco.Gestor.FuncionarioId}), RESULTADO_GESTOR = 'RECUSOU', DATA_ETAPA_GESTOR = NOW() WHERE ID_MANUTENCAO_PONTO = {pedido}");

        var resposta = await CorrigirAsync(api, pessoa.FuncionarioId, dia); // a lista vazia apagaria a entrada

        Assert.Equal(409, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("PONTO_MARCACAO_EM_USO", "Esta batida está ligada a um pedido de ajuste e não pode ser removida."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(2, (await BatidasAsync(api, pessoa.FuncionarioId, dia)).Count); // tudo ou nada: a saída também continua
    }

    [Fact]
    public async Task Duas_batidas_no_mesmo_horario_sao_409_PONTO_BATIDA_DUPLICADA_e_sequencia_quebrada_e_422()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB;
        var dia = NovoDia();

        var duplicada = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)), Batida("SAIDA", Utc(dia, 11)));
        var quebrada = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)), Batida("ENTRADA", Utc(dia, 12)), Batida("SAIDA", Utc(dia, 20)));
        var aberta = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)));

        var daDuplicada = await CorpoAsync(duplicada);
        Assert.Equal((409, "PONTO_BATIDA_DUPLICADA", "Há duas batidas no mesmo horário neste dia. Corrija os horários."), ((int)duplicada.StatusCode, Codigo(daDuplicada), daDuplicada.GetProperty("detail").GetString()));
        foreach (var resposta in new[] { quebrada, aberta })
        {
            var problema = await CorpoAsync(resposta);
            Assert.Equal((422, "PONTO_SEQUENCIA_INVALIDA", "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        }

        Assert.Empty(await BatidasAsync(api, pessoa.FuncionarioId, dia));
    }

    [Fact]
    public async Task Dia_futuro_e_horario_no_futuro_sao_422_PONTO_INSTANTE_NO_FUTURO_e_fora_do_dia_e_422_PONTO_INSTANTE_FORA_DO_DIA()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.GestorSemSetor;
        var diaPassado = new DateOnly(2026, 10, 2);
        var hoje = new DateOnly(2026, 10, 7);

        var diaFuturo = await CorrigirAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 9), Batida("ENTRADA", "2026-10-09T11:00:00Z"), Batida("SAIDA", "2026-10-09T20:00:00Z"));
        var futuroNoDiaDeHoje = await CorrigirAsync(api, pessoa.FuncionarioId, hoje, Batida("ENTRADA", "2026-10-07T11:00:00Z"), Batida("SAIDA", "2026-10-07T12:43:00Z")); // um minuto depois de agora
        var foraDoDia = await CorrigirAsync(api, pessoa.FuncionarioId, diaPassado, Batida("ENTRADA", "2026-10-02T11:00:00Z"), Batida("SAIDA", "2026-10-04T03:00:00Z")); // 00:00 do dia 04 em Brasília: dois dias depois, já passado
        var ateAgoraNoDiaDeHoje = await CorrigirAsync(api, pessoa.FuncionarioId, hoje, Batida("ENTRADA", "2026-10-07T11:00:00Z"), Batida("SAIDA", "2026-10-07T12:42:00Z")); // exatamente agora vale

        Assert.Equal(("PONTO_INSTANTE_NO_FUTURO", "PONTO_INSTANTE_NO_FUTURO", "PONTO_INSTANTE_FORA_DO_DIA", 200), (Codigo(await CorpoAsync(diaFuturo)), Codigo(await CorpoAsync(futuroNoDiaDeHoje)), Codigo(await CorpoAsync(foraDoDia)), (int)ateAgoraNoDiaDeHoje.StatusCode));
        Assert.Equal([422, 422, 422], new[] { diaFuturo, futuroNoDiaDeHoje, foraDoDia }.Select(r => (int)r.StatusCode));
    }

    [Fact]
    public async Task Jornada_que_atravessa_a_meia_noite_cabe_no_dia_seguinte()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        var dia = NovoDia();

        var resposta = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 22)), Batida("SAIDA", $"{dia.AddDays(1):yyyy-MM-dd}T03:10:00Z")); // 19:00 e 00:10 em Brasília

        Assert.Equal(200, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task Pessoa_inativa_e_422_FUNCIONARIO_INATIVO_com_a_regra_do_banco_e_antes_da_admissao_e_422_sem_codigo()
    {
        var api = await ApiAsync();
        var inativo = await CorrigirAsync(api, api.Elenco.Inativo.FuncionarioId, new DateOnly(2026, 9, 1), Batida("ENTRADA", "2026-09-01T11:00:00Z"), Batida("SAIDA", "2026-09-01T20:00:00Z"));
        var antesDaAdmissao = await CorrigirAsync(api, api.Elenco.MembroA.FuncionarioId, new DateOnly(2025, 12, 31), Batida("ENTRADA", "2025-12-31T11:00:00Z"), Batida("SAIDA", "2025-12-31T20:00:00Z"));

        var problema = await CorpoAsync(inativo);
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Este colaborador está inativo e o ponto dele não pode mais ser alterado.", "Nao e possivel registrar ponto de funcionario inativo."), ((int)inativo.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString(), problema.GetProperty("regraDoBanco").GetString()));
        var semCodigo = await CorpoAsync(antesDaAdmissao);
        Assert.Equal((422, "(sem código)", "A data do ponto e anterior a admissao."), ((int)antesDaAdmissao.StatusCode, Codigo(semCodigo), semCodigo.GetProperty("regraDoBanco").GetString()));
    }

    [Fact]
    public async Task Corpo_invalido_e_400_id_de_outro_dia_ou_repetido_e_400_pessoa_inexistente_e_404_e_so_o_RH_corrige()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB;
        var dia = NovoDia();
        var outroDia = NovoDia();
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, outroDia, (TipoDeMarcacao.Entrada, Utc(outroDia, 11)), (TipoDeMarcacao.Saida, Utc(outroDia, 20)));
        var idDeOutroDia = (await BatidasAsync(api, pessoa.FuncionarioId, outroDia))[0].Id;
        var cliente = await api.ClienteDoRhAgoraAsync();
        var rota = $"/api/rh/funcionarios/{pessoa.FuncionarioId}/ponto/dias/{dia:yyyy-MM-dd}";

        var demais = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Enumerable.Range(0, 21).Select(i => Batida(i % 2 == 0 ? "ENTRADA" : "SAIDA", Utc(dia, 8 + (i / 2), i % 2 * 30))).ToArray());
        var semFuso = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", $"{dia:yyyy-MM-dd}T11:00:00"));
        var semMarcacoes = await cliente.PutAsJsonAsync(rota, new { });
        var semCorpo = await cliente.PutAsync(rota, content: null);
        var deOutroDia = await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11), idDeOutroDia));
        var inexistente = await CorrigirAsync(api, 999999, dia, Batida("ENTRADA", Utc(dia, 11)));
        var gestor = await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).PutAsJsonAsync(rota, new { marcacoes = Array.Empty<object>() });
        var anonimo = await api.Api.ClienteAnonimo().PutAsJsonAsync(rota, new { marcacoes = Array.Empty<object>() });

        Assert.Equal([400, 400, 400, 400, 400, 404, 403, 401], new[] { demais, semFuso, semMarcacoes, semCorpo, deOutroDia, inexistente, gestor, anonimo }.Select(r => (int)r.StatusCode));
        Assert.True((await CorpoAsync(demais)).GetProperty("errors").TryGetProperty("marcacoes", out _));
        Assert.True((await CorpoAsync(deOutroDia)).GetProperty("errors").TryGetProperty("marcacoes", out _));
        Assert.Empty(await BatidasAsync(api, pessoa.FuncionarioId, dia));
        Assert.Equal(2, (await BatidasAsync(api, pessoa.FuncionarioId, outroDia)).Count); // a batida do outro dia ficou como estava
    }

    // ---------------------------------------------------------------- o que a correção deixa

    [Fact]
    public async Task A_auditoria_guarda_a_batida_antiga_campo_a_campo_e_nenhum_e_mail_sai()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        var dia = NovoDia();
        var b = await QuatroBatidasAsync(api, pessoa.FuncionarioId, dia);
        var emails = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");

        await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11), b.Entrada1), Batida("SAIDA", Utc(dia, 15, 5), b.Saida1));

        var evento = (long)await Sql.ContarAsync(api.Conexao, $"SELECT ID_AUDITORIA_EVENTO FROM AUDITORIA_EVENTOS WHERE AREA = 'PONTO' AND ACAO = 'ALTEROU' AND ENTIDADE = 'FUNCIONARIOS' AND ID_REGISTRO = {pessoa.FuncionarioId} AND DESCRICAO LIKE '%{dia:dd/MM/yyyy}%' AND ID_FUNCIONARIO_ATOR = {api.Elenco.Rh.FuncionarioId}");
        var mudancas = new List<(string Campo, string? Antes, string? Depois)>();
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            await using var conexao = db.Database.GetDbConnection();
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = $"SELECT CAMPO, VALOR_ANTES, VALOR_DEPOIS FROM AUDITORIA_MUDANCAS WHERE ID_AUDITORIA_EVENTO = {evento} ORDER BY ORDEM";
            await using var leitor = await comando.ExecuteReaderAsync();
            while (await leitor.ReadAsync())
            {
                mudancas.Add((leitor.GetString(0), leitor.IsDBNull(1) ? null : leitor.GetString(1), leitor.IsDBNull(2) ? null : leitor.GetString(2)));
            }
        }

        // A batida antiga (a saída das 12:00 em Brasília) está no log; a segunda entrada e a segunda saída saíram do dia.
        Assert.Contains(("Batida corrigida (saída)", "saída 12:00:00", "saída 12:05:00"), mudancas);
        Assert.Contains(("Batida removida (entrada)", "13:00:00", null), mudancas);
        Assert.Contains(("Batida removida (saída)", "17:00:00", null), mudancas);
        Assert.Equal(emails, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS")); // sem e-mail e sem segunda etapa
    }

    [Fact]
    public async Task O_banco_de_horas_do_dia_e_recalculado_pela_porta_so_quando_a_correcao_vale()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB;
        var dia = NovoDia();
        var espiao = new RecalculoEspiao();
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IRecalculoDoBancoDeHoras>();
            s.AddSingleton<IRecalculoDoBancoDeHoras>(espiao);
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        var rota = $"/api/rh/funcionarios/{pessoa.FuncionarioId}/ponto/dias/{dia:yyyy-MM-dd}";

        await cliente.PutAsJsonAsync(rota, new { marcacoes = new[] { Batida("ENTRADA", Utc(dia, 11)), Batida("SAIDA", Utc(dia, 20)) } });
        await cliente.PutAsJsonAsync(rota, new { marcacoes = new[] { Batida("ENTRADA", Utc(dia, 11)) } }); // recusada: sequência aberta

        Assert.Equal([(pessoa.FuncionarioId, dia)], espiao.Chamadas);
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
    public async Task As_respostas_da_correcao_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.GestorDoFinanceiro;
        var dia = NovoDia();

        await (await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)), Batida("SAIDA", Utc(dia, 20)))).AfirmarConformeAoContratoAsync("corrigirMarcacoesDoDia"); // 200
        await (await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)))).AfirmarConformeAoContratoAsync("corrigirMarcacoesDoDia"); // 422
        await (await CorrigirAsync(api, pessoa.FuncionarioId, dia, Batida("ENTRADA", Utc(dia, 11)), Batida("SAIDA", Utc(dia, 11)))).AfirmarConformeAoContratoAsync("corrigirMarcacoesDoDia"); // 409
        await (await CorrigirAsync(api, 999999, dia)).AfirmarConformeAoContratoAsync("corrigirMarcacoesDoDia"); // 404
        await (await (await api.ClienteDoRhAgoraAsync()).PutAsync($"/api/rh/funcionarios/{pessoa.FuncionarioId}/ponto/dias/{dia:yyyy-MM-dd}", content: null)).AfirmarConformeAoContratoAsync("corrigirMarcacoesDoDia"); // 400
    }
}
