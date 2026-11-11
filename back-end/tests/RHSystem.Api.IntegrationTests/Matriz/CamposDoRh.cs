using System.Text.Json;
using System.Text.Json.Nodes;

namespace RHSystem.Api.IntegrationTests.Matriz;

/// <summary>
/// Os campos que só o RH recebe (CPF, RG, endereço, e-mail pessoal, matrícula, benefícios, documentos, holerites, telefone). O gestor recebe o
/// telefone em <c>telefoneMascarado</c> (outra chave, permitida) e nunca recebe estes. A varredura vale para o JSON real e para o schema do contrato.
/// </summary>
internal static class CamposDoRh
{
    public static readonly string[] Proibidos =
    [
        "cpf", "rg", "rgOrgaoEmissor", "emailPessoal", "endereco", "matricula", "beneficios", "documentos", "holerites", "telefone",
    ];

    private static bool EProibido(string nome) => Proibidos.Contains(nome, StringComparer.OrdinalIgnoreCase);

    /// <summary>Falha se o JSON tem um campo proibido em qualquer profundidade.</summary>
    public static void AfirmarSemCamposDoRh(JsonElement json)
    {
        var achados = new List<string>();
        Varrer(json, "$", achados);
        Assert.True(achados.Count == 0, "Campo do RH numa resposta que não é do RH:\n- " + string.Join("\n- ", achados));
    }

    /// <summary>
    /// Os campos proibidos que o schema (já resolvido) declara, em <c>properties</c>, <c>items</c>, <c>allOf</c>, <c>oneOf</c> e <c>anyOf</c>, em qualquer
    /// profundidade. O contrato usa <c>allOf</c> com <c>Paginacao</c> em toda lista paginada: ficar só em <c>properties</c> deixaria a varredura vazia sem avisar.
    /// </summary>
    public static IReadOnlyList<string> ProibidosNoSchema(JsonNode schemaResolvido)
    {
        var achados = new List<string>();
        VarrerSchema(schemaResolvido, "$", achados);
        return achados;
    }

    private static void Varrer(JsonElement no, string caminho, List<string> achados)
    {
        switch (no.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propriedade in no.EnumerateObject())
                {
                    if (EProibido(propriedade.Name))
                    {
                        achados.Add($"{caminho}.{propriedade.Name}");
                    }

                    Varrer(propriedade.Value, $"{caminho}.{propriedade.Name}", achados);
                }

                break;
            case JsonValueKind.Array:
                var indice = 0;
                foreach (var item in no.EnumerateArray())
                {
                    Varrer(item, $"{caminho}[{indice++}]", achados);
                }

                break;
        }
    }

    private static void VarrerSchema(JsonNode? no, string caminho, List<string> achados)
    {
        if (no is not JsonObject schema)
        {
            return;
        }

        if (schema["properties"] is JsonObject propriedades)
        {
            foreach (var (nome, doCampo) in propriedades)
            {
                if (EProibido(nome))
                {
                    achados.Add($"o schema declara o campo proibido \"{nome}\" em {caminho}");
                }

                VarrerSchema(doCampo, $"{caminho}.{nome}", achados);
            }
        }

        VarrerSchema(schema["items"], $"{caminho}[]", achados);
        VarrerSchema(schema["additionalProperties"] as JsonObject, $"{caminho}.*", achados);
        foreach (var chave in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (schema[chave] is JsonArray ramos)
            {
                foreach (var ramo in ramos)
                {
                    VarrerSchema(ramo, caminho, achados);
                }
            }
        }
    }
}
