namespace RHSystem.Application.Ponto.Regras;

/// <summary>
/// A situação de um dia no espelho (<c>PontoDiaSituacao</c> do contrato; o JSON usa <c>MAIÚSCULAS_COM_SUBLINHADO</c>). <see cref="Atraso"/> e <see cref="Normal"/> são dias com batida; <see cref="Folga"/>, <see cref="Feriado"/>, <see cref="Ferias"/> e
/// <see cref="FaltaCompensada"/> são dias SEM batida em que a falta não existe.
/// </summary>
public enum SituacaoDoDiaNoPonto
{
    Normal,
    Atraso,
    Incompleto,
    AjusteEmAnalise,
    EmAndamento,
    SemRegistro,
    Folga,
    Feriado,
    Ferias,
    FaltaCompensada,
}

/// <summary>Os fatos do dia de que a situação depende (a consulta os reúne; aqui só se decide).</summary>
public sealed record EntradaDaSituacaoDoDia(
    bool EhHoje,
    bool TemPedidoPendente,
    bool TemBatidas,
    bool SequenciaFechada,
    bool TemAtraso,
    bool EhFimDeSemana,
    bool EhFeriado,
    bool EstaDeFerias,
    bool TemAtestadoDeDiaInteiro);

/// <summary>
/// A precedência do contrato: (1) pedido pendente do dia é <c>AJUSTE_EM_ANALISE</c>, mesmo sem batida; (2) com batidas: hoje <c>EM_ANDAMENTO</c>, passado com a sequência quebrada <c>INCOMPLETO</c>, com atraso <c>ATRASO</c>, senão <c>NORMAL</c>;
/// (3) sem batida: <c>FOLGA</c> (sábado e domingo), <c>FERIADO</c>, <c>FERIAS</c>, <c>FALTA_COMPENSADA</c> (atestado validado de dia inteiro), senão <c>SEM_REGISTRO</c> (inclusive hoje, a qualquer hora).
/// </summary>
public static class SituacaoDoDiaNoEspelho
{
    public static SituacaoDoDiaNoPonto Calcular(EntradaDaSituacaoDoDia dia)
    {
        if (dia.TemPedidoPendente)
        {
            return SituacaoDoDiaNoPonto.AjusteEmAnalise;
        }

        if (dia.TemBatidas)
        {
            if (dia.EhHoje)
            {
                return SituacaoDoDiaNoPonto.EmAndamento;
            }

            if (!dia.SequenciaFechada)
            {
                return SituacaoDoDiaNoPonto.Incompleto;
            }

            return dia.TemAtraso ? SituacaoDoDiaNoPonto.Atraso : SituacaoDoDiaNoPonto.Normal;
        }

        if (dia.EhFimDeSemana)
        {
            return SituacaoDoDiaNoPonto.Folga;
        }

        if (dia.EhFeriado)
        {
            return SituacaoDoDiaNoPonto.Feriado;
        }

        if (dia.EstaDeFerias)
        {
            return SituacaoDoDiaNoPonto.Ferias;
        }

        return dia.TemAtestadoDeDiaInteiro ? SituacaoDoDiaNoPonto.FaltaCompensada : SituacaoDoDiaNoPonto.SemRegistro;
    }
}
