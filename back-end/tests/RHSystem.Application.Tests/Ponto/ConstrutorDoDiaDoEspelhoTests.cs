using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>
/// Um dia do espelho, sem banco. Os números são os do exemplo do contrato (outubro de 2026): carga 528 (44 h ÷ 5), horário padrão 08:00 com 10 minutos de tolerância, hoje = 2026-10-07 (quarta). Os instantes são UTC (Brasília = UTC − 3).
/// </summary>
public class ConstrutorDoDiaDoEspelhoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static int _id;

    private static BatidaLida E(DateOnly dia, int hora, int minuto) => new(++_id, new DateTime(dia.Year, dia.Month, dia.Day, hora, minuto, 0, DateTimeKind.Utc), TipoDeMarcacao.Entrada, OrigemDaMarcacao.Sistema, null);

    private static BatidaLida S(DateOnly dia, int hora, int minuto) => new(++_id, new DateTime(dia.Year, dia.Month, dia.Day, hora, minuto, 0, DateTimeKind.Utc), TipoDeMarcacao.Saida, OrigemDaMarcacao.Sistema, null);

    private static DadosDoDiaDoEspelho Dados(DateOnly dia, IReadOnlyList<BatidaLida>? batidas = null, string? feriado = null, bool ferias = false, bool atestadoInteiro = false, int atestadoHoras = 0, int? pedido = null, int carga = 528, TimeOnly? horario = null, int tolerancia = 10) =>
        new(dia, Hoje, batidas ?? [], feriado, ferias, atestadoInteiro, atestadoHoras, pedido, carga, horario ?? new TimeOnly(8, 0), tolerancia);

    private static readonly DateOnly Quinta1 = new(2026, 10, 1);
    private static readonly DateOnly Sexta2 = new(2026, 10, 2);
    private static readonly DateOnly Sabado3 = new(2026, 10, 3);
    private static readonly DateOnly Segunda5 = new(2026, 10, 5);

    [Fact]
    public void Dia_normal_do_contrato_tem_534_minutos_saldo_6_e_pode_pedir_ajuste()
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Quinta1, [E(Quinta1, 10, 56), S(Quinta1, 15, 1), E(Quinta1, 16, 0), S(Quinta1, 20, 49)]));

        Assert.Equal((SituacaoDoDiaNoPonto.Normal, 534, 528, 6, (int?)null, true, false), (dia.Situacao, dia.HorasMinutos, dia.CargaMinutos, dia.SaldoMinutos, dia.AtrasoMinutos, dia.PodePedirAjuste, dia.EhHoje));
        Assert.Equal((DiaDaSemanaDoPonto.Quinta, 2), (dia.DiaDaSemana, dia.Periodos.Count));
    }

    [Fact]
    public void Dia_com_pedido_em_analise_e_volta_sem_saida_tem_horas_e_saldo_nulos_e_nao_pode_pedir()
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Sexta2, [E(Sexta2, 11, 3), S(Sexta2, 15, 0), E(Sexta2, 16, 2)], pedido: 301));

        Assert.Equal((SituacaoDoDiaNoPonto.AjusteEmAnalise, (int?)null, 528, (int?)null, (int?)null, (int?)301, false), (dia.Situacao, dia.HorasMinutos, dia.CargaMinutos, dia.SaldoMinutos, dia.AtrasoMinutos, dia.PedidoEmAnaliseId, dia.PodePedirAjuste));
    }

    [Theory]
    [InlineData(5, DiaDaSemanaDoPonto.Segunda)]
    [InlineData(6, DiaDaSemanaDoPonto.Terca)]
    [InlineData(7, DiaDaSemanaDoPonto.Quarta)]
    [InlineData(8, DiaDaSemanaDoPonto.Quinta)]
    [InlineData(9, DiaDaSemanaDoPonto.Sexta)]
    [InlineData(10, DiaDaSemanaDoPonto.Sabado)]
    [InlineData(11, DiaDaSemanaDoPonto.Domingo)]
    public void O_dia_da_semana_e_o_da_data_nos_sete_dias(int dia, DiaDaSemanaDoPonto esperado)
    {
        var construido = ConstrutorDoDiaDoEspelho.Construir(Dados(new DateOnly(2026, 10, dia)));

        Assert.Equal(esperado, construido.DiaDaSemana);
    }

    [Fact]
    public void Sabado_sem_batida_e_folga_sem_carga_sem_horas_e_sem_botao()
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Sabado3));

        Assert.Equal((SituacaoDoDiaNoPonto.Folga, (int?)null, 0, (int?)null, false, DiaDaSemanaDoPonto.Sabado), (dia.Situacao, dia.HorasMinutos, dia.CargaMinutos, dia.SaldoMinutos, dia.PodePedirAjuste, dia.DiaDaSemana));
    }

    [Fact]
    public void Atraso_de_14_minutos_conta_inteiro_e_o_saldo_e_negativo()
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 11, 14), S(Segunda5, 15, 5), E(Segunda5, 16, 5), S(Segunda5, 21, 0)]));

        Assert.Equal((SituacaoDoDiaNoPonto.Atraso, 14, 526, -2), (dia.Situacao, dia.AtrasoMinutos, dia.HorasMinutos, dia.SaldoMinutos));
    }

    [Theory]
    [InlineData(10, null)] // 08:10: dentro da tolerância
    [InlineData(11, 11)]
    public void A_tolerancia_so_vale_estritamente_depois(int minuto, int? atraso)
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 11, minuto), S(Segunda5, 20, 0)]));

        Assert.Equal(atraso, dia.AtrasoMinutos);
    }

    [Fact]
    public void Sem_horario_padrao_nao_ha_atraso()
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 14, 0), S(Segunda5, 20, 0)]) with { HorarioPadrao = null });

        Assert.Equal((SituacaoDoDiaNoPonto.Normal, (int?)null), (dia.Situacao, dia.AtrasoMinutos));
    }

    [Fact]
    public void Hoje_com_batidas_e_em_andamento_e_hoje_sem_batida_e_sem_registro_e_as_horas_ficam_nulas()
    {
        var comBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(Hoje, [E(Hoje, 11, 0)]));
        var semBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(Hoje));

        Assert.Equal((SituacaoDoDiaNoPonto.EmAndamento, (int?)null, true, false), (comBatida.Situacao, comBatida.HorasMinutos, comBatida.EhHoje, comBatida.PodePedirAjuste));
        Assert.Equal((SituacaoDoDiaNoPonto.SemRegistro, (int?)null, 528, false), (semBatida.Situacao, semBatida.HorasMinutos, semBatida.CargaMinutos, semBatida.PodePedirAjuste));
    }

    [Fact]
    public void Dia_passado_util_sem_batida_e_sem_registro_com_zero_hora_e_saldo_devedor_e_pode_pedir_ajuste()
    {
        var segunda = new DateOnly(2026, 10, 5);

        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(segunda));

        Assert.Equal((SituacaoDoDiaNoPonto.SemRegistro, 0, 528, -528, true), (dia.Situacao, dia.HorasMinutos, dia.CargaMinutos, dia.SaldoMinutos, dia.PodePedirAjuste));
    }

    [Fact]
    public void Dia_passado_com_sequencia_quebrada_e_incompleto_com_horas_e_saldo_nulos_mas_pode_pedir_ajuste()
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 11, 0), S(Segunda5, 15, 0), E(Segunda5, 16, 0)]));

        Assert.Equal((SituacaoDoDiaNoPonto.Incompleto, (int?)null, (int?)null, true), (dia.Situacao, dia.HorasMinutos, dia.SaldoMinutos, dia.PodePedirAjuste));
    }

    [Fact]
    public void Feriado_sem_batida_e_feriado_e_com_batida_mantem_o_nome_sem_carga_e_a_hora_vira_saldo()
    {
        var segunda = new DateOnly(2026, 9, 7);

        var semBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(segunda, feriado: "Independencia do Brasil"));
        var comBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(segunda, [E(segunda, 12, 0), S(segunda, 16, 0)], feriado: "Independencia do Brasil"));

        Assert.Equal((SituacaoDoDiaNoPonto.Feriado, "Independencia do Brasil", (int?)null, 0, false), (semBatida.Situacao, semBatida.NomeFeriado, semBatida.HorasMinutos, semBatida.CargaMinutos, semBatida.PodePedirAjuste));
        Assert.Equal((SituacaoDoDiaNoPonto.Normal, "Independencia do Brasil", 240, 0, 240, (int?)null, true), (comBatida.Situacao, comBatida.NomeFeriado, comBatida.HorasMinutos, comBatida.CargaMinutos, comBatida.SaldoMinutos, comBatida.AtrasoMinutos, comBatida.PodePedirAjuste));
    }

    [Fact]
    public void Feriado_no_fim_de_semana_continua_folga_e_leva_o_nome()
    {
        var domingo = new DateOnly(2026, 9, 6);

        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(domingo, feriado: "Dia qualquer"));

        Assert.Equal((SituacaoDoDiaNoPonto.Folga, "Dia qualquer"), (dia.Situacao, dia.NomeFeriado));
    }

    [Fact]
    public void Ferias_sem_batida_sao_ferias_sem_carga_e_com_batida_a_carga_continua_zero()
    {
        var semBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, ferias: true));
        var comBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 11, 0), S(Segunda5, 15, 0)], ferias: true));

        Assert.Equal((SituacaoDoDiaNoPonto.Ferias, (int?)null, 0, false), (semBatida.Situacao, semBatida.HorasMinutos, semBatida.CargaMinutos, semBatida.PodePedirAjuste));
        Assert.Equal((SituacaoDoDiaNoPonto.Normal, 240, 0, 240, (int?)null), (comBatida.Situacao, comBatida.HorasMinutos, comBatida.CargaMinutos, comBatida.SaldoMinutos, comBatida.AtrasoMinutos));
    }

    [Fact]
    public void Atestado_de_dia_inteiro_e_falta_compensada_sem_carga_e_com_batida_as_horas_viram_credito()
    {
        var semBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, atestadoInteiro: true));
        var comBatida = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 11, 0), S(Segunda5, 15, 0)], atestadoInteiro: true));

        Assert.Equal((SituacaoDoDiaNoPonto.FaltaCompensada, (int?)null, 0), (semBatida.Situacao, semBatida.HorasMinutos, semBatida.CargaMinutos));
        Assert.Equal((SituacaoDoDiaNoPonto.Normal, 240, 0, 240), (comBatida.Situacao, comBatida.HorasMinutos, comBatida.CargaMinutos, comBatida.SaldoMinutos));
    }

    [Theory]
    [InlineData(120, 408)]
    [InlineData(528, 0)]
    [InlineData(600, 0)] // as horas cobertas ficam limitadas à carga do dia
    [InlineData(0, 528)]
    public void Atestado_de_horas_tira_da_carga_sem_passar_de_zero(int cobertas, int cargaEsperada)
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 11, 0), S(Segunda5, 20, 0)], atestadoHoras: cobertas));

        Assert.Equal(cargaEsperada, dia.CargaMinutos);
    }

    [Fact]
    public void Cargo_sem_carga_deixa_a_carga_zero_e_o_saldo_igual_as_horas()
    {
        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(Segunda5, [E(Segunda5, 11, 0), S(Segunda5, 15, 0)], carga: 0));

        Assert.Equal((240, 0, 240), (dia.HorasMinutos, dia.CargaMinutos, dia.SaldoMinutos));
    }

    [Fact]
    public void Folga_com_batida_pode_pedir_ajuste_e_os_periodos_levam_ordem_e_rotulo_da_quinta_batida_em_diante()
    {
        var domingo = new DateOnly(2026, 10, 4);

        var dia = ConstrutorDoDiaDoEspelho.Construir(Dados(domingo, [E(domingo, 11, 0), S(domingo, 12, 0), E(domingo, 13, 0), S(domingo, 14, 0), E(domingo, 15, 0)]));

        Assert.True(dia.PodePedirAjuste);
        Assert.Equal(["Entrada", "Saída almoço", "Volta", "Saída", "Entrada"], dia.Periodos.SelectMany(p => new[] { p.Entrada, p.Saida }).Where(m => m is not null).Select(m => m!.Rotulo));
        Assert.Equal([1, 2, 3, 4, 5], dia.Periodos.SelectMany(p => new[] { p.Entrada, p.Saida }).Where(m => m is not null).Select(m => m!.Ordem));
    }
}
