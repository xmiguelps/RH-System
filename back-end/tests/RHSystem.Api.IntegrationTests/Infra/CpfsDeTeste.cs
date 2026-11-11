namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>CPFs de teste com os dois dígitos verificadores certos (a conta do trigger <c>TRG_FUNCIONARIOS_DETALHES_BEFORE_INSERT</c>), calculados aqui e não pelo Domain.</summary>
public static class CpfsDeTeste
{
    /// <summary>
    /// O CPF cujos nove primeiros dígitos são <paramref name="n"/> (de 1 a 999999999, completado com zeros à esquerda) e os dois últimos, os verificadores.
    /// Valores como 111111111 dariam um CPF de dígitos todos iguais, que o banco recusa: lançam.
    /// </summary>
    public static string Valido(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, 999_999_999);
        var digitos = n.ToString("D9", System.Globalization.CultureInfo.InvariantCulture).Select(c => c - '0').ToList();
        digitos.Add(Verificador(digitos));
        digitos.Add(Verificador(digitos));
        var cpf = string.Concat(digitos);
        if (cpf.All(c => c == cpf[0]))
        {
            throw new ArgumentException($"{n} dá um CPF de dígitos todos iguais, que o banco recusa.", nameof(n));
        }

        return cpf;
    }

    private static int Verificador(List<int> digitos)
    {
        var soma = 0;
        for (var i = 0; i < digitos.Count; i++)
        {
            soma += digitos[i] * (digitos.Count + 1 - i);
        }

        var resto = soma * 10 % 11;
        return resto == 10 ? 0 : resto;
    }
}
