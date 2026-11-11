using RHSystem.Application.Comum.Tempo;

namespace RHSystem.Application.Tests.Comum;

public class FusoBrasilTests
{
    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto, int segundo = 0) =>
        new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);

    [Fact]
    public void ParaLocal_subtrai_tres_horas_e_ParaUtc_soma_tres_horas()
    {
        var utc = Utc(2026, 10, 7, 12, 42);

        var local = FusoBrasil.ParaLocal(utc);

        Assert.Equal(new DateTime(2026, 10, 7, 9, 42, 0), local);
        Assert.Equal(DateTimeKind.Unspecified, local.Kind);
        var deVolta = FusoBrasil.ParaUtc(local);
        Assert.Equal(utc, deVolta);
        Assert.Equal(DateTimeKind.Utc, deVolta.Kind);
    }

    [Fact]
    public void Hoje_as_02h30_UTC_ainda_e_o_dia_anterior_no_Brasil()
    {
        // A virada do dia (Review Focus 3): 02:30Z é 23:30 do dia anterior em -03:00.
        Assert.Equal(new DateOnly(2026, 10, 7), FusoBrasil.Hoje(Utc(2026, 10, 8, 2, 30)));
    }

    [Fact]
    public void Hoje_as_03h00_UTC_ja_e_o_dia_novo()
    {
        Assert.Equal(new DateOnly(2026, 10, 8), FusoBrasil.Hoje(Utc(2026, 10, 8, 3, 0)));
    }

    [Fact]
    public void InicioDoDiaEmUtc_de_2026_10_07_e_2026_10_07T03_00Z()
    {
        var inicio = FusoBrasil.InicioDoDiaEmUtc(new DateOnly(2026, 10, 7));

        Assert.Equal(Utc(2026, 10, 7, 3, 0), inicio);
        Assert.Equal(DateTimeKind.Utc, inicio.Kind);
    }

    [Fact]
    public void Intervalo_de_dias_inclusivo_vira_intervalo_de_instantes_semiaberto()
    {
        // [de, ate] inclusivo = InicioDoDiaEmUtc(de) .. InicioDoDiaEmUtc(ate + 1 dia), este exclusivo.
        var de = FusoBrasil.InicioDoDiaEmUtc(new DateOnly(2026, 10, 7));
        var ateExclusivo = FusoBrasil.InicioDoDiaEmUtc(new DateOnly(2026, 10, 7).AddDays(1));

        var ultimoInstanteDoDia = Utc(2026, 10, 8, 2, 59, 59).AddTicks(9_999_999);
        var primeiroInstanteDoDiaSeguinte = Utc(2026, 10, 8, 3, 0);

        Assert.True(ultimoInstanteDoDia >= de && ultimoInstanteDoDia < ateExclusivo);
        Assert.False(primeiroInstanteDoDiaSeguinte < ateExclusivo);
        Assert.Equal(new DateOnly(2026, 10, 7), FusoBrasil.DiaLocalDe(ultimoInstanteDoDia));
        Assert.Equal(new DateOnly(2026, 10, 8), FusoBrasil.DiaLocalDe(primeiroInstanteDoDiaSeguinte));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void ParaLocal_com_Kind_diferente_de_Utc_lanca_ArgumentException(DateTimeKind tipo)
    {
        var semFuso = new DateTime(2026, 10, 7, 12, 42, 0, tipo);

        Assert.Throws<ArgumentException>(() => FusoBrasil.ParaLocal(semFuso));
    }

    [Fact]
    public void ParaUtc_com_Kind_Utc_lanca_ArgumentException()
    {
        // Um instante que já é UTC passado como se fosse local seria convertido duas vezes, sem erro nenhum.
        Assert.Throws<ArgumentException>(() => FusoBrasil.ParaUtc(Utc(2026, 10, 7, 12, 42)));
    }
}
