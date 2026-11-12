using System.Text;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>Conta caracteres como o banco conta (<c>VARCHAR(n)</c> em utf8mb4 é por caractere): um emoji tem dois pedaços UTF-16 e conta um.</summary>
internal static class ContagemDeCaracteres
{
    public static int De(string texto) => texto.EnumerateRunes().Count();

    /// <summary>Os primeiros <paramref name="maximo"/> caracteres, sem cortar um emoji ao meio e sem espaço sobrando no fim.</summary>
    public static string Cortar(string texto, int maximo)
    {
        if (De(texto) <= maximo)
        {
            return texto;
        }

        var corte = new StringBuilder();
        var contados = 0;
        foreach (var caractere in texto.EnumerateRunes())
        {
            if (contados == maximo)
            {
                break;
            }

            corte.Append(caractere.ToString());
            contados++;
        }

        return corte.ToString().TrimEnd();
    }
}
