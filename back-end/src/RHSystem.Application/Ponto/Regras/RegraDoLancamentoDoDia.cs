using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>O que a consolidação faz com a linha do banco de horas de um dia.</summary>
public enum AcaoDoLancamento
{
    /// <summary>Não mexe: o dia ainda não se resolve (é hoje, a sequência está quebrada ou há pedido de ajuste em análise). A linha que já existia fica como está.</summary>
    Manter,

    /// <summary>Saldo zero (ou dia sem o que lançar): a linha do dia, se existe, é apagada.</summary>
    Apagar,

    /// <summary>Cria a linha do dia ou reescreve a que existe.</summary>
    Lancar,
}

/// <summary>A decisão sobre o dia: a ação e, quando é <see cref="AcaoDoLancamento.Lancar"/>, o tipo, os minutos inteiros e a observação.</summary>
public sealed record DecisaoDoLancamento(AcaoDoLancamento Acao, TipoDeLancamentoDoBanco? Tipo = null, int Minutos = 0, string? Observacao = null);

/// <summary>
/// O que lançar para um dia que já passou, a partir do dia como o espelho o calcula (a mesma conta, em minutos inteiros): saldo positivo é CRÉDITO "Hora extra"; saldo negativo é DÉBITO, "Falta" no dia útil sem batida e "Saldo negativo do dia" nos outros; saldo zero (ou
/// dia sem expediente e sem batida, férias, feriado e fim de semana sem batida, falta compensada) apaga a linha; <b>hoje, a sequência quebrada e o pedido em análise</b> não mexem em nada (a linha que já existia espera; quando o dia se resolve, é recalculado).
/// </summary>
public static class RegraDoLancamentoDoDia
{
    public const string HoraExtra = "Hora extra";
    public const string SaldoNegativo = "Saldo negativo do dia";
    public const string Falta = "Falta";

    public static DecisaoDoLancamento Decidir(DiaDoEspelho dia)
    {
        if (dia.EhHoje || dia.PedidoEmAnaliseId is not null || dia.Situacao == SituacaoDoDiaNoPonto.Incompleto)
        {
            return new DecisaoDoLancamento(AcaoDoLancamento.Manter);
        }

        var saldo = dia.SaldoMinutos ?? 0;
        if (saldo == 0)
        {
            return new DecisaoDoLancamento(AcaoDoLancamento.Apagar);
        }

        return saldo > 0
            ? new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Credito, saldo, HoraExtra)
            : new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Debito, -saldo, dia.Situacao == SituacaoDoDiaNoPonto.SemRegistro ? Falta : SaldoNegativo);
    }
}
