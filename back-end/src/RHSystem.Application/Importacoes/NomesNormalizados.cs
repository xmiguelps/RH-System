using System.Globalization;
using System.Text;

namespace RHSystem.Application.Importacoes;

/// <summary>
/// Como a importação compara texto digitado com o que existe (títulos do modelo, nomes de cargo, setor e benefício, tipo de contrato e nível): sem espaços nas pontas, sem acentos e sem diferenciar maiúsculas
/// (decisões 6 e 7 do plano da fatia 2). O banco já proíbe dois nomes iguais ignorando acentos, então a comparação nunca é ambígua.
/// </summary>
public static class NomesNormalizados
{
    public static string Chave(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var chave = new StringBuilder(decomposto.Length);
        foreach (var letra in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
            {
                chave.Append(letra);
            }
        }

        return chave.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
