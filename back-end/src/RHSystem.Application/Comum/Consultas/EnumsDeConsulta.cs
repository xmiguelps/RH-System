using System.Text.Json;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Comum.Consultas;

/// <summary>
/// O nome que o contrato dá a cada valor de enum: <c>MAIÚSCULAS_COM_SUBLINHADO</c> (<c>AcessoNegado</c> vira <c>ACESSO_NEGADO</c>), menos o
/// <see cref="Perfil"/>, que o contrato fixa como <c>Funcionario</c>, <c>Gestor</c> e <c>RH</c>. É a MESMA regra do JSON (<c>JsonDoContrato</c>):
/// o que o front lê no corpo é o que ele manda na consulta.
/// </summary>
public static class NomesDoContrato
{
    public static string Enum<TEnum>(TEnum valor)
        where TEnum : struct, Enum =>
        typeof(TEnum) == typeof(Perfil)
            ? valor.ToString()
            : JsonNamingPolicy.SnakeCaseUpper.ConvertName(valor.ToString());
}

/// <summary>
/// Liga <c>?area=ACESSO_NEGADO</c> pelo nome do contrato: o minimal API sozinho só entende o nome do C# (<c>AcessoNegado</c>), e o front manda o do
/// contrato.
/// </summary>
public static class EnumsDeConsulta
{
    /// <summary>
    /// Texto nulo ou vazio é <c>null</c> (o filtro não foi pedido); o nome do contrato dá o valor; qualquer outro texto, inclusive o nome do C#
    /// (<c>AcessoNegado</c>) e a grafia errada (<c>acesso_negado</c>), é erro de validação do <paramref name="campo"/> com os valores aceitos.
    /// </summary>
    /// <exception cref="ErroDeValidacaoException">O texto não é o nome de nenhum valor.</exception>
    public static TEnum? Ler<TEnum>(string? texto, string campo)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(texto))
        {
            return null;
        }

        var valores = System.Enum.GetValues<TEnum>();
        foreach (var valor in valores)
        {
            if (string.Equals(NomesDoContrato.Enum(valor), texto, StringComparison.Ordinal))
            {
                return valor;
            }
        }

        throw new ErroDeValidacaoException(campo, $"Valor inválido. Use um destes: {string.Join(", ", valores.Select(NomesDoContrato.Enum))}.");
    }
}
