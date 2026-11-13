using RHSystem.Application.Ferias.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>
/// As regras de férias e a conferência (uma tabela por regra, com as bordas): o exemplo do contrato (15 dias do Bruno, "sobram 3"), duração, saldo, parte de 14 dias (a fórmula do plano: <c>max(maior parte, este pedido) &gt;= 14</c> OU <c>sobra uma parte e ainda restam 14</c>),
/// máximo de 3 partes, abono, antecedência, feriado e descanso, sobreposição, os quatro modos e a escolha do período.
/// </summary>
public class ConferenciaDeFeriasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static readonly DateOnly Admissao = new(2022, 3, 14);
    private static readonly DateOnly P25Inicio = new(2025, 3, 14);
    private static readonly DateOnly P25Fim = new(2026, 3, 13);

    private static readonly HashSet<DateOnly> Feriados = [new(2026, 11, 15), new(2026, 11, 20), new(2026, 12, 25)];

    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static FeriasParaSaldo No2025(DateOnly inicio, DateOnly fim, StatusDasFerias status, int abono = 0, ResultadoDoGestorNasFerias? resultado = null, RespostaDaSugestao? resposta = null) =>
        new(P25Inicio, P25Fim, inicio, fim, abono, status, resultado, resposta);

    private static FeriasParaSaldo Dias(int dias, StatusDasFerias status = StatusDasFerias.Concluida) => No2025(D(2026, 1, 5), D(2026, 1, 5).AddDays(dias - 1), status);

    /// <summary>O histórico do exemplo do contrato antes do pedido 340: três períodos sem saldo e o de 2025 com uma parte de 12 dias.</summary>
    private static List<FeriasParaSaldo> HistoricoDoBruno() =>
    [
        new(D(2022, 3, 14), D(2023, 3, 13), D(2023, 4, 17), D(2023, 5, 16), 0, StatusDasFerias.Concluida, null, null),
        new(D(2023, 3, 14), D(2024, 3, 13), D(2024, 4, 15), D(2024, 5, 14), 0, StatusDasFerias.Concluida, null, null),
        new(D(2024, 3, 14), D(2025, 3, 13), D(2025, 4, 14), D(2025, 5, 13), 0, StatusDasFerias.Concluida, null, null),
        No2025(D(2026, 4, 13), D(2026, 4, 24), StatusDasFerias.Concluida),
    ];

    private static ContextoDaConferencia Contexto(List<FeriasParaSaldo>? ferias = null, DateOnly? admissao = null, ModoDaConferencia modo = ModoDaConferencia.Pessoa, DateOnly? periodoFixo = null)
    {
        var lista = ferias ?? HistoricoDoBruno();
        return new ContextoDaConferencia(Hoje, modo, SaldoDeFerias.De(admissao ?? Admissao, null, Hoje, lista), lista, Feriados.Contains, periodoFixo);
    }

    /// <summary>Quem foi admitido em 14/03/2025: o período de 2025 a 2026 é o único fechado (as linhas dos testes de partes caem nele).</summary>
    private static ContextoDaConferencia Contexto25(List<FeriasParaSaldo> ferias, ModoDaConferencia modo = ModoDaConferencia.Pessoa) => Contexto(ferias, P25Inicio, modo);

    private static ComandoDeFerias Pedido(DateOnly inicio, DateOnly fim, bool abono = false, int diasAbono = 0) => new(inicio, fim, abono, diasAbono);

    private static ResultadoDaRegra Regra(ConferenciaDeFerias c, RegraDeFerias regra) => c.Regras.Single(r => r.Regra == regra);

    [Theory]
    [InlineData(2023, 2, 6, false)] // bem antes do fim do primeiro período (13/03/2023)
    [InlineData(2023, 3, 13, false)] // no último dia do período
    [InlineData(2023, 3, 14, true)] // o primeiro dia depois do fim
    public void Inicio_antes_do_fim_do_primeiro_periodo_nao_cabe_no_saldo_mesmo_com_o_saldo_cheio_porque_o_banco_recusaria(int ano, int mes, int dia, bool cabe)
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(ano, mes, dia), D(ano, mes, dia).AddDays(9)), Contexto(ferias: [], modo: ModoDaConferencia.Rh));

        var saldo = Regra(c, RegraDeFerias.CabeNoSaldo);
        Assert.Equal(cabe, saldo.Atendida);
        Assert.Equal(cabe, c.PodeEnviar);
        if (!cabe)
        {
            Assert.Equal("As férias precisam começar depois do fim do período aquisitivo (13/03/2023).", saldo.Mensagem);
        }
    }

    [Fact]
    public void O_exemplo_do_contrato_15_dias_a_partir_de_16_de_novembro_atende_as_sete_regras_e_sobram_3_dias()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 30)), Contexto());

        Assert.Equal((15, 3, true, (DateOnly?)D(2026, 11, 14)), (c.TotalDias, c.DiasQueSobram, c.PodeEnviar, c.PagamentoAte));
        Assert.Equal(
            [
                (RegraDeFerias.DuracaoDaParte, true, "A parte tem entre 5 e 30 dias."),
                (RegraDeFerias.CabeNoSaldo, true, "Cabe no saldo disponível."),
                (RegraDeFerias.ParteDe14Dias, true, "Tem pelo menos 14 dias seguidos."),
                (RegraDeFerias.MaximoDe3Partes, true, "É a 2ª parte do período."),
                (RegraDeFerias.AntecedenciaDe30Dias, true, "Começa daqui a mais de 30 dias."),
                (RegraDeFerias.NaoComecaPertoDeFeriadoOuDescanso, true, "Não começa nos 2 dias antes de um feriado ou do fim de semana."),
                (RegraDeFerias.SemSobreposicao, true, "Não coincide com outras férias suas."),
            ],
            c.Regras.Select(r => (r.Regra, r.Atendida, r.Mensagem)));
        Assert.Equal(P25Inicio, c.Periodo!.Inicio);
    }

    [Fact]
    public void Passar_do_saldo_diz_quantos_dias_faltam_e_diasQueSobram_fica_negativo()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 12, 4)), Contexto()); // 19 dias para 18 de saldo

        Assert.Equal((19, -1, false), (c.TotalDias, c.DiasQueSobram, c.PodeEnviar));
        Assert.Equal((false, "Passa do saldo disponível: falta 1 dia."), (Regra(c, RegraDeFerias.CabeNoSaldo).Atendida, Regra(c, RegraDeFerias.CabeNoSaldo).Mensagem));
        var quatro = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 12, 7)), Contexto()); // 22 dias
        Assert.Equal("Passa do saldo disponível: faltam 4 dias.", Regra(quatro, RegraDeFerias.CabeNoSaldo).Mensagem);
    }

    [Fact]
    public void O_que_esta_em_pedido_fica_reservado_e_nao_pode_ser_pedido_de_novo()
    {
        var ferias = HistoricoDoBruno();
        ferias.Add(No2025(D(2026, 11, 16), D(2026, 11, 30), StatusDasFerias.Solicitada)); // 15 reservados: livre 3
        var tres = ConferenciaDeFerias.Conferir(Pedido(D(2027, 1, 11), D(2027, 1, 13)), Contexto(ferias));
        var seis = ConferenciaDeFerias.Conferir(Pedido(D(2027, 1, 11), D(2027, 1, 16)), Contexto(ferias));

        Assert.Equal((3, 0), (tres.TotalDias, tres.DiasQueSobram)); // cabe, mas a duração mínima barra
        Assert.True(Regra(tres, RegraDeFerias.CabeNoSaldo).Atendida);
        Assert.False(Regra(seis, RegraDeFerias.CabeNoSaldo).Atendida);
        Assert.Equal(-3, seis.DiasQueSobram);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(29, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void Cada_parte_tem_de_5_a_30_dias_com_as_duas_pontas_dentro(int dias, bool vale)
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 16).AddDays(dias - 1)), Contexto([]));

        Assert.Equal(vale, Regra(c, RegraDeFerias.DuracaoDaParte).Atendida);
        Assert.Equal(vale ? "A parte tem entre 5 e 30 dias." : "A parte precisa ter entre 5 e 30 dias.", Regra(c, RegraDeFerias.DuracaoDaParte).Mensagem);
    }

    [Theory]
    [InlineData(false, 0, false)] // sem abono a regra nem aparece
    [InlineData(true, 1, true)]
    [InlineData(true, 10, true)]
    [InlineData(true, 11, false)]
    [InlineData(true, 0, false)]
    [InlineData(false, 3, false)]
    public void O_abono_e_de_1_a_10_dias_e_so_entra_na_lista_quando_a_pessoa_usa_abono(bool abono, int diasAbono, bool atendida)
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25), abono, diasAbono), Contexto([]));

        var aparece = abono || diasAbono > 0;
        Assert.Equal(aparece, c.Regras.Any(r => r.Regra == RegraDeFerias.AbonoAte10Dias));
        if (aparece)
        {
            Assert.Equal(atendida, Regra(c, RegraDeFerias.AbonoAte10Dias).Atendida);
        }
    }

    [Fact]
    public void O_abono_conta_no_que_cabe_no_saldo_e_a_mensagem_diz_o_numero_de_dias()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 30), true, 10), Contexto()); // 15 + 10 = 25 para 18

        Assert.Equal((false, -7), (Regra(c, RegraDeFerias.CabeNoSaldo).Atendida, c.DiasQueSobram));
        Assert.Equal("Abono de 10 dias.", Regra(c, RegraDeFerias.AbonoAte10Dias).Mensagem);
        Assert.Equal("Abono de 1 dia.", Regra(ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25), true, 1), Contexto([])), RegraDeFerias.AbonoAte10Dias).Mensagem);
        Assert.Equal("O abono é de 1 a 10 dias.", Regra(ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25), true, 11), Contexto([])), RegraDeFerias.AbonoAte10Dias).Mensagem);
    }

    [Theory]
    [InlineData(2026, 11, 5, false)] // 29 dias
    [InlineData(2026, 11, 6, true)] // 30 dias
    [InlineData(2026, 11, 7, true)]
    public void A_antecedencia_e_de_pelo_menos_30_dias_depois_de_hoje(int ano, int mes, int dia, bool vale)
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(ano, mes, dia), D(ano, mes, dia).AddDays(9)), Contexto([]));

        Assert.Equal(vale, Regra(c, RegraDeFerias.AntecedenciaDe30Dias).Atendida);
        Assert.Equal(vale ? "Começa daqui a mais de 30 dias." : "Precisa começar pelo menos 30 dias depois de hoje.", Regra(c, RegraDeFerias.AntecedenciaDe30Dias).Mensagem);
    }

    [Theory]
    [InlineData(2026, 11, 16, true)] // segunda
    [InlineData(2026, 11, 17, true)] // terça
    [InlineData(2026, 11, 18, false)] // quarta: dois dias antes do feriado de sexta 20/11
    [InlineData(2026, 11, 19, false)] // quinta: véspera do feriado
    [InlineData(2026, 11, 20, false)] // o próprio feriado
    [InlineData(2026, 11, 21, false)] // sábado
    [InlineData(2026, 11, 22, false)] // domingo
    [InlineData(2026, 11, 23, true)]
    [InlineData(2026, 11, 25, true)] // quarta
    [InlineData(2026, 11, 26, false)] // quinta: dois dias antes do sábado
    [InlineData(2026, 11, 27, false)] // sexta: véspera do sábado
    public void Nao_comeca_em_feriado_nem_no_fim_de_semana_nem_nos_2_dias_antes_deles(int ano, int mes, int dia, bool vale)
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(ano, mes, dia), D(ano, mes, dia).AddDays(9)), Contexto([]));

        Assert.Equal(vale, Regra(c, RegraDeFerias.NaoComecaPertoDeFeriadoOuDescanso).Atendida);
    }

    [Fact]
    public void Feriado_numa_terca_proibe_a_segunda_mas_nao_a_quarta_seguinte()
    {
        var ehFeriado = new HashSet<DateOnly> { D(2027, 5, 4) }.Contains; // terça
        ContextoDaConferencia Com() => new(Hoje, ModoDaConferencia.Pessoa, SaldoDeFerias.De(Admissao, null, Hoje, []), [], ehFeriado, null);

        Assert.False(Regra(ConferenciaDeFerias.Conferir(Pedido(D(2027, 5, 3), D(2027, 5, 12)), Com()), RegraDeFerias.NaoComecaPertoDeFeriadoOuDescanso).Atendida); // segunda
        Assert.True(Regra(ConferenciaDeFerias.Conferir(Pedido(D(2027, 5, 5), D(2027, 5, 14)), Com()), RegraDeFerias.NaoComecaPertoDeFeriadoOuDescanso).Atendida); // quarta, depois do feriado: ainda faltam 3 dias para o sábado
    }

    [Theory]
    [InlineData(0, true, "É a 1ª parte do período.")]
    [InlineData(1, true, "É a 2ª parte do período.")]
    [InlineData(2, true, "É a 3ª parte do período.")]
    [InlineData(3, false, "O período já tem 3 partes.")]
    public void No_maximo_3_partes_por_periodo_aquisitivo(int existentes, bool vale, string mensagem)
    {
        var ferias = new List<FeriasParaSaldo>();
        for (var i = 0; i < existentes; i++)
        {
            ferias.Add(No2025(D(2026, 1, 5).AddDays(i * 40), D(2026, 1, 9).AddDays(i * 40), StatusDasFerias.Concluida)); // 5 dias cada
        }

        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 20)), Contexto25(ferias));

        Assert.Equal((vale, mensagem), (Regra(c, RegraDeFerias.MaximoDe3Partes).Atendida, Regra(c, RegraDeFerias.MaximoDe3Partes).Mensagem));
    }

    [Fact]
    public void Partes_em_SUGESTAO_DE_DATA_rejeitadas_e_canceladas_nao_contam_mas_as_em_pedido_contam()
    {
        var ferias = new List<FeriasParaSaldo>
        {
            No2025(D(2026, 1, 5), D(2026, 1, 9), StatusDasFerias.Solicitada, resultado: ResultadoDoGestorNasFerias.SugeriuData),
            No2025(D(2026, 2, 5), D(2026, 2, 9), StatusDasFerias.Rejeitada),
            No2025(D(2026, 3, 5), D(2026, 3, 9), StatusDasFerias.Cancelada),
            No2025(D(2026, 4, 6), D(2026, 4, 10), StatusDasFerias.Solicitada),
        };

        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 20)), Contexto25(ferias));

        Assert.Equal("É a 2ª parte do período.", Regra(c, RegraDeFerias.MaximoDe3Partes).Mensagem);
    }

    [Theory]
    [InlineData("12", 0, 14, true)] // o Bruno do ELENCO: 12 tirados, 18 de saldo: de 14 a 18
    [InlineData("12", 0, 13, false)]
    [InlineData("12", 0, 18, true)]
    [InlineData("12", 0, 5, false)] // sobrariam 13: não dá mais para ter uma parte de 14
    [InlineData("6+6", 0, 14, true)] // duas partes de 6: esta é a 3ª e é a de 14
    [InlineData("6+6", 0, 13, false)]
    [InlineData("14", 0, 5, true)] // já tem uma de 14
    [InlineData("", 0, 5, true)] // sobram 25 e ainda cabem 2 partes
    [InlineData("", 0, 16, true)]
    [InlineData("", 0, 17, true)]
    [InlineData("10", 0, 10, false)] // sobram 10 e esta é de 10: nenhuma de 14
    [InlineData("10", 0, 5, true)] // sobram 15
    [InlineData("5", 0, 5, true)] // esta é a 2ª parte e sobram 20
    [InlineData("5+5", 0, 5, false)] // esta é a 3ª parte: não há mais parte para ter os 14, mesmo sobrando 15
    [InlineData("5+5", 0, 14, true)]
    [InlineData("12", 5, 5, false)] // o abono de 5 dias entra no que foi tirado: sobram 8
    [InlineData("12", 5, 13, false)]
    [InlineData("", 10, 5, true)] // abono sem parte: sobram 15
    public void A_parte_de_14_dias_depois_do_pedido_ainda_precisa_ser_possivel(string partesExistentes, int abonoTirado, int pedido, bool vale)
    {
        // as partes existentes são "concluídas" no período de 2025; o abono já tirado vai na primeira parte (ou numa de 5 dias quando não há parte)
        var ferias = new List<FeriasParaSaldo>();
        var inicio = D(2026, 1, 5);
        var abonoPendente = abonoTirado;
        foreach (var parte in partesExistentes.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var n = int.Parse(parte, System.Globalization.CultureInfo.InvariantCulture);
            ferias.Add(new FeriasParaSaldo(P25Inicio, P25Fim, inicio, inicio.AddDays(n - 1), abonoPendente, StatusDasFerias.Concluida, null, null));
            abonoPendente = 0;
            inicio = inicio.AddDays(n + 40);
        }

        if (abonoPendente > 0)
        {
            ferias.Add(new FeriasParaSaldo(P25Inicio, P25Fim, inicio, inicio.AddDays(4), abonoPendente - 5, StatusDasFerias.Concluida, null, null)); // 5 dias de gozo e o resto de abono: uma parte
        }

        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 16).AddDays(pedido - 1)), Contexto25(ferias));

        Assert.Equal(vale, Regra(c, RegraDeFerias.ParteDe14Dias).Atendida);
    }

    [Fact]
    public void As_tres_mensagens_da_parte_de_14_dias()
    {
        var propria = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 30)), Contexto());
        var jaTem = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 20)), Contexto25([Dias(14)]));
        var aindaDa = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 20)), Contexto25([]));
        var naoDa = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 20)), Contexto25([Dias(12)]));

        Assert.Equal("Tem pelo menos 14 dias seguidos.", Regra(propria, RegraDeFerias.ParteDe14Dias).Mensagem);
        Assert.Equal("O período já tem uma parte de 14 dias.", Regra(jaTem, RegraDeFerias.ParteDe14Dias).Mensagem);
        Assert.Equal("Ainda dá para ter uma parte de 14 dias.", Regra(aindaDa, RegraDeFerias.ParteDe14Dias).Mensagem);
        Assert.Equal((false, "Depois deste pedido ainda precisa dar para ter uma parte de 14 dias seguidos."), (Regra(naoDa, RegraDeFerias.ParteDe14Dias).Atendida, Regra(naoDa, RegraDeFerias.ParteDe14Dias).Mensagem));
    }

    [Theory]
    [InlineData(StatusDasFerias.Solicitada, true)]
    [InlineData(StatusDasFerias.Aprovada, true)]
    [InlineData(StatusDasFerias.EmGozo, true)]
    [InlineData(StatusDasFerias.Concluida, true)]
    [InlineData(StatusDasFerias.Rejeitada, false)]
    [InlineData(StatusDasFerias.Cancelada, false)]
    public void So_pedido_em_andamento_aprovado_ou_em_gozo_bloqueia_o_mesmo_dia(StatusDasFerias status, bool bloqueia)
    {
        var ferias = new List<FeriasParaSaldo> { new(P25Inicio, P25Fim, D(2026, 11, 9), D(2026, 11, 16), 0, status, null, null) };

        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25)), Contexto25(ferias)); // um dia em comum: o 16

        Assert.Equal(!bloqueia, Regra(c, RegraDeFerias.SemSobreposicao).Atendida);
    }

    [Fact]
    public void Dias_colados_nao_se_sobrepoem_e_a_sugestao_pendente_nao_bloqueia()
    {
        var colado = new List<FeriasParaSaldo> { new(P25Inicio, P25Fim, D(2026, 11, 9), D(2026, 11, 15), 0, StatusDasFerias.Aprovada, null, null) };
        var emSugestao = new List<FeriasParaSaldo> { new(P25Inicio, P25Fim, D(2026, 11, 16), D(2026, 11, 25), 0, StatusDasFerias.Solicitada, ResultadoDoGestorNasFerias.SugeriuData, null) };

        Assert.True(Regra(ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25)), Contexto(colado)), RegraDeFerias.SemSobreposicao).Atendida);
        Assert.True(Regra(ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25)), Contexto(emSugestao)), RegraDeFerias.SemSobreposicao).Atendida);
        var sobrepoe = new List<FeriasParaSaldo> { new(P25Inicio, P25Fim, D(2026, 11, 25), D(2026, 12, 4), 0, StatusDasFerias.Aprovada, null, null) };
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25)), Contexto(sobrepoe));
        Assert.Equal((false, "Coincide com outras férias suas."), (Regra(c, RegraDeFerias.SemSobreposicao).Atendida, Regra(c, RegraDeFerias.SemSobreposicao).Mensagem));
    }

    [Fact]
    public void O_pagamento_vai_ate_2_dias_corridos_antes_do_inicio_e_sem_datas_validas_nao_ha_data()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 30)), Contexto());
        var invertido = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 30), D(2026, 11, 16)), Contexto());

        Assert.Equal(D(2026, 11, 14), c.PagamentoAte);
        Assert.Equal((0, null), (invertido.TotalDias, invertido.PagamentoAte));
        Assert.False(Regra(invertido, RegraDeFerias.DuracaoDaParte).Atendida);
    }

    [Theory]
    [InlineData(ModoDaConferencia.Rh, "Não começa nos 2 dias antes de um feriado ou do descanso semanal.", "Não coincide com outras férias da pessoa.")]
    [InlineData(ModoDaConferencia.SugestaoDoGestor, "Não começa nos 2 dias antes de um feriado ou do descanso semanal.", "Não coincide com outras férias da pessoa.")]
    [InlineData(ModoDaConferencia.AceiteDaSugestao, "Não começa nos 2 dias antes de um feriado ou do fim de semana.", "Não coincide com outras férias suas.")]
    public void Os_tres_modos_sem_antecedencia_nao_listam_a_regra_e_usam_o_texto_de_quem_e_o_dono(ModoDaConferencia modo, string descanso, string sobreposicao)
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 30)), Contexto(modo: modo));

        Assert.DoesNotContain(c.Regras, r => r.Regra == RegraDeFerias.AntecedenciaDe30Dias);
        Assert.Equal(descanso, Regra(c, RegraDeFerias.NaoComecaPertoDeFeriadoOuDescanso).Mensagem);
        Assert.Equal(sobreposicao, Regra(c, RegraDeFerias.SemSobreposicao).Mensagem);
        Assert.Equal(6, c.Regras.Count);
        Assert.True(c.PodeEnviar);
    }

    [Fact]
    public void O_registro_do_RH_pode_ser_no_passado_e_a_antecedencia_nao_vale()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2023, 6, 5), D(2023, 6, 14)), Contexto([], modo: ModoDaConferencia.Rh));

        Assert.Equal(new DateOnly(2022, 3, 14), c.Periodo!.Inicio);
        Assert.True(c.PodeEnviar);
        Assert.Equal(D(2023, 6, 3), c.PagamentoAte);
    }

    [Fact]
    public void O_periodo_escolhido_e_o_mais_antigo_que_cabe_e_so_entram_os_que_terminam_antes_do_inicio()
    {
        // 2022 a 2023 com só 10 de saldo livre; 2023 a 2024 com 30: um pedido de 12 dias vai para o segundo
        var ferias = new List<FeriasParaSaldo> { new(D(2022, 3, 14), D(2023, 3, 13), D(2023, 4, 17), D(2023, 5, 6), 0, StatusDasFerias.Concluida, null, null) };

        var cabeNoSegundo = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 27)), Contexto(ferias));
        var cabeNoPrimeiro = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 24)), Contexto(ferias)); // 9 dias
        var retroativo = ConferenciaDeFerias.Conferir(Pedido(D(2023, 6, 5), D(2023, 6, 16)), Contexto(ferias, modo: ModoDaConferencia.Rh)); // só o primeiro período terminou antes

        Assert.Equal(D(2023, 3, 14), cabeNoSegundo.Periodo!.Inicio);
        Assert.Equal(D(2022, 3, 14), cabeNoPrimeiro.Periodo!.Inicio);
        Assert.Equal(D(2022, 3, 14), retroativo.Periodo!.Inicio);
        Assert.False(Regra(retroativo, RegraDeFerias.CabeNoSaldo).Atendida); // 12 dias para 10
    }

    [Fact]
    public void Quando_nenhum_periodo_comporta_o_pedido_os_numeros_se_referem_ao_periodo_do_saldo()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 12, 15)), Contexto()); // 30 dias; o período do saldo (2025) tem 18

        Assert.Equal(P25Inicio, c.Periodo!.Inicio);
        Assert.Equal(-12, c.DiasQueSobram);
        Assert.False(c.SemPeriodoAquisitivo);
    }

    [Fact]
    public void Quem_ainda_nao_completou_um_ano_nao_tem_periodo_aquisitivo()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 30)), Contexto([], admissao: D(2026, 3, 14)));

        Assert.True(c.SemPeriodoAquisitivo);
        Assert.Null(c.Periodo);
        Assert.False(c.PodeEnviar);
        Assert.Equal((false, "Você ainda não completou um ano de casa."), (Regra(c, RegraDeFerias.CabeNoSaldo).Atendida, Regra(c, RegraDeFerias.CabeNoSaldo).Mensagem));
        Assert.Equal(-15, c.DiasQueSobram);
    }

    [Fact]
    public void Com_todos_os_periodos_sem_saldo_nao_falta_periodo_mas_nao_cabe()
    {
        var todos = new List<FeriasParaSaldo>
        {
            new(D(2022, 3, 14), D(2023, 3, 13), D(2023, 4, 17), D(2023, 5, 16), 0, StatusDasFerias.Concluida, null, null),
            new(D(2023, 3, 14), D(2024, 3, 13), D(2024, 4, 15), D(2024, 5, 14), 0, StatusDasFerias.Concluida, null, null),
            new(D(2024, 3, 14), D(2025, 3, 13), D(2025, 4, 14), D(2025, 5, 13), 0, StatusDasFerias.Concluida, null, null),
            No2025(D(2026, 1, 5), D(2026, 2, 3), StatusDasFerias.Concluida),
        };

        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 25)), Contexto(todos));

        Assert.False(c.SemPeriodoAquisitivo);
        Assert.False(Regra(c, RegraDeFerias.CabeNoSaldo).Atendida);
        Assert.Equal("Passa do saldo disponível: faltam 10 dias.", Regra(c, RegraDeFerias.CabeNoSaldo).Mensagem);
        Assert.Equal(new DateOnly(2022, 3, 14), c.Periodo!.Inicio);
    }

    [Fact]
    public void O_periodo_fixo_do_aceite_e_da_sugestao_vale_mesmo_que_um_mais_antigo_tenha_saldo()
    {
        var ferias = new List<FeriasParaSaldo> { new(D(2022, 3, 14), D(2023, 3, 13), D(2023, 4, 17), D(2023, 5, 6), 0, StatusDasFerias.Concluida, null, null) };

        var solto = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 24)), Contexto(ferias, modo: ModoDaConferencia.AceiteDaSugestao));
        var fixo = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 24)), Contexto(ferias, modo: ModoDaConferencia.AceiteDaSugestao, periodoFixo: D(2024, 3, 14)));

        Assert.Equal(D(2022, 3, 14), solto.Periodo!.Inicio);
        Assert.Equal(D(2024, 3, 14), fixo.Periodo!.Inicio);
    }

    [Fact]
    public void Periodo_fixo_que_nao_existe_mais_vale_como_sem_periodo()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 24)), Contexto([], modo: ModoDaConferencia.AceiteDaSugestao, periodoFixo: D(2001, 1, 1)));

        Assert.True(c.SemPeriodoAquisitivo);
    }
}
