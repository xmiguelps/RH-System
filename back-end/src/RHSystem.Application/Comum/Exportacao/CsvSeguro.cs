using System.Buffers;
using System.Globalization;
using System.Text;

namespace RHSystem.Application.Comum.Exportacao;

/// <summary>
/// CSV que o Excel abre sem executar nada: célula de texto que começa com <c>=</c>, <c>+</c>, <c>-</c> ou <c>@</c> (ou TAB ou CR) é lida como fórmula, então
/// ganha um <c>'</c> na frente (decisão 9 do roadmap). Número não passa por <see cref="Texto"/>: um saldo negativo continua sendo número.
/// </summary>
public static class CsvSeguro
{
    private const char Separador = ';';

    // A célula com separador, aspas ou quebra de linha vai entre aspas.
    private static readonly SearchValues<char> ExigemAspas = SearchValues.Create(";\"\r\n");

    public static string Texto(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return string.Empty;
        }

        return valor[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + valor : valor;
    }

    /// <summary>O número como texto, sem neutralizar: <c>Numero(-225)</c> é <c>"-225"</c>.</summary>
    public static string Numero(long valor) => valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// UTF-8 com BOM (o Excel abre os acentos certos), separador <c>;</c>, aspas dobradas e fim de linha CRLF. As células já vêm prontas
    /// (<see cref="Texto"/> e <see cref="Numero"/>): aqui só se põe aspas onde o formato exige.
    /// </summary>
    public static byte[] Escrever(IReadOnlyList<string> titulos, IEnumerable<IReadOnlyList<string>> linhas)
    {
        var texto = new StringBuilder();
        AcrescentarLinha(texto, titulos);
        foreach (var linha in linhas)
        {
            AcrescentarLinha(texto, linha);
        }

        var codificacao = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. codificacao.GetPreamble(), .. codificacao.GetBytes(texto.ToString())];
    }

    private static void AcrescentarLinha(StringBuilder texto, IReadOnlyList<string> celulas)
    {
        for (var i = 0; i < celulas.Count; i++)
        {
            if (i > 0)
            {
                texto.Append(Separador);
            }

            texto.Append(Celula(celulas[i]));
        }

        texto.Append("\r\n");
    }

    private static string Celula(string valor) =>
        valor.AsSpan().ContainsAny(ExigemAspas)
            ? "\"" + valor.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : valor;
}
