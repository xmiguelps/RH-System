using RHSystem.Application.Comum.Tempo;

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>
/// O prazo que o RH tem para responder um pedido de benefício (<c>respostaPrevistaAte</c>): 5 dias úteis depois do pedido, sem contar sábado, domingo nem os feriados do calendário da empresa. A conta parte do DIA do pedido em -03:00 (um pedido às 02:30Z ainda é do dia
/// anterior) e conta a partir do dia seguinte, ainda que o pedido tenha sido feito num dia sem expediente.
/// </summary>
public static class RespostaPrevista
{
    public const int DiasUteis = 5;

    /// <summary>Quantos dias à frente do pedido o calendário precisa cobrir: três semanas, ou 15 dias úteis, aguentam até dez feriados de semana no caminho.</summary>
    public const int DiasDaJanela = 21;

    /// <summary>De que dia a que dia os feriados precisam ser lidos para <see cref="De"/> acertar (inclusive nas duas pontas).</summary>
    public static (DateOnly De, DateOnly Ate) JanelaDeFeriados(DateTime pedidoEmUtc)
    {
        var dia = FusoBrasil.DiaLocalDe(pedidoEmUtc);
        return (dia, dia.AddDays(DiasDaJanela));
    }

    /// <param name="feriados">Os feriados da empresa na <see cref="JanelaDeFeriados"/> do pedido.</param>
    public static DateOnly De(DateTime pedidoEmUtc, IReadOnlySet<DateOnly> feriados)
    {
        var dia = FusoBrasil.DiaLocalDe(pedidoEmUtc);
        var contados = 0;
        while (contados < DiasUteis)
        {
            dia = dia.AddDays(1);
            if (dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !feriados.Contains(dia))
            {
                contados++;
            }
        }

        return dia;
    }
}
