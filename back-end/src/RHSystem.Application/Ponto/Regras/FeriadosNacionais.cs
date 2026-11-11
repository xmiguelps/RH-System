namespace RHSystem.Application.Ponto.Regras;

/// <summary>Um feriado calculado para um ano: o nome (com acento) e o dia.</summary>
public sealed record FeriadoCalculado(string Nome, DateOnly Data);

/// <summary>
/// Os feriados nacionais de um ano: os dez da carga do <c>V001</c> (de 2026), com a Sexta-feira Santa calculada pela Páscoa. Carnaval e Corpus Christi são pontos facultativos e <b>não</b> entram (a carga do banco também não os trouxe; a empresa que os
/// adota cadastra como feriado dela).
/// </summary>
public static class FeriadosNacionais
{
    public static IReadOnlyList<FeriadoCalculado> Do(int ano) =>
    [
        new("Confraternização Universal", new DateOnly(ano, 1, 1)),
        new("Sexta-feira Santa", Pascoa(ano).AddDays(-2)),
        new("Tiradentes", new DateOnly(ano, 4, 21)),
        new("Dia do Trabalho", new DateOnly(ano, 5, 1)),
        new("Independência do Brasil", new DateOnly(ano, 9, 7)),
        new("Nossa Senhora Aparecida", new DateOnly(ano, 10, 12)),
        new("Finados", new DateOnly(ano, 11, 2)),
        new("Proclamação da República", new DateOnly(ano, 11, 15)),
        new("Dia da Consciência Negra", new DateOnly(ano, 11, 20)),
        new("Natal", new DateOnly(ano, 12, 25)),
    ];

    /// <summary>O domingo de Páscoa (calendário gregoriano), pelo algoritmo de Meeus, Jones e Butcher.</summary>
    public static DateOnly Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var mes = (h + l - (7 * m) + 114) / 31;
        var dia = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(ano, mes, dia);
    }
}
