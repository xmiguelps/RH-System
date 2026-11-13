using System.Globalization;
using System.Text;
using RHSystem.Application.Arquivos;

namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>O número que o certificado mostra: <c>CERT-{ano de DATA_EMISSAO}-{id do certificado com 5 dígitos}</c> (calculado, sem coluna; id maior que 99999 passa a ter mais dígitos).</summary>
public static class NumeroDoCertificado
{
    public static string De(int ano, int idDoCertificado) => $"CERT-{ano.ToString(CultureInfo.InvariantCulture)}-{idDoCertificado.ToString("D5", CultureInfo.InvariantCulture)}";
}

/// <summary>Os nomes dos arquivos baixados: o CSV de quem fez o treinamento e o PDF do certificado.</summary>
public static class NomesDeArquivoDoTreinamento
{
    /// <summary><c>treinamento-&lt;título em minúsculas, sem acento, com hífens&gt;.csv</c> (título sem letra nem número fica só <c>treinamento.csv</c>).</summary>
    public static string CsvDosParticipantes(string titulo)
    {
        var decomposto = titulo.Normalize(NormalizationForm.FormD);
        var limpo = new StringBuilder(decomposto.Length);
        var separador = true;
        foreach (var letra in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var minuscula = char.ToLowerInvariant(letra);
            if (minuscula is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                limpo.Append(minuscula);
                separador = false;
            }
            else if (!separador)
            {
                limpo.Append('-');
                separador = true;
            }
        }

        var nome = limpo.ToString().Trim('-');
        return nome.Length == 0 ? "treinamento.csv" : $"treinamento-{nome}.csv";
    }

    /// <summary><c>certificado-&lt;título&gt;.pdf</c>, limpo como os outros downloads, sem diretório e com no máximo 100 caracteres (o título é cortado, a extensão nunca).</summary>
    public static string PdfDoCertificado(string titulo) => NomeParaDownload.Montar("certificado-", titulo, ".pdf");
}
