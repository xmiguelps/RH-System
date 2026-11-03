namespace RHSystem.Application.Comum.Tempo;

/// <summary>
/// O fuso do negócio: deslocamento fixo de -03:00 (o Brasil não tem horário de verão desde 2019). Instante é sempre UTC (<c>Kind = Utc</c>); o dia
/// de calendário (admissão, dia do ponto, feriado) é o dia em -03:00. A conversão é do back-end (decisão 4 do roadmap).
/// </summary>
public static class FusoBrasil
{
    private static readonly TimeSpan Deslocamento = TimeSpan.FromHours(-3);

    /// <summary>Instante UTC para o relógio de -03:00 (<c>Kind = Unspecified</c>, como o banco do grupo guarda).</summary>
    /// <exception cref="ArgumentException">O instante não tem <c>Kind = Utc</c>: seria impossível saber de que fuso ele veio.</exception>
    public static DateTime ParaLocal(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"O instante precisa ter Kind = Utc (veio {utc.Kind}).", nameof(utc));
        }

        return DateTime.SpecifyKind(utc + Deslocamento, DateTimeKind.Unspecified);
    }

    /// <summary>O inverso de <see cref="ParaLocal"/>: o relógio de -03:00 para o instante UTC (<c>Kind = Utc</c>).</summary>
    /// <exception cref="ArgumentException">O valor já é UTC: convertê-lo de novo erraria 3 horas sem aviso.</exception>
    public static DateTime ParaUtc(DateTime local)
    {
        if (local.Kind == DateTimeKind.Utc)
        {
            throw new ArgumentException("O valor já é UTC; ParaUtc recebe o relógio de -03:00 (Kind Unspecified).", nameof(local));
        }

        return DateTime.SpecifyKind(local - Deslocamento, DateTimeKind.Utc);
    }

    /// <summary>O dia de calendário em -03:00 no instante dado: às 02:30Z ainda é o dia anterior.</summary>
    public static DateOnly Hoje(DateTime agoraUtc) => DiaLocalDe(agoraUtc);

    public static DateOnly DiaLocalDe(DateTime instanteUtc) => DateOnly.FromDateTime(ParaLocal(instanteUtc));

    /// <summary>
    /// 00:00 de -03:00, em UTC (03:00Z). Um intervalo de dias <c>[de, ate]</c> inclusivo é, em instantes,
    /// <c>InicioDoDiaEmUtc(de)</c> até <c>InicioDoDiaEmUtc(ate.AddDays(1))</c> exclusivo.
    /// </summary>
    public static DateTime InicioDoDiaEmUtc(DateOnly dia) => ParaUtc(dia.ToDateTime(TimeOnly.MinValue));
}
