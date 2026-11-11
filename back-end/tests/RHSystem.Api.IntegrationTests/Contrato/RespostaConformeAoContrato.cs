using System.Text.Json;

namespace RHSystem.Api.IntegrationTests.Contrato;

internal static class RespostaConformeAoContrato
{
    /// <summary>
    /// Valida o JSON REAL da resposta contra o schema que o contrato declara para o status REAL que veio (e para o tipo de mídia real). Falha dizendo
    /// o caminho do campo (<c>$.usuario.perfil</c>) e o que está errado. Resposta sem corpo declarado (204) confere que o corpo veio vazio.
    /// </summary>
    public static async Task AfirmarConformeAoContratoAsync(this HttpResponseMessage resposta, string operationId)
    {
        var status = (int)resposta.StatusCode;
        var declarada = ContratoDaApi.RespostaDeclarada(operationId, status);
        Assert.True(declarada is not null, $"{operationId}: o contrato não declara a resposta {status}.");

        var corpo = await resposta.Content.ReadAsStringAsync();
        if (declarada!["content"] is not System.Text.Json.Nodes.JsonObject conteudo)
        {
            Assert.True(string.IsNullOrEmpty(corpo), $"{operationId} ({status}): o contrato não declara corpo, e a API enviou: {corpo}");
            return;
        }

        var tipoDeMidia = resposta.Content.Headers.ContentType?.MediaType ?? string.Empty;
        Assert.True(
            conteudo.TryGetPropertyValue(tipoDeMidia, out var midia),
            $"{operationId} ({status}): o contrato declara [{string.Join(", ", conteudo.Select(c => c.Key))}] e a API enviou \"{tipoDeMidia}\".");

        if (midia?["schema"] is not { } schema || !tipoDeMidia.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return; // o arquivo (CSV, PDF) não é JSON: o tipo de mídia declarado já foi conferido acima
        }

        var problemas = ValidadorDeRespostaDoContrato.Validar(schema, JsonSerializer.Deserialize<JsonElement>(corpo));
        Assert.True(problemas.Count == 0, $"{operationId} ({status}) não bate com o contrato:\n- " + string.Join("\n- ", problemas));
    }
}
