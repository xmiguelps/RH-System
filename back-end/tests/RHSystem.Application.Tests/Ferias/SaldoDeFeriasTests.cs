using RHSystem.Application.Ferias.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>
/// O saldo de férias, UM cálculo para as seis telas: o exemplo do Bruno do contrato (18 disponíveis, 15 em pedido, 1 em análise), o que reserva e o que não, o abono, o período do saldo, as próximas férias, a pessoa desligada e o cálculo em lote igual ao individual.
/// </summary>
public class SaldoDeFeriasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static readonly DateOnly Admissao = new(2022, 3, 14);

    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static FeriasParaSaldo Linha(DateOnly periodoInicio, DateOnly periodoFim, DateOnly inicio, DateOnly fim, StatusDasFerias status, int abono = 0, ResultadoDoGestorNasFerias? resultado = null, RespostaDaSugestao? resposta = null) =>
        new(periodoInicio, periodoFim, inicio, fim, abono, status, resultado, resposta);

    private static readonly DateOnly P25Inicio = D(2025, 3, 14);
    private static readonly DateOnly P25Fim = D(2026, 3, 13);

    private static FeriasParaSaldo No2025(DateOnly inicio, DateOnly fim, StatusDasFerias status, int abono = 0, ResultadoDoGestorNasFerias? resultado = null, RespostaDaSugestao? resposta = null) =>
        Linha(P25Inicio, P25Fim, inicio, fim, status, abono, resultado, resposta);

    private static List<FeriasParaSaldo> HistoricoDoBruno() =>
    [
        Linha(D(2022, 3, 14), D(2023, 3, 13), D(2023, 4, 17), D(2023, 5, 16), StatusDasFerias.Concluida),
        Linha(D(2023, 3, 14), D(2024, 3, 13), D(2024, 4, 15), D(2024, 5, 14), StatusDasFerias.Concluida),
        Linha(D(2024, 3, 14), D(2025, 3, 13), D(2025, 4, 14), D(2025, 5, 13), StatusDasFerias.Concluida),
        No2025(D(2026, 4, 13), D(2026, 4, 24), StatusDasFerias.Concluida),
        No2025(D(2026, 11, 16), D(2026, 11, 30), StatusDasFerias.Solicitada),
    ];

    [Fact]
    public void O_exemplo_do_Bruno_do_contrato_da_os_mesmos_numeros_por_periodo_e_no_resumo()
    {
        var saldo = SaldoDeFerias.De(Admissao, null, Hoje, HistoricoDoBruno());

        Assert.Equal(
            [("SEM_SALDO", 30, 0, 0), ("SEM_SALDO", 30, 0, 0), ("SEM_SALDO", 30, 0, 0), ("COM_SALDO", 12, 15, 18), ("EM_AQUISICAO", 0, 0, 0)],
            saldo.Periodos.Select(p => (Texto(p.Situacao), p.DiasTirados, p.DiasEmPedido, p.DiasDisponiveis)));
        Assert.All(saldo.Periodos, p => Assert.Equal(30, p.DiasDeDireito));
        Assert.Equal(18, saldo.DiasDisponiveis);
        Assert.Equal((P25Inicio, P25Fim, D(2027, 3, 13)), (saldo.PeriodoDoSaldo!.Inicio, saldo.PeriodoDoSaldo.Fim, saldo.PeriodoDoSaldo.PrazoParaTirar));
        Assert.Equal((D(2026, 3, 14), D(2027, 3, 13)), (saldo.PeriodoAtual!.Inicio, saldo.PeriodoAtual.Fim));
        Assert.Equal((6, D(2027, 3, 14)), (saldo.PeriodoAtual.MesesTrabalhados, saldo.PeriodoAtual.DisponivelAPartirDe));
        Assert.Null(saldo.ProximasFerias);
        Assert.Equal(1, saldo.PedidosEmAnalise);
    }

    [Fact]
    public void So_o_periodo_em_andamento_mostra_meses_trabalhados_e_a_data_de_disponibilidade()
    {
        var saldo = SaldoDeFerias.De(Admissao, null, Hoje, HistoricoDoBruno());

        Assert.All(saldo.Periodos.Take(4), p =>
        {
            Assert.Null(p.MesesTrabalhados);
            Assert.Null(p.DisponivelAPartirDe);
        });
    }

    private static string Texto(SituacaoDoPeriodo situacao) => situacao switch
    {
        SituacaoDoPeriodo.EmAquisicao => "EM_AQUISICAO",
        SituacaoDoPeriodo.ComSaldo => "COM_SALDO",
        _ => "SEM_SALDO",
    };

    [Theory]
    [InlineData(StatusDasFerias.Aprovada, 10, 0)]
    [InlineData(StatusDasFerias.EmGozo, 10, 0)]
    [InlineData(StatusDasFerias.Concluida, 10, 0)]
    [InlineData(StatusDasFerias.Solicitada, 0, 10)]
    [InlineData(StatusDasFerias.Rejeitada, 0, 0)]
    [InlineData(StatusDasFerias.Cancelada, 0, 0)]
    public void Cada_status_conta_como_tirado_reservado_ou_nada(StatusDasFerias status, int tirados, int emPedido)
    {
        var saldo = SaldoDeFerias.De(Admissao, null, Hoje, [No2025(D(2026, 5, 4), D(2026, 5, 13), status)]);

        var periodo = saldo.Periodos[3];
        Assert.Equal((tirados, emPedido), (periodo.DiasTirados, periodo.DiasEmPedido));
        Assert.Equal(30 - tirados, periodo.DiasDisponiveis);
    }

    [Fact]
    public void Pedido_em_SUGESTAO_DE_DATA_nao_reserva_e_depois_de_aceita_a_sugestao_reserva_com_as_datas_novas()
    {
        var emSugestao = SaldoDeFerias.De(Admissao, null, Hoje, [No2025(D(2026, 5, 4), D(2026, 5, 13), StatusDasFerias.Solicitada, resultado: ResultadoDoGestorNasFerias.SugeriuData)]);
        var aceita = SaldoDeFerias.De(Admissao, null, Hoje, [No2025(D(2026, 5, 4), D(2026, 5, 18), StatusDasFerias.Solicitada, resultado: ResultadoDoGestorNasFerias.SugeriuData, resposta: RespostaDaSugestao.Aceitou)]);

        Assert.Equal(1, emSugestao.PedidosEmAnalise);
        Assert.Equal(0, emSugestao.Periodos[3].DiasEmPedido);
        Assert.Equal(15, aceita.Periodos[3].DiasEmPedido);
        Assert.Equal(1, aceita.PedidosEmAnalise);
    }

    [Fact]
    public void O_abono_pecuniario_entra_no_que_foi_tirado_e_no_que_esta_em_pedido()
    {
        var tirado = SaldoDeFerias.De(Admissao, null, Hoje, [No2025(D(2026, 5, 4), D(2026, 5, 23), StatusDasFerias.Aprovada, abono: 10)]);
        var emPedido = SaldoDeFerias.De(Admissao, null, Hoje, [No2025(D(2026, 5, 4), D(2026, 5, 13), StatusDasFerias.Solicitada, abono: 5)]);

        Assert.Equal((30, 0, 0, "SEM_SALDO"), (tirado.Periodos[3].DiasTirados, tirado.Periodos[3].DiasEmPedido, tirado.Periodos[3].DiasDisponiveis, Texto(tirado.Periodos[3].Situacao)));
        Assert.Equal((0, 15, 30), (emPedido.Periodos[3].DiasTirados, emPedido.Periodos[3].DiasEmPedido, emPedido.Periodos[3].DiasDisponiveis));
    }

    [Fact]
    public void O_saldo_nunca_fica_negativo_mesmo_com_dado_que_passa_de_30()
    {
        var saldo = SaldoDeFerias.De(P25Inicio, null, Hoje, [No2025(D(2026, 4, 1), D(2026, 4, 30), StatusDasFerias.Concluida, abono: 10)]);

        Assert.Equal((40, 0), (saldo.Periodos[0].DiasTirados, saldo.Periodos[0].DiasDisponiveis));
        Assert.Equal(0, saldo.DiasDisponiveis);
    }

    [Fact]
    public void O_periodo_do_saldo_e_o_fechado_mais_antigo_com_saldo_e_o_total_so_soma_periodos_fechados()
    {
        var pedidos = new List<FeriasParaSaldo>
        {
            Linha(D(2022, 3, 14), D(2023, 3, 13), D(2023, 4, 17), D(2023, 5, 16), StatusDasFerias.Concluida), // 2022 a 2023: sem saldo
            Linha(D(2023, 3, 14), D(2024, 3, 13), D(2024, 4, 15), D(2024, 4, 24), StatusDasFerias.Concluida), // 2023 a 2024: 20 de saldo
            No2025(D(2026, 4, 13), D(2026, 4, 24), StatusDasFerias.Concluida), // 2025 a 2026: 18 de saldo
            Linha(D(2026, 3, 14), D(2027, 3, 13), D(2027, 4, 5), D(2027, 4, 14), StatusDasFerias.Solicitada), // em andamento: nada de saldo
        };

        var saldo = SaldoDeFerias.De(Admissao, null, Hoje, pedidos);

        Assert.Equal(D(2023, 3, 14), saldo.PeriodoDoSaldo!.Inicio);
        Assert.Equal(20 + 30 + 18, saldo.DiasDisponiveis);
        Assert.Equal(0, saldo.PeriodoAtual!.DiasDisponiveis);
    }

    [Fact]
    public void Sem_nenhum_saldo_o_periodo_do_saldo_e_nulo()
    {
        var saldo = SaldoDeFerias.De(D(2026, 3, 14), null, Hoje, []);

        Assert.Equal(0, saldo.DiasDisponiveis);
        Assert.Null(saldo.PeriodoDoSaldo);
        Assert.NotNull(saldo.PeriodoAtual);
    }

    [Fact]
    public void As_proximas_ferias_sao_a_aprovada_mais_proxima_que_ainda_nao_comecou()
    {
        var pedidos = new List<FeriasParaSaldo>
        {
            No2025(D(2026, 10, 7), D(2026, 10, 16), StatusDasFerias.Aprovada), // começa hoje: já começou
            No2025(D(2026, 12, 14), D(2026, 12, 23), StatusDasFerias.Aprovada),
            No2025(D(2026, 10, 20), D(2026, 10, 29), StatusDasFerias.Aprovada), // a mais próxima
            No2025(D(2026, 10, 9), D(2026, 10, 14), StatusDasFerias.Solicitada), // só pedido
            No2025(D(2026, 10, 8), D(2026, 10, 12), StatusDasFerias.EmGozo),
        };

        var saldo = SaldoDeFerias.De(Admissao, null, Hoje, pedidos);

        Assert.Equal((D(2026, 10, 20), D(2026, 10, 29), 10), (saldo.ProximasFerias!.DataInicio, saldo.ProximasFerias.DataFim, saldo.ProximasFerias.Dias));
    }

    [Fact]
    public void As_proximas_ferias_do_dia_seguinte_contam_e_as_do_proprio_dia_nao()
    {
        var amanha = SaldoDeFerias.De(Admissao, null, Hoje, [No2025(D(2026, 10, 8), D(2026, 10, 12), StatusDasFerias.Aprovada)]);
        var hoje = SaldoDeFerias.De(Admissao, null, Hoje, [No2025(D(2026, 10, 7), D(2026, 10, 11), StatusDasFerias.Aprovada)]);

        Assert.Equal(5, amanha.ProximasFerias!.Dias);
        Assert.Null(hoje.ProximasFerias);
    }

    [Fact]
    public void Ferias_de_um_periodo_que_nao_e_deste_vinculo_aparecem_so_no_que_esta_em_analise_e_nao_mexem_no_saldo()
    {
        // readmissão: a admissão nova muda os períodos, e as férias antigas não casam com nenhum período novo
        var saldo = SaldoDeFerias.De(D(2026, 3, 14), null, Hoje, [Linha(D(2021, 1, 4), D(2022, 1, 3), D(2022, 2, 7), D(2022, 3, 8), StatusDasFerias.Concluida)]);

        Assert.Equal(0, saldo.Periodos[0].DiasTirados);
        Assert.Equal(0, saldo.PedidosEmAnalise);
    }

    [Fact]
    public void Pessoa_desligada_nao_tem_periodo_em_andamento_nem_o_que_tirar_de_um_ano_que_nao_fechou()
    {
        var saldo = SaldoDeFerias.De(Admissao, D(2024, 6, 1), Hoje, [Linha(D(2022, 3, 14), D(2023, 3, 13), D(2023, 4, 17), D(2023, 4, 26), StatusDasFerias.Concluida)]);

        Assert.Equal(2, saldo.Periodos.Count);
        Assert.Null(saldo.PeriodoAtual);
        Assert.Equal(20 + 30, saldo.DiasDisponiveis);
    }

    [Fact]
    public void O_calculo_em_lote_da_o_mesmo_resultado_do_individual_para_cada_pessoa()
    {
        var historicoDoBruno = HistoricoDoBruno();
        var pessoas = new List<(int, DateOnly, DateOnly?, IReadOnlyList<FeriasParaSaldo>)>
        {
            (12, Admissao, null, historicoDoBruno),
            (13, D(2026, 3, 14), null, []),
            (14, Admissao, D(2024, 6, 1), []),
        };

        var lote = SaldoDeFerias.DeVarias(Hoje, pessoas);

        Assert.Equal([12, 13, 14], lote.Keys.Order());
        Assert.Equal(SaldoDeFerias.De(Admissao, null, Hoje, historicoDoBruno).DiasDisponiveis, lote[12].DiasDisponiveis);
        Assert.Equal(18, lote[12].DiasDisponiveis);
        Assert.Equal(1, lote[12].PedidosEmAnalise);
        Assert.Single(lote[13].Periodos);
        Assert.Equal(60, lote[14].DiasDisponiveis);
        Assert.Null(lote[14].PeriodoAtual);
    }
}
