using System.Text.Json.Nodes;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Compara o contrato (<c>contrato/openapi.yaml</c>) com o documento OpenAPI que a API gera, SÓ nas operações que a fatia já implementa
/// (lista de <c>operationId</c>, nunca prefixo de caminho). Devolve uma frase em português por divergência; lista vazia quando bate.
/// Compara: existência, <c>operationId</c>, códigos de resposta, tipos de mídia, schemas do corpo da requisição (JSON e
/// <c>multipart/form-data</c>) e das respostas 2xx em JSON, e parâmetros. Não compara <c>summary</c>, <c>description</c>, <c>tags</c>,
/// exemplos, restrições de valor (<c>minLength</c> etc.), extensões <c>x-*</c>, <c>security</c> nem cabeçalhos.
/// Trabalha sobre <see cref="JsonNode"/> (o YAML do contrato é convertido): resolve <c>$ref</c> sozinho, une o <c>allOf</c> e normaliza as
/// formas de escrever "aceita null".
/// </summary>
public static class ComparadorDeContrato
{
    private static readonly string[] Metodos = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    private sealed record Operacao(string Id, string Metodo, string Caminho, JsonObject No, JsonNode? ParametrosDoCaminho);

    private sealed class Canonico
    {
        public HashSet<string> Tipos { get; } = [];

        public SortedDictionary<string, Canonico> Propriedades { get; } = [];

        public HashSet<string> Obrigatorias { get; } = [];

        public Canonico? Itens { get; set; }

        public string? Formato { get; set; }

        public HashSet<string> Valores { get; } = [];

        public string? Ciclo { get; set; }
    }

    public static IReadOnlyList<string> Comparar(JsonNode contrato, JsonNode gerado, IReadOnlySet<string> operationIdsNoEscopo)
    {
        var divergencias = new List<string>();
        var doContrato = Operacoes(contrato);
        var daApi = Operacoes(gerado);

        foreach (var id in operationIdsNoEscopo.Order(StringComparer.Ordinal))
        {
            doContrato.TryGetValue(id, out var c);
            daApi.TryGetValue(id, out var g);

            if (c is null && g is null)
            {
                divergencias.Add($"{id}: a operação está no escopo, mas não existe nem no contrato nem na API.");
                continue;
            }

            if (c is null)
            {
                divergencias.Add($"{id}: a operação existe na API ({g!.Metodo.ToUpperInvariant()} {g.Caminho}), mas não existe no contrato.");
                continue;
            }

            if (g is null)
            {
                var outra = daApi.Values.FirstOrDefault(o => o.Metodo == c.Metodo && o.Caminho == c.Caminho);
                divergencias.Add(outra is null
                    ? $"{id}: a operação existe no contrato ({c.Metodo.ToUpperInvariant()} {c.Caminho}), mas a API não a tem."
                    : $"{id}: o operationId é diferente em {c.Metodo.ToUpperInvariant()} {c.Caminho}: no contrato é '{id}' e na API é '{outra.Id}'.");
                continue;
            }

            if (c.Metodo != g.Metodo || c.Caminho != g.Caminho)
            {
                divergencias.Add($"{id}: o contrato diz {c.Metodo.ToUpperInvariant()} {c.Caminho} e a API {g.Metodo.ToUpperInvariant()} {g.Caminho}.");
            }

            CompararOperacao(id, contrato, c, gerado, g, divergencias);
        }

        return divergencias;
    }

    /// <summary>
    /// As operações (`MÉTODO caminho`) que NÃO têm <c>operationId</c>. Elas não entram no índice nem no escopo; sem esta lista uma rota
    /// mapeada sem <c>.WithName</c> sumiria da comparação em silêncio.
    /// </summary>
    public static IReadOnlyList<string> OperacoesSemOperationId(JsonNode documento)
    {
        var semId = new List<string>();
        if (documento["paths"] is not JsonObject caminhos)
        {
            return semId;
        }

        foreach (var (caminho, item) in caminhos)
        {
            foreach (var metodo in Metodos)
            {
                if (item is JsonObject itemDoCaminho && itemDoCaminho[metodo] is JsonObject operacao && operacao["operationId"] is null)
                {
                    semId.Add($"{metodo.ToUpperInvariant()} {caminho}");
                }
            }
        }

        return semId;
    }

    // ----- índice das operações -----

    private static Dictionary<string, Operacao> Operacoes(JsonNode documento)
    {
        var indice = new Dictionary<string, Operacao>(StringComparer.Ordinal);
        if (documento["paths"] is not JsonObject caminhos)
        {
            return indice;
        }

        foreach (var (caminho, item) in caminhos)
        {
            if (item is not JsonObject itemDoCaminho)
            {
                continue;
            }

            foreach (var metodo in Metodos)
            {
                if (itemDoCaminho[metodo] is JsonObject operacao && operacao["operationId"]?.GetValue<string>() is { } id)
                {
                    indice[id] = new Operacao(id, metodo, caminho, operacao, itemDoCaminho["parameters"]);
                }
            }
        }

        return indice;
    }

    // ----- referências -----

    private static JsonNode? Resolver(JsonNode documento, string referencia)
    {
        if (!referencia.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        JsonNode? atual = documento;
        foreach (var parte in referencia[2..].Split('/'))
        {
            var nome = parte.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            atual = atual is JsonObject objeto ? objeto[nome] : null;
            if (atual is null)
            {
                return null;
            }
        }

        return atual;
    }

    /// <summary>Segue <c>$ref</c> até um nó de verdade (com limite, para não travar numa referência que aponta para si mesma).</summary>
    private static JsonNode? Desreferenciar(JsonNode documento, JsonNode? no)
    {
        for (var i = 0; i < 20 && no is JsonObject objeto && objeto["$ref"]?.GetValue<string>() is { } referencia; i++)
        {
            no = Resolver(documento, referencia);
        }

        return no;
    }

    // ----- operação -----

    private static void CompararOperacao(string id, JsonNode docContrato, Operacao c, JsonNode docApi, Operacao g, List<string> saida)
    {
        CompararCorpoDaRequisicao(id, docContrato, c.No, docApi, g.No, saida);
        CompararRespostas(id, docContrato, c.No, docApi, g.No, saida);
        CompararParametros(id, docContrato, c, docApi, g, saida);
    }

    /// <summary>`required: true` vem como texto no YAML convertido e como boolean no JSON gerado; as duas formas são "sim".</summary>
    private static bool Obrigatorio(JsonNode? no) =>
        string.Equals(no?["required"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

    private static JsonObject? Conteudo(JsonNode documento, JsonNode? no) =>
        Desreferenciar(documento, no) is JsonObject objeto ? objeto["content"] as JsonObject : null;

    private static void CompararCorpoDaRequisicao(string id, JsonNode docC, JsonObject opC, JsonNode docG, JsonObject opG, List<string> saida)
    {
        var corpoC = Desreferenciar(docC, opC["requestBody"]);
        var corpoG = Desreferenciar(docG, opG["requestBody"]);
        if (corpoC is null && corpoG is null)
        {
            return;
        }

        if (corpoC is null || corpoG is null)
        {
            saida.Add($"{id}: o corpo da requisição existe só {(corpoC is null ? "na API" : "no contrato")}.");
            return;
        }

        var obrigatorioC = Obrigatorio(corpoC);
        var obrigatorioG = Obrigatorio(corpoG);
        if (obrigatorioC != obrigatorioG)
        {
            saida.Add($"{id}: o corpo da requisição é {(obrigatorioC ? "obrigatório" : "opcional")} no contrato e {(obrigatorioG ? "obrigatório" : "opcional")} na API.");
        }

        CompararMidias(id, "do corpo da requisição", docC, Conteudo(docC, corpoC), docG, Conteudo(docG, corpoG), ["application/json", "multipart/form-data"], saida);
    }

    private static void CompararRespostas(string id, JsonNode docC, JsonObject opC, JsonNode docG, JsonObject opG, List<string> saida)
    {
        var respostasC = opC["responses"] as JsonObject ?? [];
        var respostasG = opG["responses"] as JsonObject ?? [];
        var codigosC = respostasC.Select(r => r.Key).ToHashSet();
        var codigosG = respostasG.Select(r => r.Key).ToHashSet();

        foreach (var codigo in codigosC.Except(codigosG).Order())
        {
            saida.Add($"{id}: o contrato tem a resposta {codigo}, e a API não declara.");
        }

        foreach (var codigo in codigosG.Except(codigosC).Order())
        {
            saida.Add($"{id}: a API declara a resposta {codigo}, e o contrato não tem.");
        }

        foreach (var codigo in codigosC.Intersect(codigosG).Order())
        {
            var tipos = codigo.StartsWith('2') ? new[] { "application/json" } : [];
            CompararMidias(id, $"da resposta {codigo}", docC, Conteudo(docC, respostasC[codigo]), docG, Conteudo(docG, respostasG[codigo]), tipos, saida);
        }
    }

    /// <summary>Compara o conjunto de tipos de mídia e, nos tipos pedidos, o schema.</summary>
    private static void CompararMidias(string id, string onde, JsonNode docC, JsonObject? conteudoC, JsonNode docG, JsonObject? conteudoG, string[] midiasComSchema, List<string> saida)
    {
        var midiasC = conteudoC?.Select(m => m.Key).ToHashSet() ?? [];
        var midiasG = conteudoG?.Select(m => m.Key).ToHashSet() ?? [];
        if (!midiasC.SetEquals(midiasG))
        {
            saida.Add($"{id}: os tipos de mídia {onde} são diferentes: contrato [{string.Join(", ", midiasC.Order())}], API [{string.Join(", ", midiasG.Order())}].");
        }

        foreach (var midia in midiasComSchema.Where(m => midiasC.Contains(m) && midiasG.Contains(m)))
        {
            var a = Canonizar(docC, (conteudoC![midia] as JsonObject)?["schema"], new Stack<string>());
            var b = Canonizar(docG, (conteudoG![midia] as JsonObject)?["schema"], new Stack<string>());
            CompararSchema($"{id}: {midia} {onde}", a, b, saida);
        }
    }

    private static void CompararParametros(string id, JsonNode docC, Operacao c, JsonNode docG, Operacao g, List<string> saida)
    {
        var paramsC = Parametros(docC, c);
        var paramsG = Parametros(docG, g);
        foreach (var chave in paramsC.Keys.Except(paramsG.Keys).Order())
        {
            saida.Add($"{id}: o parâmetro {chave} existe no contrato, e a API não o declara.");
        }

        foreach (var chave in paramsG.Keys.Except(paramsC.Keys).Order())
        {
            saida.Add($"{id}: o parâmetro {chave} existe na API, e o contrato não o tem.");
        }

        foreach (var chave in paramsC.Keys.Intersect(paramsG.Keys).Order())
        {
            var (obrigatorioC, canonicoC) = paramsC[chave];
            var (obrigatorioG, canonicoG) = paramsG[chave];
            var tiposC = canonicoC.Tipos;
            var tiposG = canonicoG.Tipos;
            if (obrigatorioC != obrigatorioG)
            {
                saida.Add($"{id}: o parâmetro {chave} é {(obrigatorioC ? "obrigatório" : "opcional")} no contrato e {(obrigatorioG ? "obrigatório" : "opcional")} na API.");
            }

            if (!tiposC.SetEquals(tiposG))
            {
                saida.Add($"{id}: o tipo do parâmetro {chave} é [{string.Join(", ", tiposC.Order())}] no contrato e [{string.Join(", ", tiposG.Order())}] na API.");
            }

            if (canonicoC.Formato != canonicoG.Formato)
            {
                saida.Add($"{id}: o formato do parâmetro {chave} é '{canonicoC.Formato ?? "(nenhum)"}' no contrato e '{canonicoG.Formato ?? "(nenhum)"}' na API.");
            }

            if (!canonicoC.Valores.SetEquals(canonicoG.Valores))
            {
                saida.Add($"{id}: os valores do enum do parâmetro {chave} são [{string.Join(", ", canonicoC.Valores.Order())}] no contrato e [{string.Join(", ", canonicoG.Valores.Order())}] na API.");
            }
        }
    }

    private static Dictionary<string, (bool Obrigatorio, Canonico Schema)> Parametros(JsonNode documento, Operacao operacao)
    {
        var resultado = new Dictionary<string, (bool, Canonico)>();
        foreach (var lista in new[] { operacao.ParametrosDoCaminho, operacao.No["parameters"] })
        {
            if (lista is not JsonArray itens)
            {
                continue;
            }

            foreach (var item in itens)
            {
                if (Desreferenciar(documento, item) is not JsonObject parametro)
                {
                    continue;
                }

                var nome = parametro["name"]?.GetValue<string>() ?? string.Empty;
                var onde = parametro["in"]?.GetValue<string>() ?? string.Empty;
                var obrigatorio = onde == "path" || parametro["required"]?.ToString() == "true";
                resultado[$"{onde}:{nome}"] = (obrigatorio, Canonizar(documento, parametro["schema"], new Stack<string>()));
            }
        }

        return resultado;
    }

    // ----- schema canônico -----

    private static bool EhNulo(JsonNode? no) =>
        no is JsonObject objeto && objeto["type"]?.ToString() == "null" && objeto["$ref"] is null;

    private static Canonico Canonizar(JsonNode documento, JsonNode? no, Stack<string> pilha)
    {
        if (no is not JsonObject objeto)
        {
            return new Canonico();
        }

        if (objeto["$ref"]?.GetValue<string>() is { } referencia)
        {
            if (pilha.Contains(referencia))
            {
                return new Canonico { Ciclo = referencia };
            }

            pilha.Push(referencia);
            try
            {
                return Canonizar(documento, Resolver(documento, referencia), pilha);
            }
            finally
            {
                pilha.Pop();
            }
        }

        var resultado = new Canonico();

        if (objeto["allOf"] is JsonArray todos)
        {
            foreach (var membro in todos)
            {
                Mesclar(resultado, Canonizar(documento, membro, pilha));
            }
        }

        if ((objeto["oneOf"] ?? objeto["anyOf"]) is JsonArray alternativas)
        {
            // oneOf [{type: 'null'}, X] (em qualquer ordem) é "X ou null": a mesma coisa que type [x, 'null'] e nullable.
            foreach (var alternativa in alternativas.Where(a => !EhNulo(a)))
            {
                Mesclar(resultado, Canonizar(documento, alternativa, pilha));
            }

            if (alternativas.Any(EhNulo))
            {
                resultado.Tipos.Add("null");
            }
        }

        AplicarProprios(resultado, objeto, documento, pilha);
        return resultado;
    }

    private static void AplicarProprios(Canonico resultado, JsonObject objeto, JsonNode documento, Stack<string> pilha)
    {
        switch (objeto["type"])
        {
            case JsonArray tipos:
                foreach (var tipo in tipos)
                {
                    resultado.Tipos.Add(tipo!.ToString());
                }

                break;
            case JsonNode tipo:
                resultado.Tipos.Add(tipo.ToString());
                break;
        }

        if (objeto["nullable"]?.ToString() == "true")
        {
            resultado.Tipos.Add("null");
        }

        if (objeto["properties"] is JsonObject propriedades)
        {
            foreach (var (nome, schema) in propriedades)
            {
                resultado.Propriedades[nome] = Canonizar(documento, schema, pilha);
            }
        }

        if (objeto["required"] is JsonArray obrigatorias)
        {
            foreach (var nome in obrigatorias)
            {
                resultado.Obrigatorias.Add(nome!.ToString());
            }
        }

        if (objeto["items"] is not null)
        {
            resultado.Itens = Canonizar(documento, objeto["items"], pilha);
        }

        if (objeto["format"]?.ToString() is "date-time" or "date" or "binary")
        {
            resultado.Formato = objeto["format"]!.ToString();
        }

        if (objeto["enum"] is JsonArray valores)
        {
            foreach (var valor in valores)
            {
                if (valor is null)
                {
                    // Um null DENTRO do enum (como o gerador do .NET escreve o enum anulável) quer dizer "aceita null", o mesmo que o contrato diz com `oneOf` de uma referência e `null`:
                    // conta como tipo, não como um valor a mais do enum.
                    resultado.Tipos.Add("null");
                    continue;
                }

                resultado.Valores.Add(valor.ToString());
            }

            if (objeto["type"] is null)
            {
                resultado.Tipos.Add("string"); // enum sem type, como o gerador do .NET escreve o enum em texto
            }
        }

        if (resultado.Tipos.Count == 0 && resultado.Propriedades.Count > 0)
        {
            resultado.Tipos.Add("object");
        }

        if (resultado.Tipos.Count == 0 && resultado.Itens is not null)
        {
            resultado.Tipos.Add("array");
        }
    }

    private static void Mesclar(Canonico destino, Canonico origem)
    {
        destino.Tipos.UnionWith(origem.Tipos);
        foreach (var (nome, propriedade) in origem.Propriedades)
        {
            destino.Propriedades[nome] = propriedade;
        }

        destino.Obrigatorias.UnionWith(origem.Obrigatorias);
        destino.Itens ??= origem.Itens;
        destino.Formato ??= origem.Formato;
        destino.Valores.UnionWith(origem.Valores);
        destino.Ciclo ??= origem.Ciclo;
    }

    private static void CompararSchema(string onde, Canonico contrato, Canonico api, List<string> saida)
    {
        if (contrato.Ciclo is not null || api.Ciclo is not null)
        {
            return; // referência circular: os dois lados já foram comparados até aqui; não se desce de novo
        }

        var tiposC = contrato.Tipos.Where(t => t != "null").ToHashSet();
        var tiposG = api.Tipos.Where(t => t != "null").ToHashSet();
        if (!tiposC.SetEquals(tiposG))
        {
            saida.Add($"{onde}: o tipo no contrato é [{string.Join(", ", tiposC.Order())}] e na API é [{string.Join(", ", tiposG.Order())}].");
        }

        if (contrato.Tipos.Contains("null") != api.Tipos.Contains("null"))
        {
            saida.Add($"{onde}: o contrato {(contrato.Tipos.Contains("null") ? "aceita" : "não aceita")} null e a API {(api.Tipos.Contains("null") ? "aceita" : "não aceita")}.");
        }

        if (contrato.Formato != api.Formato)
        {
            saida.Add($"{onde}: o formato no contrato é '{contrato.Formato ?? "(nenhum)"}' e na API é '{api.Formato ?? "(nenhum)"}'.");
        }

        if (!contrato.Valores.SetEquals(api.Valores))
        {
            saida.Add($"{onde}: os valores do enum são [{string.Join(", ", contrato.Valores.Order())}] no contrato e [{string.Join(", ", api.Valores.Order())}] na API.");
        }

        foreach (var nome in contrato.Propriedades.Keys.Except(api.Propriedades.Keys))
        {
            saida.Add($"{onde}: o campo '{nome}' existe no contrato, e a API não o tem.");
        }

        foreach (var nome in api.Propriedades.Keys.Except(contrato.Propriedades.Keys))
        {
            saida.Add($"{onde}: o campo '{nome}' existe na API, e o contrato não o tem.");
        }

        if (!contrato.Obrigatorias.SetEquals(api.Obrigatorias))
        {
            saida.Add($"{onde}: os campos obrigatórios (required) são [{string.Join(", ", contrato.Obrigatorias.Order())}] no contrato e [{string.Join(", ", api.Obrigatorias.Order())}] na API.");
        }

        foreach (var nome in contrato.Propriedades.Keys.Intersect(api.Propriedades.Keys))
        {
            CompararSchema($"{onde} > {nome}", contrato.Propriedades[nome], api.Propriedades[nome], saida);
        }

        if (contrato.Itens is not null && api.Itens is not null)
        {
            CompararSchema($"{onde} > (itens)", contrato.Itens, api.Itens, saida);
        }
        else if ((contrato.Itens is null) != (api.Itens is null))
        {
            saida.Add($"{onde}: os itens do array existem só {(contrato.Itens is null ? "na API" : "no contrato")}.");
        }
    }
}
