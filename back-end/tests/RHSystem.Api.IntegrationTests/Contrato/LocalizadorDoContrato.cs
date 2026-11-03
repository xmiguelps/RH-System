using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>Acha e lê o <c>contrato/openapi.yaml</c> (o contrato da API, que é lei) como uma árvore JSON.</summary>
public static class LocalizadorDoContrato
{
    /// <summary>Sobe de <c>AppContext.BaseDirectory</c> até a pasta que tem <c>contrato/openapi.yaml</c>; falha mostrando tudo o que procurou.</summary>
    public static string CaminhoDoOpenApi()
    {
        var procurados = new List<string>();
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var candidato = Path.Combine(pasta.FullName, "contrato", "openapi.yaml");
            procurados.Add(candidato);
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new FileNotFoundException($"O contrato (contrato/openapi.yaml) não foi encontrado. Procurei em: {string.Join("; ", procurados)}");
    }

    public static JsonNode CarregarContrato() => YamlParaJson(File.ReadAllText(CaminhoDoOpenApi()));

    /// <summary>
    /// Converte YAML em <see cref="JsonNode"/>. Todo valor simples vira texto: o comparador só olha a estrutura (tipos, nomes, listas
    /// <c>required</c>), e valores como <c>minimum: 1</c> nem entram na comparação.
    /// </summary>
    public static JsonNode YamlParaJson(string yaml)
    {
        var fluxo = new YamlStream();
        fluxo.Load(new StringReader(yaml));
        return Converter(fluxo.Documents[0].RootNode)!;
    }

    private static JsonNode? Converter(YamlNode no) => no switch
    {
        YamlMappingNode mapa => new JsonObject(mapa.Children.Select(c => new KeyValuePair<string, JsonNode?>(((YamlScalarNode)c.Key).Value ?? string.Empty, Converter(c.Value)))),
        YamlSequenceNode lista => new JsonArray(lista.Children.Select(Converter).ToArray()),
        YamlScalarNode simples => JsonValue.Create(simples.Value),
        _ => null,
    };
}
