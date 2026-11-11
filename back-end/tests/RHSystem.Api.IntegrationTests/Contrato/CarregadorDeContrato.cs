using System.Text.Json.Nodes;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// O MESMO leitor para o contrato (<c>contrato/openapi.yaml</c>) e para o documento que a API gera: os dois viram <see cref="JsonNode"/>
/// (o YAML é convertido por <see cref="LocalizadorDoContrato"/>), que é o que o comparador de divergência já usa. O contrato é lido uma vez por execução.
/// </summary>
internal static class CarregadorDeContrato
{
    private static readonly Lazy<JsonNode> Carregado = new(LocalizadorDoContrato.CarregarContrato);

    public static JsonNode Contrato() => Carregado.Value;

    public static async Task<JsonNode> GeradoAsync(HttpClient clienteDoApiFactory) =>
        JsonNode.Parse(await clienteDoApiFactory.GetStringAsync("/openapi/v1.json"))!;
}
