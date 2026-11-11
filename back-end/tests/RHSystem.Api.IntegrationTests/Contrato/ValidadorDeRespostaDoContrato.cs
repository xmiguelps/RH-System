using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Confere um JSON real contra o schema do contrato (já resolvido, sem <c>$ref</c>). É pequeno de propósito: tipo (com <c>null</c> só onde o schema
/// aceita), <c>required</c>, chaves que o schema não declara (quando há <c>properties</c> e não há <c>additionalProperties</c>), <c>enum</c>,
/// <c>format: date-time</c> terminando em <c>Z</c>, <c>format: date</c> em <c>yyyy-MM-dd</c>, itens de <c>array</c>, <c>oneOf</c>/<c>anyOf</c> (vale se um
/// ramo bater) e <c>allOf</c> (une <c>properties</c> e <c>required</c> dos membros antes de validar). NÃO valida <c>minLength</c>, <c>pattern</c> nem
/// outras restrições de valor. Devolve uma frase por problema; lista vazia = conforme.
/// </summary>
internal static class ValidadorDeRespostaDoContrato
{
    public static IReadOnlyList<string> Validar(JsonNode schemaResolvido, JsonElement json)
    {
        var problemas = new List<string>();
        ValidarNo(schemaResolvido, json, "$", problemas);
        return problemas;
    }

    private static void ValidarNo(JsonNode? bruto, JsonElement valor, string caminho, List<string> problemas)
    {
        if (bruto is not JsonObject original)
        {
            return; // sem schema (ou "true"): aceita qualquer valor
        }

        var schema = Combinar(original);

        foreach (var chave in new[] { "oneOf", "anyOf" })
        {
            if (schema[chave] is not JsonArray ramos)
            {
                continue;
            }

            var motivos = new List<string>();
            var algumBate = false;
            foreach (var ramo in ramos)
            {
                var doRamo = new List<string>();
                ValidarNo(ramo, valor, caminho, doRamo);
                if (doRamo.Count == 0)
                {
                    algumBate = true;
                    break;
                }

                motivos.AddRange(doRamo);
            }

            if (!algumBate)
            {
                problemas.Add($"{caminho}: nenhum ramo de {chave} aceita o valor ({string.Join("; ", motivos.Take(3))})");
                return;
            }
        }

        var tipos = TiposDe(schema);
        if (tipos.Count > 0 && !tipos.Any(t => TemOTipo(t, valor)))
        {
            problemas.Add($"{caminho}: o tipo é {NomeDoTipo(valor)} e o schema aceita [{string.Join(", ", tipos)}]");
            return;
        }

        if (valor.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        ValidarEnum(schema, valor, caminho, problemas);

        switch (valor.ValueKind)
        {
            case JsonValueKind.Object:
                ValidarObjeto(schema, valor, caminho, problemas);
                break;
            case JsonValueKind.Array:
                var indice = 0;
                foreach (var item in valor.EnumerateArray())
                {
                    ValidarNo(schema["items"], item, $"{caminho}[{indice++}]", problemas);
                }

                break;
            case JsonValueKind.String:
                ValidarFormato(schema, valor.GetString()!, caminho, problemas);
                break;
        }
    }

    private static void ValidarObjeto(JsonObject schema, JsonElement valor, string caminho, List<string> problemas)
    {
        var declaradas = schema["properties"] as JsonObject;
        var obrigatorias = (schema["required"] as JsonArray)?.Select(n => n!.GetValue<string>()) ?? [];
        var presentes = valor.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        foreach (var nome in obrigatorias.Where(n => !presentes.ContainsKey(n)))
        {
            problemas.Add($"{caminho}: o campo \"{nome}\" é obrigatório e está ausente");
        }

        foreach (var (nome, conteudo) in presentes)
        {
            if (declaradas is not null && declaradas.TryGetPropertyValue(nome, out var doCampo))
            {
                ValidarNo(doCampo, conteudo, $"{caminho}.{nome}", problemas);
                continue;
            }

            switch (schema["additionalProperties"])
            {
                case JsonObject paraOsValores:
                    ValidarNo(paraOsValores, conteudo, $"{caminho}.{nome}", problemas);
                    break;
                case JsonValue permissao when permissao.ToString() is "true" or "True":
                    break;
                default:
                    if (declaradas is not null)
                    {
                        problemas.Add($"{caminho}: o schema não declara o campo \"{nome}\", que a API enviou");
                    }

                    break;
            }
        }
    }

    private static void ValidarEnum(JsonObject schema, JsonElement valor, string caminho, List<string> problemas)
    {
        if (schema["enum"] is not JsonArray permitidos)
        {
            return;
        }

        var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString()! : valor.GetRawText();
        var aceitos = permitidos.Select(p => p?.ToString() ?? "null").ToList();
        if (!aceitos.Contains(texto, StringComparer.Ordinal))
        {
            problemas.Add($"{caminho}: \"{texto}\" não está entre os valores aceitos ({string.Join(", ", aceitos)})");
        }
    }

    private static void ValidarFormato(JsonObject schema, string texto, string caminho, List<string> problemas)
    {
        switch (schema["format"]?.ToString())
        {
            case "date-time":
                if (!texto.EndsWith('Z') || !DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                {
                    problemas.Add($"{caminho}: o instante precisa estar em UTC e terminar em Z (veio \"{texto}\")");
                }

                break;
            case "date":
                if (!DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    problemas.Add($"{caminho}: a data precisa estar em yyyy-MM-dd (veio \"{texto}\")");
                }

                break;
        }
    }

    /// <summary>Une <c>properties</c> e <c>required</c> dos membros de um <c>allOf</c> (e as demais palavras, a primeira que aparecer vale).</summary>
    private static JsonObject Combinar(JsonObject schema)
    {
        if (schema["allOf"] is not JsonArray membros)
        {
            return schema;
        }

        var propriedades = new JsonObject();
        var obrigatorias = new List<string>();
        var resultado = new JsonObject();

        void Absorver(JsonObject parte)
        {
            foreach (var (chave, conteudo) in parte)
            {
                switch (chave)
                {
                    case "allOf":
                        break;
                    case "properties" when conteudo is JsonObject props:
                        foreach (var (nome, doCampo) in props)
                        {
                            propriedades[nome] = doCampo?.DeepClone();
                        }

                        break;
                    case "required" when conteudo is JsonArray lista:
                        obrigatorias.AddRange(lista.Select(n => n!.GetValue<string>()));
                        break;
                    default:
                        resultado[chave] ??= conteudo?.DeepClone();
                        break;
                }
            }
        }

        Absorver(schema);
        foreach (var membro in membros.OfType<JsonObject>())
        {
            Absorver(Combinar(membro));
        }

        if (propriedades.Count > 0)
        {
            resultado["properties"] = propriedades;
        }

        if (obrigatorias.Count > 0)
        {
            resultado["required"] = new JsonArray(obrigatorias.Distinct().Select(n => (JsonNode?)JsonValue.Create(n)).ToArray());
        }

        return resultado;
    }

    private static List<string> TiposDe(JsonObject schema) => schema["type"] switch
    {
        JsonArray lista => lista.Select(t => t!.ToString()).ToList(),
        JsonValue unico => [unico.ToString()],
        _ => [],
    };

    private static bool TemOTipo(string tipo, JsonElement valor) => tipo switch
    {
        "string" => valor.ValueKind == JsonValueKind.String,
        "integer" => valor.ValueKind == JsonValueKind.Number && valor.TryGetInt64(out _),
        "number" => valor.ValueKind == JsonValueKind.Number,
        "boolean" => valor.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object" => valor.ValueKind == JsonValueKind.Object,
        "array" => valor.ValueKind == JsonValueKind.Array,
        "null" => valor.ValueKind == JsonValueKind.Null,
        _ => true,
    };

    private static string NomeDoTipo(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => valor.TryGetInt64(out _) ? "integer" : "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.Null => "null",
        _ => valor.ValueKind.ToString(),
    };
}
