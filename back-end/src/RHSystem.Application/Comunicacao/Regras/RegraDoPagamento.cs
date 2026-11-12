namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>De onde veio a data: <c>REGRA</c> (o dia padrão da empresa) ou <c>EXCECAO</c> (o RH cadastrou a data do mês).</summary>
public enum OrigemDoPagamento
{
    Regra,
    Excecao,
}

/// <summary>O que a regra mexeu na data (só na <c>REGRA</c>): o mês não tem o dia, ou a data caiu num fim de semana ou num feriado e foi para o dia útil anterior.</summary>
public enum AjusteDoPagamento
{
    UltimoDiaDoMes,
    AntecipadoFimDeSemana,
    AntecipadoFeriado,
}

/// <summary>
/// O pagamento de um mês (<c>PagamentoData</c> do contrato da fatia 7). <c>Ano</c> e <c>Mes</c> são os do mês da regra, mesmo que a antecipação leve a data para o fim do mês anterior (a regra de novembro que cai no domingo 01/11 sai em 30/10 e
/// continua sendo a de novembro). A competência do holerite é sempre o mês anterior ao do pagamento.
/// </summary>
public sealed record PagamentoDoMes(int Ano, int Mes, DateOnly Data, OrigemDoPagamento Origem, IReadOnlyList<AjusteDoPagamento> Ajustes)
{
    public (int Ano, int Mes) Competencia => Mes == 1 ? (Ano - 1, 12) : (Ano, Mes - 1);
}

/// <summary>
/// A regra do pagamento (aceita pelo dono em 2026-10-29): o dia do mês da empresa; se o mês não tem o dia vale o último; se a data cai em fim de semana ou feriado (nacional, estadual da UF, municipal ou da empresa) antecipa para o <b>dia útil
/// anterior</b>; a <b>exceção</b> do mês vale mais que a regra e a data dela vale <b>como está</b> (quem escolhe é o RH). O próximo pagamento vale de hoje em diante (se hoje é o dia, é hoje).
/// </summary>
public static class RegraDoPagamento
{
    private const int MesesProcurados = 36;

    /// <summary>O pagamento do mês, ou <c>null</c> sem exceção e sem dia da regra. <paramref name="ehFeriado"/> diz se uma data é feriado que vale para a empresa.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O dia da regra não está entre 1 e 31.</exception>
    public static PagamentoDoMes? DoMes(int ano, int mes, int? diaDoPagamento, DateOnly? excecao, Func<DateOnly, bool> ehFeriado)
    {
        if (diaDoPagamento is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(diaDoPagamento), "O dia do pagamento vai de 1 a 31.");
        }

        if (excecao is { } data)
        {
            return new PagamentoDoMes(ano, mes, data, OrigemDoPagamento.Excecao, []);
        }

        if (diaDoPagamento is not { } dia)
        {
            return null;
        }

        var ajustes = new List<AjusteDoPagamento>();
        var ultimoDia = DateTime.DaysInMonth(ano, mes);
        if (dia > ultimoDia)
        {
            ajustes.Add(AjusteDoPagamento.UltimoDiaDoMes);
        }

        var pagamento = new DateOnly(ano, mes, Math.Min(dia, ultimoDia));
        if (EhFimDeSemana(pagamento))
        {
            ajustes.Add(AjusteDoPagamento.AntecipadoFimDeSemana);
        }
        else if (ehFeriado(pagamento))
        {
            ajustes.Add(AjusteDoPagamento.AntecipadoFeriado);
        }

        while (EhFimDeSemana(pagamento) || ehFeriado(pagamento))
        {
            pagamento = pagamento.AddDays(-1);
        }

        return new PagamentoDoMes(ano, mes, pagamento, OrigemDoPagamento.Regra, ajustes);
    }

    /// <summary>O primeiro pagamento de hoje em diante (hoje inclusive), do mês de <paramref name="hoje"/> para a frente; <c>null</c> se não há regra nem exceção nos próximos 36 meses.</summary>
    public static PagamentoDoMes? Proximo(DateOnly hoje, int? diaDoPagamento, Func<int, int, DateOnly?> excecaoDoMes, Func<DateOnly, bool> ehFeriado)
    {
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1);
        for (var i = 0; i < MesesProcurados; i++)
        {
            var mes = inicio.AddMonths(i);
            if (DoMes(mes.Year, mes.Month, diaDoPagamento, excecaoDoMes(mes.Year, mes.Month), ehFeriado) is { } pagamento && pagamento.Data >= hoje)
            {
                return pagamento;
            }
        }

        return null;
    }

    private static bool EhFimDeSemana(DateOnly dia) => dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
