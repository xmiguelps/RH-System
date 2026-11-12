using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Application.Tests.Comunicacao;

/// <summary>A janela dos aniversariantes (de hoje até 14 dias à frente): a próxima data, hoje conta, a virada de ano e o 29/02 nos anos que não são bissextos.</summary>
public class JanelaDeAniversariosTests
{
    [Theory]
    [InlineData("1990-03-15", "2026-10-07", "2027-03-15")] // já passou este ano: o do ano que vem
    [InlineData("1990-03-15", "2026-03-14", "2026-03-15")] // amanhã
    [InlineData("1990-03-15", "2026-03-15", "2026-03-15")] // hoje conta
    [InlineData("1990-03-15", "2026-03-16", "2027-03-15")] // ontem: o do ano que vem
    [InlineData("1990-01-05", "2026-12-25", "2027-01-05")] // a virada de ano
    [InlineData("1990-12-31", "2026-12-31", "2026-12-31")]
    [InlineData("1990-01-01", "2026-12-31", "2027-01-01")]
    [InlineData("2000-02-29", "2027-02-27", "2027-02-28")] // 29/02 num ano que não é bissexto: 28/02
    [InlineData("2000-02-29", "2027-02-28", "2027-02-28")]
    [InlineData("2000-02-29", "2027-03-01", "2028-02-29")] // o próximo bissexto
    [InlineData("2000-02-29", "2028-02-10", "2028-02-29")] // ano bissexto: 29/02
    [InlineData("2000-02-29", "2028-03-01", "2029-02-28")]
    [InlineData("1996-02-28", "2027-02-28", "2027-02-28")] // quem nasceu em 28/02 não muda
    public void A_proxima_data_do_aniversario(string nascimento, string hoje, string esperado) =>
        Assert.Equal(DateOnly.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), JanelaDeAniversarios.ProximoAniversario(DateOnly.Parse(nascimento, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(hoje, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("1990-10-07", "2026-10-07", 0, true)] // hoje
    [InlineData("1990-10-21", "2026-10-07", 14, true)] // o último dia da janela
    [InlineData("1990-10-22", "2026-10-07", 15, false)] // um dia depois
    [InlineData("1990-10-06", "2026-10-07", 364, false)] // ontem: só no ano que vem
    [InlineData("1990-01-08", "2026-12-25", 14, true)] // a janela atravessa o ano
    [InlineData("1990-01-09", "2026-12-25", 15, false)]
    [InlineData("1990-01-05", "2026-12-25", 11, true)]
    [InlineData("2000-02-29", "2027-02-28", 0, true)] // 29/02 comemora em 28/02
    [InlineData("2000-02-29", "2027-02-14", 14, true)]
    [InlineData("2000-02-29", "2027-02-13", 15, false)]
    [InlineData("2000-03-01", "2027-02-28", 1, true)]
    public void Os_dias_ate_o_aniversario_e_se_esta_na_janela_de_14_dias(string nascimento, string hoje, int dias, bool naJanela)
    {
        var nasceu = DateOnly.Parse(nascimento, System.Globalization.CultureInfo.InvariantCulture);
        var dia = DateOnly.Parse(hoje, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(dias, JanelaDeAniversarios.DiasAte(nasceu, dia));
        Assert.Equal(naJanela, JanelaDeAniversarios.EstaNaJanela(nasceu, dia));
    }

    [Fact]
    public void A_janela_e_de_14_dias() => Assert.Equal(14, JanelaDeAniversarios.DiasAFrente);
}
