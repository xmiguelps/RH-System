using RHSystem.Application.Ferias.Regras;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>Os períodos aquisitivos (um por ano de casa, contados da admissão): o exemplo do Bruno do contrato, a admissão em 29/02, menos de um ano, pessoa desligada e as bordas de "fechado".</summary>
public class PeriodosAquisitivosTests
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    [Fact]
    public void O_exemplo_do_Bruno_admitido_em_14_de_marco_de_2022_tem_cinco_periodos_e_o_ultimo_em_andamento()
    {
        var periodos = PeriodosAquisitivos.Calcular(D(2022, 3, 14), D(2026, 10, 7), null);

        Assert.Equal(
            [(D(2022, 3, 14), D(2023, 3, 13)), (D(2023, 3, 14), D(2024, 3, 13)), (D(2024, 3, 14), D(2025, 3, 13)), (D(2025, 3, 14), D(2026, 3, 13)), (D(2026, 3, 14), D(2027, 3, 13))],
            periodos.Select(p => (p.Inicio, p.Fim)));
        Assert.Equal(
            [D(2024, 3, 13), D(2025, 3, 13), D(2026, 3, 13), D(2027, 3, 13), D(2028, 3, 13)],
            periodos.Select(p => p.PrazoParaTirar));
    }

    [Fact]
    public void Com_menos_de_um_ano_de_casa_so_existe_o_periodo_em_andamento()
    {
        var periodos = PeriodosAquisitivos.Calcular(D(2026, 3, 14), D(2026, 10, 7), null);

        var unico = Assert.Single(periodos);
        Assert.Equal((D(2026, 3, 14), D(2027, 3, 13)), (unico.Inicio, unico.Fim));
    }

    [Fact]
    public void Admissao_no_futuro_ainda_nao_tem_periodo_e_admissao_hoje_tem_o_primeiro()
    {
        Assert.Empty(PeriodosAquisitivos.Calcular(D(2026, 10, 8), D(2026, 10, 7), null));
        Assert.Single(PeriodosAquisitivos.Calcular(D(2026, 10, 7), D(2026, 10, 7), null));
    }

    [Fact]
    public void O_novo_periodo_comeca_no_dia_seguinte_ao_fim_do_anterior()
    {
        var no_ultimo_dia = PeriodosAquisitivos.Calcular(D(2022, 3, 14), D(2027, 3, 13), null);
        var no_dia_seguinte = PeriodosAquisitivos.Calcular(D(2022, 3, 14), D(2027, 3, 14), null);

        Assert.Equal(5, no_ultimo_dia.Count);
        Assert.Equal(6, no_dia_seguinte.Count);
        Assert.Equal((D(2027, 3, 14), D(2028, 3, 13)), (no_dia_seguinte[5].Inicio, no_dia_seguinte[5].Fim));
    }

    [Fact]
    public void Admissao_em_29_de_fevereiro_conta_cada_ano_da_admissao_sem_buraco_nem_sobreposicao()
    {
        var periodos = PeriodosAquisitivos.Calcular(D(2024, 2, 29), D(2028, 3, 1), null);

        Assert.Equal(
            [(D(2024, 2, 29), D(2025, 2, 27)), (D(2025, 2, 28), D(2026, 2, 27)), (D(2026, 2, 28), D(2027, 2, 27)), (D(2027, 2, 28), D(2028, 2, 28)), (D(2028, 2, 29), D(2029, 2, 27))],
            periodos.Select(p => (p.Inicio, p.Fim)));
        for (var i = 1; i < periodos.Count; i++)
        {
            Assert.Equal(periodos[i - 1].Fim.AddDays(1), periodos[i].Inicio);
        }
    }

    [Fact]
    public void Pessoa_desligada_so_tem_os_periodos_que_fecharam_ate_o_desligamento()
    {
        var no_ultimo_dia_do_periodo = PeriodosAquisitivos.Calcular(D(2022, 3, 14), D(2026, 10, 7), D(2024, 3, 13));
        var um_dia_antes = PeriodosAquisitivos.Calcular(D(2022, 3, 14), D(2026, 10, 7), D(2024, 3, 12));
        var no_meio_de_um_periodo = PeriodosAquisitivos.Calcular(D(2022, 3, 14), D(2026, 10, 7), D(2024, 6, 1));

        Assert.Equal([D(2023, 3, 13), D(2024, 3, 13)], no_ultimo_dia_do_periodo.Select(p => p.Fim));
        Assert.Equal([D(2023, 3, 13)], um_dia_antes.Select(p => p.Fim));
        Assert.Equal([D(2023, 3, 13), D(2024, 3, 13)], no_meio_de_um_periodo.Select(p => p.Fim));
    }

    [Theory]
    [InlineData(2026, 3, 14, 0)] // o dia do início
    [InlineData(2026, 4, 13, 0)] // faltam 1 dia para o primeiro mês
    [InlineData(2026, 4, 14, 1)]
    [InlineData(2026, 10, 7, 6)] // o exemplo do contrato: "6 de 12 meses"
    [InlineData(2026, 10, 14, 7)]
    [InlineData(2027, 3, 13, 11)]
    public void Os_meses_trabalhados_contam_so_os_meses_completos_desde_o_inicio_do_periodo(int ano, int mes, int dia, int esperado) =>
        Assert.Equal(esperado, PeriodosAquisitivos.MesesTrabalhados(new PeriodoAquisitivo(D(2026, 3, 14), D(2027, 3, 13)), D(ano, mes, dia)));

    [Fact]
    public void O_periodo_so_esta_fechado_depois_do_ultimo_dia()
    {
        var periodo = new PeriodoAquisitivo(D(2025, 3, 14), D(2026, 3, 13));

        Assert.False(PeriodosAquisitivos.Fechado(periodo, D(2026, 3, 13)));
        Assert.True(PeriodosAquisitivos.Fechado(periodo, D(2026, 3, 14)));
    }
}
