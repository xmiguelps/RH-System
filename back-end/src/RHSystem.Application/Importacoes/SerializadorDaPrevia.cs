using System.Text.Json;
using System.Text.Json.Serialization;

namespace RHSystem.Application.Importacoes;

/// <summary>
/// O texto da prévia que vai para <c>IMPORTACOES.PREVIA</c> (JSON): camelCase e enum pelo nome do contrato (<c>COM_ERRO</c>, <c>NIVEL_DE_ACESSO</c>), com opções PRÓPRIAS, sem depender das da API: mudar o JSON da API não
/// pode tornar ilegível o que já está guardado.
/// </summary>
public static class SerializadorDaPrevia
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    public static string Serializar(IReadOnlyList<ImportacaoLinha> linhas) => JsonSerializer.Serialize(linhas, Opcoes);

    /// <exception cref="InvalidOperationException">O texto guardado não é uma lista de linhas.</exception>
    public static IReadOnlyList<ImportacaoLinha> Ler(string json) =>
        JsonSerializer.Deserialize<List<ImportacaoLinha>>(json, Opcoes) ?? throw new InvalidOperationException("A prévia guardada não é uma lista de linhas.");
}
