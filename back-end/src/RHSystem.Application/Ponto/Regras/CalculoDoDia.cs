namespace RHSystem.Application.Ponto.Regras;

/// <summary>
/// As contas do dia, em MINUTOS inteiros (nunca as funções de horas do banco: duas casas de hora erram até 0,3 minuto por dia). Os SEGUNDOS são descartados de cada batida antes da conta, porque a tela mostra <c>HH:mm</c>:
/// 09:00:50 a 09:10:10 vale os 10 minutos que a pessoa vê, não os 9 minutos e 20 segundos do relógio.
/// </summary>
public static class CalculoDoDia
{
    /// <summary>A soma dos períodos FECHADOS (entrada sem saída e saída solta não contam).</summary>
    public static int HorasEmMinutos(IEnumerable<PeriodoDoDia> periodos) =>
        periodos.Where(p => p.Fechado).Sum(p => (int)(AoMinuto(p.Saida!.InstanteUtc) - AoMinuto(p.Entrada!.InstanteUtc)).TotalMinutes);

    /// <summary>
    /// As horas do dia ATÉ AGORA (a tela da equipe): os períodos fechados e, <b>hoje</b>, o período aberto (a ENTRADA sem saída que é a última do dia) contado até o instante, também sem segundos. Em dia passado só os períodos fechados.
    /// </summary>
    public static int HorasAteAgoraEmMinutos(IReadOnlyList<PeriodoDoDia> periodos, DateTime agoraUtc, bool hoje)
    {
        var total = HorasEmMinutos(periodos);
        if (hoje && periodos.Count > 0 && periodos[^1] is { Entrada: { } entrada, Saida: null })
        {
            total += Math.Max(0, (int)(AoMinuto(agoraUtc) - AoMinuto(entrada.InstanteUtc)).TotalMinutes);
        }

        return total;
    }

    /// <summary>
    /// O atraso: a primeira entrada (no horário local, sem segundos) menos o horário padrão da empresa, <b>só se passa da tolerância</b>, e então conta inteiro desde o horário (08:14 com horário 08:00 e tolerância 10 são 14 minutos;
    /// 08:10 não é atraso). <c>null</c> sem batida, sem horário padrão ou dentro da tolerância.
    /// </summary>
    public static int? Atraso(TimeOnly? primeiraEntradaLocal, TimeOnly? horarioPadrao, int toleranciaMinutos)
    {
        if (primeiraEntradaLocal is null || horarioPadrao is null)
        {
            return null;
        }

        // ToTimeSpan: a subtração de TimeOnly dá a volta na meia-noite (07:55 - 08:00 seria 23:55). O (int) descarta os segundos (08:10:50 vale 10 minutos).
        var minutos = (int)(primeiraEntradaLocal.Value.ToTimeSpan() - horarioPadrao.Value.ToTimeSpan()).TotalMinutes;
        return minutos > toleranciaMinutos ? minutos : null;
    }

    /// <summary>O saldo do dia (horas menos a carga); <c>null</c> se as horas não existem (hoje, sequência quebrada, dia sem expediente e sem batida).</summary>
    public static int? Saldo(int? horasMinutos, int cargaMinutos) => horasMinutos - cargaMinutos;

    private static DateTime AoMinuto(DateTime instante) => new(instante.Ticks - (instante.Ticks % TimeSpan.TicksPerMinute), instante.Kind);
}
