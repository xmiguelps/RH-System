using System.Text.Json.Nodes;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Troca cada <c>$ref</c> (<c>#/components/schemas/X</c>, <c>#/components/responses/Y</c>) pelo conteúdo que ele aponta, e devolve uma cópia
/// "resolvida" que não depende mais do documento. Uma referência que volta para si mesma (ciclo) vira um schema vazio, que aceita qualquer valor.
/// </summary>
internal static class ResolvedorDeEsquema
{
    public static JsonNode Resolver(JsonNode no, JsonNode raiz) => Resolver(no, raiz, []);

    private static JsonNode Resolver(JsonNode no, JsonNode raiz, HashSet<string> emCurso)
    {
        switch (no)
        {
            case JsonObject objeto:
                if (objeto["$ref"] is JsonValue referencia && referencia.TryGetValue<string>(out var caminho))
                {
                    if (!emCurso.Add(caminho))
                    {
                        return new JsonObject();
                    }

                    var resolvido = Resolver(Buscar(raiz, caminho), raiz, emCurso);
                    emCurso.Remove(caminho);
                    return resolvido;
                }

                var copia = new JsonObject();
                foreach (var (chave, valor) in objeto)
                {
                    copia[chave] = valor is null ? null : Resolver(valor, raiz, emCurso);
                }

                return copia;
            case JsonArray lista:
                var itens = new JsonArray();
                foreach (var item in lista)
                {
                    itens.Add(item is null ? null : Resolver(item, raiz, emCurso));
                }

                return itens;
            default:
                return no.DeepClone();
        }
    }

    private static JsonNode Buscar(JsonNode raiz, string caminho)
    {
        JsonNode? atual = raiz;
        foreach (var parte in caminho.TrimStart('#', '/').Split('/'))
        {
            atual = atual?[parte.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)];
        }

        return atual ?? throw new InvalidOperationException($"A referência {caminho} não existe no contrato.");
    }
}
