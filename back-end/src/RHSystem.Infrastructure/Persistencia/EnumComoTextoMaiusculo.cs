using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RHSystem.Infrastructure.Persistencia;

/// <summary>
/// Enum do C# (<c>SemRegistro</c>) como o texto em maiúsculas que o banco e o contrato guardam (<c>SEM_REGISTRO</c>). Um texto que não
/// seja de nenhum membro lança na leitura: um valor novo que o grupo ponha no banco aparece como erro, e não como outro valor.
/// </summary>
public partial class EnumComoTextoMaiusculo<TEnum>()
    : ValueConverter<TEnum, string>(v => NomesDeEnum<TEnum>.ParaTexto(v), t => NomesDeEnum<TEnum>.DoTexto(t))
    where TEnum : struct, Enum;

internal static partial class NomesDeEnum<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> ParaTextoMapa =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => PascalParaMaiusculoComSublinhado(v.ToString()));

    private static readonly Dictionary<string, TEnum> DoTextoMapa =
        ParaTextoMapa.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    internal static string ParaTexto(TEnum valor) =>
        ParaTextoMapa.TryGetValue(valor, out var texto)
            ? texto
            : throw new InvalidOperationException($"O valor {valor} não é um membro de {typeof(TEnum).Name}.");

    internal static TEnum DoTexto(string texto) =>
        DoTextoMapa.TryGetValue(texto, out var valor)
            ? valor
            : throw new InvalidOperationException($"O texto \"{texto}\" não é um valor de {typeof(TEnum).Name} ({string.Join(", ", DoTextoMapa.Keys)}).");

    private static string PascalParaMaiusculoComSublinhado(string nome) =>
        FronteiraDePalavra().Replace(nome, "_").ToUpperInvariant();

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex FronteiraDePalavra();
}
