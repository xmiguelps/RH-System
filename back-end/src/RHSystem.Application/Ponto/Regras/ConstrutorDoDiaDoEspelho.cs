using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>Os dias da semana do contrato (<c>PontoDiaDaSemana</c>): o JSON usa <c>SEGUNDA</c>, <c>TERCA</c>... <c>DOMINGO</c>.</summary>
public enum DiaDaSemanaDoPonto
{
    Segunda,
    Terca,
    Quarta,
    Quinta,
    Sexta,
    Sabado,
    Domingo,
}

/// <summary>
/// Os fatos de UM dia de UMA pessoa: as batidas do dia do ponto, o feriado que vale para a empresa, as férias e o atestado validado que cobrem o dia (as horas cobertas por atestado de horas somadas), o pedido pendente, a carga do dia e o horário
/// padrão da empresa (<c>null</c> sem empresa configurada: sem horário não há atraso).
/// </summary>
public sealed record DadosDoDiaDoEspelho(
    DateOnly Dia,
    DateOnly Hoje,
    IReadOnlyList<BatidaLida> Batidas,
    string? NomeFeriado,
    bool EstaDeFerias,
    bool TemAtestadoDeDiaInteiro,
    int MinutosDeAtestadoDeHoras,
    int? PedidoPendenteId,
    int CargaDiariaMinutos,
    TimeOnly? HorarioPadrao,
    int ToleranciaMinutos);

/// <summary>Um dia do espelho já calculado (os campos de <c>PontoEspelhoDia</c> do contrato, mais <see cref="PodePedirAjuste"/>, que só a própria pessoa vê).</summary>
public sealed record DiaDoEspelho(
    DateOnly Data,
    DiaDaSemanaDoPonto DiaDaSemana,
    bool EhHoje,
    SituacaoDoDiaNoPonto Situacao,
    string? NomeFeriado,
    IReadOnlyList<PontoPeriodo> Periodos,
    int? HorasMinutos,
    int CargaMinutos,
    int? SaldoMinutos,
    int? AtrasoMinutos,
    int? PedidoEmAnaliseId,
    bool PodePedirAjuste);

/// <summary>
/// O dia do espelho, sem banco. <b>Dia com expediente</b> é segunda a sexta, sem feriado, sem férias e sem atestado de dia inteiro: só nele há carga, atraso e falta. <b>Horas</b> (minutos, segundos descartados por batida): <c>null</c> hoje, no dia de sequência
/// quebrada e no dia sem batida e sem expediente; <c>0</c> no dia passado com expediente e sem batida (a falta); senão a soma dos períodos fechados. A carga é a do dia menos as horas de atestado de horas, sem passar de zero; o saldo é horas menos carga.
/// </summary>
public static class ConstrutorDoDiaDoEspelho
{
    public static DiaDoEspelho Construir(DadosDoDiaDoEspelho d)
    {
        var ehHoje = d.Dia == d.Hoje;
        var fimDeSemana = d.Dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var comExpediente = !fimDeSemana && d.NomeFeriado is null && !d.EstaDeFerias && !d.TemAtestadoDeDiaInteiro;
        var temBatidas = d.Batidas.Count > 0;
        var marcacoes = d.Batidas.Select(b => new MarcacaoDoDia(b.Id, b.InstanteUtc, b.Tipo)).ToList();
        var consistente = temBatidas && SequenciaDoDia.EhConsistente(marcacoes);

        int? horas;
        if (ehHoje)
        {
            horas = null;
        }
        else if (temBatidas)
        {
            horas = consistente ? CalculoDoDia.HorasEmMinutos(SequenciaDoDia.Periodos(marcacoes)) : null;
        }
        else
        {
            horas = comExpediente ? 0 : null;
        }

        var carga = comExpediente ? Math.Max(0, d.CargaDiariaMinutos - d.MinutosDeAtestadoDeHoras) : 0;
        var atraso = comExpediente && temBatidas ? CalculoDoDia.Atraso(PrimeiraEntradaLocal(d.Batidas), d.HorarioPadrao, d.ToleranciaMinutos) : null;

        var situacao = SituacaoDoDiaNoEspelho.Calcular(new EntradaDaSituacaoDoDia(
            EhHoje: ehHoje,
            TemPedidoPendente: d.PedidoPendenteId is not null,
            TemBatidas: temBatidas,
            SequenciaFechada: consistente,
            TemAtraso: atraso is not null,
            EhFimDeSemana: fimDeSemana,
            EhFeriado: d.NomeFeriado is not null,
            EstaDeFerias: d.EstaDeFerias,
            TemAtestadoDeDiaInteiro: d.TemAtestadoDeDiaInteiro));

        return new DiaDoEspelho(
            d.Dia,
            DiaDaSemana(d.Dia),
            ehHoje,
            situacao,
            d.NomeFeriado,
            PeriodosDoDia.De(d.Batidas),
            horas,
            carga,
            CalculoDoDia.Saldo(horas, carga),
            atraso,
            d.PedidoPendenteId,
            d.Dia < d.Hoje && d.PedidoPendenteId is null && (comExpediente || temBatidas));
    }

    private static TimeOnly? PrimeiraEntradaLocal(IReadOnlyList<BatidaLida> batidas)
    {
        var primeira = batidas.Where(b => b.Tipo == TipoDeMarcacao.Entrada).OrderBy(b => b.InstanteUtc).ThenBy(b => b.Id).FirstOrDefault();
        return primeira is null ? null : TimeOnly.FromDateTime(FusoBrasil.ParaLocal(primeira.InstanteUtc));
    }

    private static DiaDaSemanaDoPonto DiaDaSemana(DateOnly dia) => dia.DayOfWeek switch
    {
        DayOfWeek.Monday => DiaDaSemanaDoPonto.Segunda,
        DayOfWeek.Tuesday => DiaDaSemanaDoPonto.Terca,
        DayOfWeek.Wednesday => DiaDaSemanaDoPonto.Quarta,
        DayOfWeek.Thursday => DiaDaSemanaDoPonto.Quinta,
        DayOfWeek.Friday => DiaDaSemanaDoPonto.Sexta,
        DayOfWeek.Saturday => DiaDaSemanaDoPonto.Sabado,
        _ => DiaDaSemanaDoPonto.Domingo,
    };
}
