namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>
/// A janela dos aniversariantes: de hoje até 14 dias à frente (<c>diasAte</c> de 0 a 14). O aniversário de um ano que não é bissexto para quem nasceu em 29/02 é 28/02. O ano e a idade nunca saem daqui (só o dia e o mês de nascimento entram na conta, e só a próxima data sai).
/// </summary>
public static class JanelaDeAniversarios
{
    public const int DiasAFrente = 14;

    /// <summary>
    /// A próxima data de aniversário de hoje em diante: a do ano de hoje se ainda não passou (hoje conta, <c>diasAte</c> 0), senão a do ano seguinte (a virada de ano: em 25/12, quem faz em 05/01 comemora no ano que vem). Quem nasceu em 29/02 comemora em 28/02 nos
    /// anos que não são bissextos.
    /// </summary>
    public static DateOnly ProximoAniversario(DateOnly nascimento, DateOnly hoje)
    {
        var desteAno = NoAno(nascimento, hoje.Year);
        return desteAno >= hoje ? desteAno : NoAno(nascimento, hoje.Year + 1);
    }

    /// <summary>Quantos dias faltam para a próxima data de aniversário (0 é hoje).</summary>
    public static int DiasAte(DateOnly nascimento, DateOnly hoje) => ProximoAniversario(nascimento, hoje).DayNumber - hoje.DayNumber;

    /// <summary>O aniversário entra na lista se faltam de 0 a 14 dias.</summary>
    public static bool EstaNaJanela(DateOnly nascimento, DateOnly hoje) => DiasAte(nascimento, hoje) <= DiasAFrente;

    private static DateOnly NoAno(DateOnly nascimento, int ano) =>
        new(ano, nascimento.Month, nascimento.Month == 2 && nascimento.Day == 29 && !DateTime.IsLeapYear(ano) ? 28 : nascimento.Day);
}
