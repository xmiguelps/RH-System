using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Application.Tests.Comunicacao;

/// <summary>
/// A regra do próximo pagamento, pura (sem banco): o dia do mês da empresa (o último dia se o mês não tem o dia), antecipado para o dia útil anterior se cai em fim de semana ou feriado; a exceção do mês vale como está; de hoje em diante (se hoje é o dia, é hoje).
/// O calendário de 2026 dos exemplos: novembro/2026 começa num domingo (06/11 é sexta), 05/09/2026 é sábado, 12/10/2026 é segunda.
/// </summary>
public class RegraDoPagamentoTests
{
    private static readonly Func<DateOnly, bool> SemFeriados = _ => false;

    private static Func<DateOnly, bool> Feriados(params DateOnly[] dias) => dias.Contains;

    // ---------------------------------------------------------------- o pagamento de um mês

    [Fact]
    public void O_dia_da_regra_num_dia_util_vale_como_esta()
    {
        var pagamento = RegraDoPagamento.DoMes(2026, 11, 6, null, SemFeriados)!;

        Assert.Equal((2026, 11, new DateOnly(2026, 11, 6), OrigemDoPagamento.Regra), (pagamento.Ano, pagamento.Mes, pagamento.Data, pagamento.Origem));
        Assert.Empty(pagamento.Ajustes);
    }

    [Fact]
    public void No_sabado_antecipa_para_a_sexta_e_diz_que_foi_o_fim_de_semana()
    {
        var pagamento = RegraDoPagamento.DoMes(2026, 9, 5, null, SemFeriados)!; // exemplo do contrato: 05/09/2026 é sábado

        Assert.Equal(new DateOnly(2026, 9, 4), pagamento.Data);
        Assert.Equal([AjusteDoPagamento.AntecipadoFimDeSemana], pagamento.Ajustes);
    }

    [Fact]
    public void No_domingo_antecipa_para_a_sexta_mesmo_que_ela_seja_do_mes_anterior_e_o_mes_continua_sendo_o_da_regra()
    {
        var pagamento = RegraDoPagamento.DoMes(2026, 11, 1, null, SemFeriados)!; // 01/11/2026 é domingo: a sexta é 30/10

        Assert.Equal((2026, 11, new DateOnly(2026, 10, 30)), (pagamento.Ano, pagamento.Mes, pagamento.Data));
        Assert.Equal([AjusteDoPagamento.AntecipadoFimDeSemana], pagamento.Ajustes);
    }

    [Fact]
    public void No_feriado_antecipa_para_o_dia_util_anterior_pulando_fim_de_semana_e_outros_feriados()
    {
        var feriados = Feriados(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 8));

        var pagamento = RegraDoPagamento.DoMes(2026, 10, 12, null, feriados)!; // segunda feriada; antes dela domingo, sábado, e a quinta e a sexta também feriadas

        Assert.Equal(new DateOnly(2026, 10, 7), pagamento.Data);
        Assert.Equal([AjusteDoPagamento.AntecipadoFeriado], pagamento.Ajustes); // o motivo é o da data da regra
    }

    [Fact]
    public void Sabado_que_tambem_e_feriado_conta_como_fim_de_semana()
    {
        var pagamento = RegraDoPagamento.DoMes(2026, 9, 5, null, Feriados(new DateOnly(2026, 9, 5)))!;

        Assert.Equal([AjusteDoPagamento.AntecipadoFimDeSemana], pagamento.Ajustes);
        Assert.Equal(new DateOnly(2026, 9, 4), pagamento.Data);
    }

    [Fact]
    public void Mes_sem_o_dia_usa_o_ultimo_dia_e_o_ajuste_se_soma_ao_da_antecipacao()
    {
        var novembro = RegraDoPagamento.DoMes(2026, 11, 31, null, SemFeriados)!; // 30/11/2026 é segunda
        var fevereiro = RegraDoPagamento.DoMes(2027, 2, 30, null, SemFeriados)!; // 28/02/2027 é domingo: a sexta é 26/02
        var bissexto = RegraDoPagamento.DoMes(2028, 2, 30, null, SemFeriados)!; // 29/02/2028 é terça

        Assert.Equal(new DateOnly(2026, 11, 30), novembro.Data);
        Assert.Equal([AjusteDoPagamento.UltimoDiaDoMes], novembro.Ajustes);
        Assert.Equal(new DateOnly(2027, 2, 26), fevereiro.Data);
        Assert.Equal([AjusteDoPagamento.UltimoDiaDoMes, AjusteDoPagamento.AntecipadoFimDeSemana], fevereiro.Ajustes);
        Assert.Equal(new DateOnly(2028, 2, 29), bissexto.Data);
        Assert.Equal([AjusteDoPagamento.UltimoDiaDoMes], bissexto.Ajustes);
    }

    [Fact]
    public void O_dia_que_e_exatamente_o_ultimo_do_mes_nao_leva_o_ajuste_de_ultimo_dia()
    {
        var pagamento = RegraDoPagamento.DoMes(2026, 11, 30, null, SemFeriados)!; // novembro tem 30 dias e 30/11/2026 é segunda

        Assert.Equal(new DateOnly(2026, 11, 30), pagamento.Data);
        Assert.Empty(pagamento.Ajustes);
    }

    [Fact]
    public void A_excecao_do_mes_vale_como_esta_sem_antecipar_por_fim_de_semana_nem_feriado_e_sem_ajustes()
    {
        var sabado = RegraDoPagamento.DoMes(2026, 11, 6, new DateOnly(2026, 11, 7), SemFeriados)!;
        var feriado = RegraDoPagamento.DoMes(2026, 10, 6, new DateOnly(2026, 10, 12), Feriados(new DateOnly(2026, 10, 12)))!;

        Assert.Equal((new DateOnly(2026, 11, 7), OrigemDoPagamento.Excecao), (sabado.Data, sabado.Origem));
        Assert.Empty(sabado.Ajustes);
        Assert.Equal((new DateOnly(2026, 10, 12), OrigemDoPagamento.Excecao), (feriado.Data, feriado.Origem));
    }

    [Fact]
    public void Sem_dia_e_sem_excecao_nao_ha_pagamento_e_a_excecao_vale_mesmo_sem_o_dia_da_regra()
    {
        Assert.Null(RegraDoPagamento.DoMes(2026, 11, null, null, SemFeriados));
        Assert.Equal(new DateOnly(2026, 11, 20), RegraDoPagamento.DoMes(2026, 11, null, new DateOnly(2026, 11, 20), SemFeriados)!.Data);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Dia_de_pagamento_fora_de_1_a_31_e_erro_de_programacao(int dia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RegraDoPagamento.DoMes(2026, 11, dia, null, SemFeriados));
    }

    [Theory]
    [InlineData(2026, 11, 2026, 10)]
    [InlineData(2027, 1, 2026, 12)]
    [InlineData(2026, 2, 2026, 1)]
    public void A_competencia_do_holerite_e_o_mes_anterior_ao_do_pagamento(int ano, int mes, int anoEsperado, int mesEsperado)
    {
        var pagamento = new PagamentoDoMes(ano, mes, new DateOnly(ano, mes, 6), OrigemDoPagamento.Regra, []);

        Assert.Equal((anoEsperado, mesEsperado), pagamento.Competencia);
    }

    // ---------------------------------------------------------------- o próximo pagamento

    private static DateOnly? SemExcecoes(int ano, int mes) => null;

    [Fact]
    public void Se_hoje_e_o_dia_do_pagamento_o_proximo_e_hoje_e_se_ja_passou_e_o_do_mes_seguinte()
    {
        var hoje = RegraDoPagamento.Proximo(new DateOnly(2026, 11, 6), 6, SemExcecoes, SemFeriados)!;
        var antes = RegraDoPagamento.Proximo(new DateOnly(2026, 11, 5), 6, SemExcecoes, SemFeriados)!;
        var depois = RegraDoPagamento.Proximo(new DateOnly(2026, 11, 7), 6, SemExcecoes, SemFeriados)!; // dezembro: 06/12 é domingo, a sexta é 04/12

        Assert.Equal((new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 6)), (hoje.Data, antes.Data));
        Assert.Equal((new DateOnly(2026, 12, 4), 12), (depois.Data, depois.Mes));
        Assert.Equal([AjusteDoPagamento.AntecipadoFimDeSemana], depois.Ajustes);
    }

    [Fact]
    public void A_excecao_do_mes_ainda_por_vir_vence_a_regra_e_depois_dela_volta_a_regra()
    {
        DateOnly? Excecoes(int ano, int mes) => ano == 2026 && mes == 11 ? new DateOnly(2026, 11, 20) : null;

        var antes = RegraDoPagamento.Proximo(new DateOnly(2026, 11, 10), 6, Excecoes, SemFeriados)!; // a regra (06/11) já passou, mas a exceção (20/11) ainda não
        var depois = RegraDoPagamento.Proximo(new DateOnly(2026, 11, 21), 6, Excecoes, SemFeriados)!;

        Assert.Equal((new DateOnly(2026, 11, 20), OrigemDoPagamento.Excecao), (antes.Data, antes.Origem));
        Assert.Equal((new DateOnly(2026, 12, 4), OrigemDoPagamento.Regra), (depois.Data, depois.Origem));
    }

    [Fact]
    public void O_pagamento_antecipado_para_o_mes_anterior_conta_e_o_que_ja_passou_dele_pula_para_o_seguinte()
    {
        var noDia = RegraDoPagamento.Proximo(new DateOnly(2026, 10, 30), 1, SemExcecoes, SemFeriados)!; // o de novembro (dia 1, domingo) sai na sexta 30/10
        var depois = RegraDoPagamento.Proximo(new DateOnly(2026, 10, 31), 1, SemExcecoes, SemFeriados)!; // sábado: passou; o de dezembro é 01/12 (terça)

        Assert.Equal((new DateOnly(2026, 10, 30), 11), (noDia.Data, noDia.Mes));
        Assert.Equal((new DateOnly(2026, 12, 1), 12), (depois.Data, depois.Mes));
    }

    [Fact]
    public void A_virada_do_ano_leva_o_pagamento_e_a_competencia_para_o_ano_seguinte()
    {
        var pagamento = RegraDoPagamento.Proximo(new DateOnly(2026, 12, 31), 5, SemExcecoes, SemFeriados)!; // 05/01/2027 é terça

        Assert.Equal((new DateOnly(2027, 1, 5), 2027, 1, (2026, 12)), (pagamento.Data, pagamento.Ano, pagamento.Mes, pagamento.Competencia));
    }

    [Fact]
    public void Sem_dia_nem_excecao_nao_ha_proximo_pagamento_e_so_excecoes_futuras_bastam()
    {
        DateOnly? Futura(int ano, int mes) => ano == 2027 && mes == 3 ? new DateOnly(2027, 3, 12) : null;

        Assert.Null(RegraDoPagamento.Proximo(new DateOnly(2026, 11, 7), null, SemExcecoes, SemFeriados));
        Assert.Equal(new DateOnly(2027, 3, 12), RegraDoPagamento.Proximo(new DateOnly(2026, 11, 7), null, Futura, SemFeriados)!.Data);
    }
}
