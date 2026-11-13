namespace RHSystem.Application.Ferias.Regras;

/// <summary>Um ano de casa, contado da admissão. O prazo para tirar é 12 meses depois do fim (informação: o saldo não vence).</summary>
public sealed record PeriodoAquisitivo(DateOnly Inicio, DateOnly Fim)
{
    public DateOnly PrazoParaTirar => Fim.AddYears(1);
}

/// <summary>
/// Os períodos aquisitivos de uma pessoa. O n-ésimo começa em <c>admissão + n anos</c> (sempre a partir da admissão original, não encadeado: quem entrou em 29/02 não "anda" um dia por ano) e termina na véspera do começo do seguinte, então não há buraco nem sobreposição.
/// O período só está fechado depois do último dia; pessoa desligada só tem os períodos que fecharam até o desligamento (o ano que não se completou não existe).
/// </summary>
public static class PeriodosAquisitivos
{
    public static IReadOnlyList<PeriodoAquisitivo> Calcular(DateOnly admissao, DateOnly hoje, DateOnly? desligamento)
    {
        var periodos = new List<PeriodoAquisitivo>();
        for (var n = 0; ; n++)
        {
            var inicio = admissao.AddYears(n);
            if (inicio > hoje)
            {
                break;
            }

            var fim = admissao.AddYears(n + 1).AddDays(-1);
            if (desligamento is { } saida && fim > saida)
            {
                break;
            }

            periodos.Add(new PeriodoAquisitivo(inicio, fim));
        }

        return periodos;
    }

    /// <summary>Fechado = já passou o último dia do período; é o que libera os 30 dias.</summary>
    public static bool Fechado(PeriodoAquisitivo periodo, DateOnly hoje) => periodo.Fim < hoje;

    /// <summary>Os meses completos desde o início do período ("6 de 12 meses trabalhados").</summary>
    public static int MesesTrabalhados(PeriodoAquisitivo periodo, DateOnly hoje)
    {
        var meses = 0;
        while (periodo.Inicio.AddMonths(meses + 1) <= hoje)
        {
            meses++;
        }

        return meses;
    }
}
