using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Tests.Ponto;

public class CalculoDoDiaTests
{
    private static readonly DateTime Dia = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);

    private static PeriodoDoDia Periodo(DateTime entrada, DateTime? saida) =>
        new(new MarcacaoDoDia(1, entrada, TipoDeMarcacao.Entrada), saida is null ? null : new MarcacaoDoDia(2, saida.Value, TipoDeMarcacao.Saida));

    // ---------------------------------------------------------------- horas

    [Fact]
    public void Soma_so_os_periodos_fechados()
    {
        var periodos = new[]
        {
            Periodo(Dia.AddHours(8), Dia.AddHours(12)),
            Periodo(Dia.AddHours(13), Dia.AddHours(17).AddMinutes(30)),
            Periodo(Dia.AddHours(18), null), // aberto: não conta
        };

        Assert.Equal(510, CalculoDoDia.HorasEmMinutos(periodos));
    }

    [Fact]
    public void Os_segundos_de_cada_batida_sao_descartados_antes_da_conta()
    {
        // 09:00:50 até 09:10:10 são 9 min 20 s de relógio, mas a pessoa vê 09:00 e 09:10: valem 10 minutos.
        var periodos = new[] { Periodo(Dia.AddHours(9).AddSeconds(50), Dia.AddHours(9).AddMinutes(10).AddSeconds(10)) };

        Assert.Equal(10, CalculoDoDia.HorasEmMinutos(periodos));
    }

    [Fact]
    public void Periodo_que_atravessa_a_meia_noite_conta_inteiro()
    {
        var periodos = new[] { Periodo(Dia.AddHours(22), Dia.AddDays(1).AddHours(6)) };

        Assert.Equal(480, CalculoDoDia.HorasEmMinutos(periodos));
    }

    [Fact]
    public void Sem_periodo_fechado_as_horas_sao_zero() =>
        Assert.Equal(0, CalculoDoDia.HorasEmMinutos([Periodo(Dia.AddHours(8), null)]));

    [Fact]
    public void Saida_solta_nao_conta()
    {
        var solta = new PeriodoDoDia(null, new MarcacaoDoDia(2, Dia.AddHours(8), TipoDeMarcacao.Saida));

        Assert.Equal(0, CalculoDoDia.HorasEmMinutos([solta]));
    }

    // ---------------------------------------------------------------- atraso

    [Theory]
    [InlineData("08:00", 10, "08:00", null)]
    [InlineData("08:00", 10, "07:55", null)]
    [InlineData("08:00", 10, "08:10", null)]
    [InlineData("08:00", 10, "08:11", 11)]
    [InlineData("08:00", 10, "08:14", 14)]
    [InlineData("08:00", 0, "08:01", 1)]
    [InlineData("08:00", 0, "08:00", null)]
    [InlineData("08:00", 15, "09:30", 90)]
    public void O_atraso_so_existe_se_passa_da_tolerancia_e_conta_desde_o_horario_padrao(string horario, int tolerancia, string entrada, int? esperado) =>
        Assert.Equal(esperado, CalculoDoDia.Atraso(TimeOnly.Parse(entrada), TimeOnly.Parse(horario), tolerancia));

    [Fact]
    public void Sem_horario_padrao_nao_ha_atraso() =>
        Assert.Null(CalculoDoDia.Atraso(new TimeOnly(9, 0), null, 10));

    [Fact]
    public void Sem_batida_nao_ha_atraso() =>
        Assert.Null(CalculoDoDia.Atraso(null, new TimeOnly(8, 0), 10));

    [Fact]
    public void Os_segundos_da_entrada_nao_criam_atraso_na_tolerancia()
    {
        // 08:10:59 é 08:10 para quem olha o relógio da tela: dentro da tolerância de 10 minutos.
        Assert.Null(CalculoDoDia.Atraso(new TimeOnly(8, 10, 59), new TimeOnly(8, 0), 10));
        Assert.Equal(11, CalculoDoDia.Atraso(new TimeOnly(8, 11, 0), new TimeOnly(8, 0), 10));
    }

    // ---------------------------------------------------------------- saldo

    [Theory]
    [InlineData(480, 480, 0)]
    [InlineData(540, 480, 60)]
    [InlineData(450, 480, -30)]
    [InlineData(0, 0, 0)]
    [InlineData(120, 0, 120)]
    public void O_saldo_e_horas_menos_carga(int horas, int carga, int esperado) =>
        Assert.Equal(esperado, CalculoDoDia.Saldo(horas, carga));

    [Fact]
    public void Sem_horas_nao_ha_saldo() => Assert.Null(CalculoDoDia.Saldo(null, 480));
}
