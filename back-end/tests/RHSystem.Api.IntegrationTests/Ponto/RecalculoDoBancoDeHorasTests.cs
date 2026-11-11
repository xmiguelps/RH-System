using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// O recálculo do banco de horas de UM dia de UMA pessoa (<see cref="IRecalculoDoBancoDeHoras"/>), no banco REAL com o relógio em 2026-10-07T12:42Z (quarta, 09:42 em Brasília). A carga é de 8h48 (cargo de 44 horas por semana). Cada teste usa a sua pessoa e o seu dia (setembro de
/// 2026). O dia que ainda não passou, o que ainda não se resolve (incompleto, pedido em análise) e o que não é da vida da pessoa (antes da admissão, depois do desligamento) não mexem em nada.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class RecalculoDoBancoDeHorasTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(RecalculoDoBancoDeHorasTests));
        await ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo do banco {Guid.NewGuid():N}", 44m);
        var e = api.Elenco;
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO IN ({e.MembroA.FuncionarioId}, {e.MembroB.FuncionarioId}, {e.DeOutroSetor.FuncionarioId}, {e.Gestor.FuncionarioId}, {e.Inativo.FuncionarioId}, {e.GestorDoFinanceiro.FuncionarioId})");
        return api;
    }

    private static string Utc(DateOnly dia, int hora, int minuto = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00Z";

    /// <summary>Períodos em horário LOCAL (Brasília) gravados como UTC: <c>(8, 0, 12, 0)</c> é das 08:00 às 12:00.</summary>
    private static async Task BaterPeriodosAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia, params (int EntradaHora, int EntradaMinuto, int SaidaHora, int SaidaMinuto)[] periodos)
    {
        var batidas = new List<(TipoDeMarcacao, string)>();
        foreach (var p in periodos)
        {
            batidas.Add((TipoDeMarcacao.Entrada, Utc(dia, p.EntradaHora + 3, p.EntradaMinuto)));
            batidas.Add((TipoDeMarcacao.Saida, Utc(dia, p.SaidaHora + 3, p.SaidaMinuto)));
        }

        await BaterEmAsync(api.Conexao, funcionarioId, dia, [.. batidas]);
    }

    private static Task LimparBatidasAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia) =>
        Sql.ExecutarAsync(api.Conexao, $"DELETE m FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {funcionarioId} AND p.DATA = '{dia:yyyy-MM-dd}'");

    private static async Task RecalcularAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<IRecalculoDoBancoDeHoras>().RecalcularDiaAsync(funcionarioId, dia, Ct);
    }

    private static async Task<List<(string Tipo, decimal Horas, string? Observacao, DateOnly Dia, DateOnly Lancamento)>> LinhasAsync(ApiDeAcesso api, int funcionarioId)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var linhas = await db.LancamentosDoBancoDeHoras.AsNoTracking().Where(l => l.FuncionarioId == funcionarioId).OrderBy(l => l.DiaDeReferencia).ToListAsync();
        return [.. linhas.Select(l => (l.Tipo!.Value.ToString().ToUpperInvariant(), l.Quantidade!.Value, l.Observacao, l.DiaDeReferencia!.Value, l.DataDoLancamento!.Value))];
    }

    private static async Task<List<(string Tipo, decimal Horas, string? Observacao, DateOnly Dia, DateOnly Lancamento)>> LinhasDosDiasAsync(ApiDeAcesso api, int funcionarioId, params DateOnly[] dias) =>
        [.. (await LinhasAsync(api, funcionarioId)).Where(l => dias.Contains(l.Dia))];

    private static Task InserirLinhaAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia, string tipo, string horas, string observacao) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO BANCOS_HORAS (ID_FUNCIONARIO, DATA_LANCAMENTO, TIPO_LANCAMENTO, QUANTIDADE, OBSERVACAO, DIA_REFERENCIA) VALUES ({funcionarioId}, '{dia:yyyy-MM-dd}', '{tipo}', {horas}, '{observacao}', '{dia:yyyy-MM-dd}')");

    // ---------------------------------------------------------------- o que lança

    [Fact]
    public async Task Hora_extra_vira_credito_com_o_dia_de_referencia_e_a_data_de_hoje_e_62_minutos_sao_1_03_hora()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 9, 1);
        await BaterPeriodosAsync(api, api.Elenco.MembroA.FuncionarioId, dia, (8, 0, 12, 0), (13, 0, 18, 50)); // 590 min contra 528

        await RecalcularAsync(api, api.Elenco.MembroA.FuncionarioId, dia);

        Assert.Equal([("CREDITO", 1.03m, (string?)"Hora extra", dia, new DateOnly(2026, 10, 7))], await LinhasDosDiasAsync(api, api.Elenco.MembroA.FuncionarioId, dia));
    }

    [Fact]
    public async Task Cinco_minutos_de_credito_sao_0_08_hora_e_voltam_como_cinco_minutos_no_saldo()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 9, 2);
        await BaterPeriodosAsync(api, api.Elenco.MembroB.FuncionarioId, dia, (8, 0, 12, 0), (13, 0, 17, 53)); // 240 + 293 = 533: cinco a mais

        await RecalcularAsync(api, api.Elenco.MembroB.FuncionarioId, dia);

        Assert.Equal([("CREDITO", 0.08m, (string?)"Hora extra", dia, new DateOnly(2026, 10, 7))], await LinhasDosDiasAsync(api, api.Elenco.MembroB.FuncionarioId, dia));
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var saldos = await escopo.ServiceProvider.GetRequiredService<ISaldoDoBancoDeHoras>().ObterSaldosAsync([api.Elenco.MembroB.FuncionarioId], Ct);
        Assert.True(saldos[api.Elenco.MembroB.FuncionarioId] >= 5); // o saldo soma o dia (cinco minutos) com os outros dias da Júlia neste banco
    }

    [Fact]
    public async Task Dia_util_sem_batida_vira_debito_de_falta_de_8_80_horas_e_o_dia_com_menos_horas_e_saldo_negativo()
    {
        var api = await ApiAsync();
        var semBatida = new DateOnly(2026, 9, 3);
        var curto = new DateOnly(2026, 9, 4);
        await BaterPeriodosAsync(api, api.Elenco.DeOutroSetor.FuncionarioId, curto, (8, 0, 12, 0), (13, 0, 17, 0)); // 480: faltam 48

        await RecalcularAsync(api, api.Elenco.DeOutroSetor.FuncionarioId, semBatida);
        await RecalcularAsync(api, api.Elenco.DeOutroSetor.FuncionarioId, curto);

        Assert.Equal(
            [("DEBITO", 8.80m, (string?)"Falta", semBatida, new DateOnly(2026, 10, 7)), ("DEBITO", 0.80m, (string?)"Saldo negativo do dia", curto, new DateOnly(2026, 10, 7))],
            await LinhasDosDiasAsync(api, api.Elenco.DeOutroSetor.FuncionarioId, semBatida, curto));
    }

    [Fact]
    public async Task Fim_de_semana_e_feriado_sem_batida_nao_lancam_e_o_feriado_trabalhado_e_hora_extra_inteira()
    {
        var api = await ApiAsync();
        var gestor = api.Elenco.GestorDoFinanceiro.FuncionarioId;
        var sabado = new DateOnly(2026, 9, 5);
        var feriado = new DateOnly(2026, 9, 7); // Independência, na carga do V001

        await RecalcularAsync(api, gestor, sabado);
        await RecalcularAsync(api, gestor, feriado);
        Assert.Empty(await LinhasDosDiasAsync(api, gestor, sabado, feriado));

        await BaterPeriodosAsync(api, gestor, feriado, (9, 0, 12, 0));
        await RecalcularAsync(api, gestor, feriado);
        Assert.Equal([("CREDITO", 3.00m, (string?)"Hora extra", feriado, new DateOnly(2026, 10, 7))], await LinhasDosDiasAsync(api, gestor, sabado, feriado));
    }

    // ---------------------------------------------------------------- recalcular de novo

    [Fact]
    public async Task Recalcular_duas_vezes_nao_duplica_o_dia_mudado_atualiza_e_o_saldo_zero_apaga_a_linha()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var dia = new DateOnly(2026, 9, 8);
        await BaterPeriodosAsync(api, pessoa, dia, (8, 0, 12, 0), (13, 0, 18, 50)); // +62

        await RecalcularAsync(api, pessoa, dia);
        await RecalcularAsync(api, pessoa, dia);
        Assert.Equal([("CREDITO", 1.03m)], (await LinhasDosDiasAsync(api, pessoa, dia)).Select(l => (l.Tipo, l.Horas)));

        await LimparBatidasAsync(api, pessoa, dia);
        await BaterPeriodosAsync(api, pessoa, dia, (8, 0, 12, 0), (13, 0, 17, 0)); // -48
        await RecalcularAsync(api, pessoa, dia);
        Assert.Equal([("DEBITO", 0.80m, (string?)"Saldo negativo do dia")], (await LinhasAsync(api, pessoa)).Where(l => l.Dia == dia).Select(l => (l.Tipo, l.Horas, l.Observacao)));

        await LimparBatidasAsync(api, pessoa, dia);
        await BaterPeriodosAsync(api, pessoa, dia, (8, 0, 12, 0), (13, 0, 17, 48)); // 528: zero
        await RecalcularAsync(api, pessoa, dia);
        Assert.DoesNotContain(await LinhasAsync(api, pessoa), l => l.Dia == dia);
    }

    [Fact]
    public async Task Dia_incompleto_nao_lanca_e_lanca_depois_de_resolvido_e_o_pedido_em_analise_deixa_a_linha_como_esta()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB.FuncionarioId;
        var incompleto = new DateOnly(2026, 9, 9);
        var comPedido = new DateOnly(2026, 9, 10);
        await BaterEmAsync(api.Conexao, pessoa, incompleto, (TipoDeMarcacao.Entrada, Utc(incompleto, 11)));
        await InserirLinhaAsync(api, pessoa, comPedido, "DEBITO", "8.80", "Falta");
        await NovoPedidoPendenteAsync(api.Conexao, pessoa, comPedido);

        await RecalcularAsync(api, pessoa, incompleto);
        await RecalcularAsync(api, pessoa, comPedido);

        var linhas = await LinhasDosDiasAsync(api, pessoa, incompleto, comPedido);
        Assert.DoesNotContain(linhas, l => l.Dia == incompleto);
        Assert.Contains(linhas, l => l.Dia == comPedido && l is { Tipo: "DEBITO", Horas: 8.80m }); // a falta continua até o pedido ser decidido

        await BaterEmAsync(api.Conexao, pessoa, incompleto, (TipoDeMarcacao.Saida, Utc(incompleto, 20)));
        await RecalcularAsync(api, pessoa, incompleto);
        Assert.Contains(await LinhasAsync(api, pessoa), l => l.Dia == incompleto && l is { Tipo: "CREDITO", Horas: 0.20m }); // 08:00 a 17:00 = 540: doze a mais
    }

    [Fact]
    public async Task Atestado_validado_de_dia_inteiro_tira_a_falta_e_o_de_algumas_horas_diminui_a_carga()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.Gestor.FuncionarioId;
        var diaInteiro = new DateOnly(2026, 9, 11);
        var algumasHoras = new DateOnly(2026, 9, 14);
        await BaterPeriodosAsync(api, pessoa, algumasHoras, (8, 0, 12, 0), (13, 0, 15, 48)); // 408 min

        await RecalcularAsync(api, pessoa, diaInteiro);
        await RecalcularAsync(api, pessoa, algumasHoras);
        Assert.Equal([("DEBITO", 8.80m, (string?)"Falta"), ("DEBITO", 2.00m, (string?)"Saldo negativo do dia")], (await LinhasAsync(api, pessoa)).Select(l => (l.Tipo, l.Horas, l.Observacao))); // 528 - 408 = 120 min de débito

        await NovoAtestadoAsync(api.Conexao, pessoa, "2026-09-11", "2026-09-11", "VALIDADO", api.Elenco.Rh.FuncionarioId);
        await NovoAtestadoAsync(api.Conexao, pessoa, "2026-09-14", "2026-09-14", "VALIDADO", api.Elenco.Rh.FuncionarioId, "15:48:00", "17:48:00"); // duas horas
        await RecalcularAsync(api, pessoa, diaInteiro);
        await RecalcularAsync(api, pessoa, algumasHoras);

        Assert.Empty(await LinhasAsync(api, pessoa)); // a falta deixou de ser débito e a carga de 528 caiu para 408: saldo zero
    }

    // ---------------------------------------------------------------- inativo e fora do período

    [Fact]
    public async Task Inativo_com_linha_atualiza_e_sem_linha_nao_cria()
    {
        var api = await ApiAsync();
        var henrique = api.Elenco.Inativo.FuncionarioId;
        var comLinha = new DateOnly(2026, 9, 15);
        var semLinha = new DateOnly(2026, 9, 16);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE, DATA_DESLIGAMENTO = NULL WHERE ID_FUNCIONARIO = {henrique}"); // só para semear: o banco recusa batida e linha nova de quem está inativo
        await BaterPeriodosAsync(api, henrique, comLinha, (8, 0, 12, 0), (13, 0, 17, 48), (18, 0, 18, 12)); // 540: doze a mais
        await BaterPeriodosAsync(api, henrique, semLinha, (8, 0, 12, 0), (13, 0, 17, 48), (18, 0, 18, 12));
        await InserirLinhaAsync(api, henrique, comLinha, "DEBITO", "1.00", "Falta");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {henrique}");

        await RecalcularAsync(api, henrique, comLinha);
        await RecalcularAsync(api, henrique, semLinha);

        Assert.Equal([("CREDITO", 0.20m, comLinha)], (await LinhasAsync(api, henrique)).Select(l => (l.Tipo, l.Horas, l.Dia)));
    }

    [Fact]
    public async Task Hoje_o_futuro_o_dia_antes_da_admissao_e_o_depois_do_desligamento_nao_mexem_em_nada()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA.FuncionarioId;
        var hoje = new DateOnly(2026, 10, 7);
        await BaterPeriodosAsync(api, diego, hoje, (8, 0, 9, 0));

        await RecalcularAsync(api, diego, hoje);
        await RecalcularAsync(api, diego, new DateOnly(2026, 10, 8));
        await RecalcularAsync(api, diego, new DateOnly(2025, 12, 31)); // antes de 2026-01 (a admissão dele)
        await RecalcularAsync(api, api.Elenco.Inativo.FuncionarioId, new DateOnly(2026, 10, 2)); // depois do desligamento de 30/09

        Assert.Empty(await LinhasDosDiasAsync(api, diego, hoje, new DateOnly(2026, 10, 8), new DateOnly(2025, 12, 31)));
        Assert.Empty(await LinhasDosDiasAsync(api, api.Elenco.Inativo.FuncionarioId, new DateOnly(2026, 10, 2)));
    }

    [Fact]
    public async Task O_dia_da_admissao_ja_e_lancado()
    {
        var api = await ApiAsync();
        var marina = api.Elenco.GestorDoFinanceiro;
        var dia = new DateOnly(2026, 9, 16);
        var original = Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_ADMISSAO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {marina.FuncionarioId}"), System.Globalization.CultureInfo.InvariantCulture);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '{dia:yyyy-MM-dd}' WHERE ID_FUNCIONARIO = {marina.FuncionarioId}");
        try
        {
            await BaterPeriodosAsync(api, marina.FuncionarioId, dia, (8, 0, 12, 0), (13, 0, 18, 50));

            await RecalcularAsync(api, marina.FuncionarioId, dia);

            Assert.Equal([("CREDITO", 1.03m)], (await LinhasDosDiasAsync(api, marina.FuncionarioId, dia)).Select(l => (l.Tipo, l.Horas)));
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '{original:yyyy-MM-dd}' WHERE ID_FUNCIONARIO = {marina.FuncionarioId}");
        }
    }

    [Fact]
    public async Task Se_outra_gravacao_do_mesmo_dia_chega_no_meio_o_recalculo_rele_e_atualiza_a_linha_dela()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var dia = new DateOnly(2026, 9, 17); // quinta-feira
        await BaterPeriodosAsync(api, pessoa, dia, (8, 0, 12, 0), (13, 0, 18, 50)); // +62
        var intruso = new InseridorDeLinhaNaCorrida(api.Conexao, pessoa, dia);
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos => servicos.AddDbContext<AppDbContext>(opcoes => opcoes.AddInterceptors(intruso))));

        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IRecalculoDoBancoDeHoras>().RecalcularDiaAsync(pessoa, dia, Ct);
        }

        Assert.True(intruso.Disparou);
        Assert.Equal([("CREDITO", 1.03m, (string?)"Hora extra")], (await LinhasDosDiasAsync(api, pessoa, dia)).Select(l => (l.Tipo, l.Horas, l.Observacao))); // a linha da outra gravação (débito de 9,99) foi reescrita
    }

    /// <summary>Na primeira gravação de uma linha nova do banco de horas, insere por OUTRA conexão a linha do mesmo dia: a chave única recusa a gravação e o recálculo tem de reler.</summary>
    private sealed class InseridorDeLinhaNaCorrida(string conexao, int funcionarioId, DateOnly dia) : SaveChangesInterceptor
    {
        public bool Disparou { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Disparou && eventData.Context!.ChangeTracker.Entries<LancamentoDoBancoDeHoras>().Any(e => e.State == EntityState.Added))
            {
                Disparou = true;
                await using var outra = new MySqlConnection(conexao);
                await outra.OpenAsync(cancellationToken);
                await using var comando = outra.CreateCommand();
                comando.CommandText = $"INSERT INTO BANCOS_HORAS (ID_FUNCIONARIO, DATA_LANCAMENTO, TIPO_LANCAMENTO, QUANTIDADE, OBSERVACAO, DIA_REFERENCIA) VALUES ({funcionarioId}, '{dia:yyyy-MM-dd}', 'DEBITO', 9.99, 'da outra gravacao', '{dia:yyyy-MM-dd}')";
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Recalcular_a_pessoa_que_nao_existe_nao_faz_nada()
    {
        var api = await ApiAsync();

        await RecalcularAsync(api, int.MaxValue, new DateOnly(2026, 9, 17));

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = 2147483647"));
    }

    // ---------------------------------------------------------------- a corrida

    [Fact]
    public async Task Quatro_recalculos_ao_mesmo_tempo_do_mesmo_dia_deixam_uma_linha_so_e_nenhum_falha()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB.FuncionarioId;
        var dia = new DateOnly(2026, 9, 18);
        await BaterPeriodosAsync(api, pessoa, dia, (8, 0, 12, 0), (13, 0, 18, 50));

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RecalcularAsync(api, pessoa, dia)));

        Assert.Equal([("CREDITO", 1.03m)], (await LinhasDosDiasAsync(api, pessoa, dia)).Select(l => (l.Tipo, l.Horas)));
    }
}
