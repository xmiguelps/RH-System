using System.Text.Json.Nodes;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>O comparador não pode passar em branco: documentos pequenos montados aqui, cada um com a divergência que ele deve acusar.</summary>
public class ComparadorDeContratoTests
{
    private static JsonNode J(string json) => JsonNode.Parse(json)!;

    private static JsonNode Doc(string caminhos, string componentes = "{}") =>
        J($$"""{ "openapi": "3.1.0", "paths": {{caminhos}}, "components": {{componentes}} }""");

    private static IReadOnlySet<string> Escopo(params string[] ids) => ids.ToHashSet();

    private static IReadOnlyList<string> Comparar(JsonNode contrato, JsonNode api, params string[] ids) =>
        ComparadorDeContrato.Comparar(contrato, api, Escopo(ids));

    /// <summary>Uma operação GET com resposta 200 em JSON com o schema dado.</summary>
    private static JsonNode DocComSchema(string schema, string operationId = "op") =>
        Doc($$"""{ "/x": { "get": { "operationId": "{{operationId}}", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": {{schema}} } } } } } } }""");

    [Fact]
    public void Enum_com_null_dentro_equivale_ao_oneOf_de_enum_e_null_do_contrato_e_a_nulabilidade_diferente_ainda_diverge()
    {
        // O gerador do .NET escreve o enum anulável com null DENTRO da lista; o contrato o escreve com oneOf [enum, null]: é a mesma coisa.
        var contrato = DocComSchema("""{ "oneOf": [ { "type": "string", "enum": ["A", "B"] }, { "type": "null" } ] }""");
        var api = DocComSchema("""{ "type": ["string", "null"], "enum": ["A", "B", null] }""");
        var semNull = DocComSchema("""{ "type": "string", "enum": ["A", "B"] }""");
        var outroValor = DocComSchema("""{ "type": ["string", "null"], "enum": ["A", "C", null] }""");

        Assert.Empty(Comparar(contrato, api, "op"));
        Assert.Contains(Comparar(contrato, semNull, "op"), d => d.Contains("null")); // o contrato aceita null e a API não
        Assert.Contains(Comparar(contrato, outroValor, "op"), d => d.Contains("valores do enum"));
    }

    [Fact]
    public void Comparador_acusa_operacao_que_so_existe_de_um_lado()
    {
        var com = Doc("""{ "/a": { "get": { "operationId": "a", "responses": { "200": { "description": "ok" } } } } }""");
        var sem = Doc("{}");

        Assert.Contains(Comparar(com, sem, "a"), d => d.StartsWith("a:") && d.Contains("a API não a tem"));
        Assert.Contains(Comparar(sem, com, "a"), d => d.StartsWith("a:") && d.Contains("existe na API"));
        Assert.Contains(Comparar(sem, sem, "a"), d => d.Contains("nem no contrato nem na API"));
        Assert.Empty(Comparar(com, sem)); // fora do escopo, ninguém é cobrado
        Assert.Empty(Comparar(com, com, "a"));
    }

    [Fact]
    public void Comparador_acusa_codigo_de_resposta_a_mais_ou_a_menos_e_tipo_de_midia_diferente()
    {
        var contrato = Doc("""{ "/a": { "post": { "operationId": "a", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } }, "401": { "description": "x", "content": { "application/problem+json": { "schema": { "type": "object" } } } } } } } }""");
        var semO401 = Doc("""{ "/a": { "post": { "operationId": "a", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } } } }""");
        var com500 = Doc("""{ "/a": { "post": { "operationId": "a", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } }, "401": { "description": "x", "content": { "application/problem+json": { "schema": { "type": "object" } } } }, "500": { "description": "y" } } } } }""");
        var midiaErrada = Doc("""{ "/a": { "post": { "operationId": "a", "responses": { "200": { "description": "ok", "content": { "text/plain": { "schema": { "type": "string" } } } }, "401": { "description": "x", "content": { "application/problem+json": { "schema": { "type": "object" } } } } } } } }""");

        Assert.Contains(Comparar(contrato, semO401, "a"), d => d.Contains("tem a resposta 401"));
        Assert.Contains(Comparar(contrato, com500, "a"), d => d.Contains("declara a resposta 500"));
        Assert.Contains(Comparar(contrato, midiaErrada, "a"), d => d.Contains("tipos de mídia") && d.Contains("da resposta 200"));
    }

    [Fact]
    public void Comparador_acusa_campo_a_mais_campo_a_menos_tipo_diferente_required_diferente_e_nulabilidade_diferente()
    {
        var contrato = DocComSchema("""{ "type": "object", "required": ["a"], "properties": { "a": { "type": "string" }, "b": { "type": "integer" } } }""");

        Assert.Contains(Comparar(contrato, DocComSchema("""{ "type": "object", "required": ["a"], "properties": { "a": { "type": "string" } } }"""), "op"), d => d.Contains("campo 'b' existe no contrato"));
        Assert.Contains(Comparar(contrato, DocComSchema("""{ "type": "object", "required": ["a"], "properties": { "a": { "type": "string" }, "b": { "type": "integer" }, "c": { "type": "string" } } }"""), "op"), d => d.Contains("campo 'c' existe na API"));
        Assert.Contains(Comparar(contrato, DocComSchema("""{ "type": "object", "required": ["a"], "properties": { "a": { "type": "integer" }, "b": { "type": "integer" } } }"""), "op"), d => d.Contains("> a") && d.Contains("tipo"));
        Assert.Contains(Comparar(contrato, DocComSchema("""{ "type": "object", "required": [], "properties": { "a": { "type": "string" }, "b": { "type": "integer" } } }"""), "op"), d => d.Contains("obrigatórios"));
        Assert.Contains(Comparar(contrato, DocComSchema("""{ "type": "object", "required": ["a"], "properties": { "a": { "type": ["string", "null"] }, "b": { "type": "integer" } } }"""), "op"), d => d.Contains("> a") && d.Contains("null"));
        Assert.Empty(Comparar(contrato, DocComSchema("""{ "type": "object", "required": ["a"], "properties": { "b": { "type": "integer" }, "a": { "type": "string" } } }"""), "op")); // a ordem não conta
    }

    [Fact]
    public void Comparador_acusa_operationId_diferente()
    {
        var contrato = DocComSchema("""{ "type": "string" }""", "entrar");
        var api = DocComSchema("""{ "type": "string" }""", "entrarX");

        Assert.Contains(Comparar(contrato, api, "entrar"), d => d.Contains("operationId") && d.Contains("'entrar'") && d.Contains("'entrarX'"));
    }

    [Fact]
    public void Comparador_ignora_summary_description_example_minLength_e_extensoes_x()
    {
        var contrato = Doc("""{ "/x": { "get": { "operationId": "op", "summary": "Resumo", "description": "Texto longo", "tags": ["A"], "x-perfis": ["RH"], "security": [], "responses": { "200": { "description": "ok", "content": { "application/json": { "example": { "a": "b" }, "schema": { "type": "object", "description": "d", "example": {}, "x-provisorio": "?", "required": ["a"], "properties": { "a": { "type": "string", "minLength": 1, "maxLength": 9, "description": "campo" } } } } } } } } } }""");
        var api = Doc("""{ "/x": { "get": { "operationId": "op", "tags": ["B"], "responses": { "200": { "description": "OK", "content": { "application/json": { "schema": { "type": "object", "required": ["a"], "properties": { "a": { "type": "string" } } } } } } } } } }""");

        Assert.Empty(Comparar(contrato, api, "op"));
    }

    [Fact]
    public void Comparador_trata_type_com_null_nullable_e_oneOf_com_null_como_a_mesma_coisa()
    {
        var contrato = DocComSchema("""{ "type": "object", "properties": { "a": { "type": ["string", "null"] } } }""");

        foreach (var forma in new[]
        {
            """{ "oneOf": [{ "type": "null" }, { "type": "string" }] }""",
            """{ "oneOf": [{ "type": "string" }, { "type": "null" }] }""",
            """{ "type": "string", "nullable": true }""",
            """{ "type": ["null", "string"] }""",
        })
        {
            Assert.Empty(Comparar(contrato, DocComSchema($$"""{ "type": "object", "properties": { "a": {{forma}} } }"""), "op"));
        }

        // Com $ref, nas duas ordens (é assim que o contrato escreve o null de um $ref).
        const string componentes = """{ "schemas": { "X": { "type": "object", "properties": { "n": { "type": "integer" } } } } }""";
        var comRef = Doc(
            """{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "oneOf": [{ "$ref": "#/components/schemas/X" }, { "type": "null" }] } } } } } } } }""",
            componentes);
        var invertido = Doc(
            """{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "oneOf": [{ "type": "null" }, { "$ref": "#/components/schemas/X" }] } } } } } } } }""",
            componentes);
        Assert.Empty(Comparar(comRef, invertido, "op"));
        Assert.Contains(Comparar(comRef, Doc("""{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "$ref": "#/components/schemas/X" } } } } } } } }""", componentes), "op"), d => d.Contains("null"));
    }

    [Fact]
    public void Comparador_une_properties_e_required_de_allOf_e_acusa_campo_que_falta()
    {
        const string componentes = """{ "schemas": { "Paginacao": { "type": "object", "required": ["pagina", "total"], "properties": { "pagina": { "type": "integer" }, "total": { "type": "integer" } } } } }""";
        var contrato = Doc(
            """{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "allOf": [{ "$ref": "#/components/schemas/Paginacao" }, { "type": "object", "required": ["itens"], "properties": { "itens": { "type": "array", "items": { "type": "string" } } } }] } } } } } } } }""",
            componentes);

        var achatado = DocComSchema("""{ "type": "object", "required": ["pagina", "total", "itens"], "properties": { "pagina": { "type": "integer" }, "total": { "type": "integer" }, "itens": { "type": "array", "items": { "type": "string" } } } }""");
        var semTotal = DocComSchema("""{ "type": "object", "required": ["pagina", "itens"], "properties": { "pagina": { "type": "integer" }, "itens": { "type": "array", "items": { "type": "string" } } } }""");

        Assert.Empty(Comparar(contrato, achatado, "op"));
        Assert.Contains(Comparar(contrato, semTotal, "op"), d => d.Contains("campo 'total' existe no contrato"));

        // allOf de um membro só, com description ao lado do $ref: é o próprio membro.
        var comDescricao = Doc("""{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "allOf": [{ "$ref": "#/components/schemas/Paginacao" }], "description": "texto" } } } } } } } }""", componentes);
        var direto = Doc("""{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Paginacao" } } } } } } } }""", componentes);
        Assert.Empty(Comparar(comDescricao, direto, "op"));
    }

    [Fact]
    public void Comparador_compara_so_os_operationId_do_escopo_e_nao_confunde_caminho_que_e_prefixo_de_outro()
    {
        // /api/rh/beneficios (no escopo) e /api/rh/beneficios-adesoes (fora): só a primeira é comparada.
        JsonNode Documento(string tipoDasAdesoes) => Doc($$"""
            {
              "/api/rh/beneficios": { "get": { "operationId": "listarBeneficios", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "string" } } } } } } },
              "/api/rh/beneficios-adesoes": { "get": { "operationId": "listarAdesoes", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "{{tipoDasAdesoes}}" } } } } } } }
            }
            """);

        Assert.Empty(Comparar(Documento("integer"), Documento("string"), "listarBeneficios"));
        Assert.Contains(Comparar(Documento("integer"), Documento("string"), "listarAdesoes"), d => d.Contains("listarAdesoes") && d.Contains("tipo"));
    }

    [Fact]
    public void Comparador_resolve_ref_e_nao_trava_em_referencia_circular()
    {
        string Componentes(string tipoDoNome) => $$"""{ "schemas": { "No": { "type": "object", "properties": { "filho": { "$ref": "#/components/schemas/No" }, "nome": { "type": "{{tipoDoNome}}" } } } } }""";
        const string Caminhos = """{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "$ref": "#/components/schemas/No" } } } } } } } }""";

        Assert.Empty(Comparar(Doc(Caminhos, Componentes("string")), Doc(Caminhos, Componentes("string")), "op")); // termina e bate
        Assert.Contains(Comparar(Doc(Caminhos, Componentes("string")), Doc(Caminhos, Componentes("integer")), "op"), d => d.Contains("> nome"));

        // Uma referência que não se resolve não derruba o comparador: aparece como diferença de forma.
        var quebrado = Doc("""{ "/x": { "get": { "operationId": "op", "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "$ref": "#/components/schemas/NaoExiste" } } } } } } } }""");
        Assert.NotEmpty(Comparar(Doc(Caminhos, Componentes("string")), quebrado, "op"));
    }

    [Fact]
    public void Comparador_compara_multipart_form_data_pelos_nomes_dos_campos_e_required_e_o_arquivo_como_binary()
    {
        // A fatia 1c depende deste teste: o envio de arquivo é multipart.
        JsonNode Documento(string requestBody) => Doc($$"""{ "/docs": { "post": { "operationId": "enviar", "requestBody": {{requestBody}}, "responses": { "204": { "description": "ok" } } } } }""");
        string Multipart(string schema) => $$"""{ "required": true, "content": { "multipart/form-data": { "schema": {{schema}} } } }""";
        const string Completo = """{ "type": "object", "required": ["modo", "tipo"], "properties": { "modo": { "type": "string" }, "tipo": { "type": "string" }, "descricao": { "type": "string" }, "dataValidade": { "type": "string", "format": "date" }, "arquivo": { "type": "string", "format": "binary" } } }""";

        var contrato = Documento(Multipart(Completo));
        Assert.Empty(Comparar(contrato, Documento(Multipart(Completo)), "enviar"));
        Assert.Contains(Comparar(contrato, Documento(Multipart(Completo.Replace(""", "descricao": { "type": "string" }""", string.Empty))), "enviar"), d => d.Contains("campo 'descricao' existe no contrato"));
        Assert.Contains(Comparar(contrato, Documento(Multipart(Completo.Replace("""["modo", "tipo"]""", """["modo"]"""))), "enviar"), d => d.Contains("obrigatórios"));
        Assert.Contains(Comparar(contrato, Documento(Multipart(Completo.Replace("""{ "type": "string", "format": "binary" }""", """{ "type": "string" }"""))), "enviar"), d => d.Contains("> arquivo") && d.Contains("formato"));
        Assert.Contains(Comparar(contrato, Documento($$"""{ "required": true, "content": { "application/json": { "schema": {{Completo}} } } }"""), "enviar"), d => d.Contains("tipos de mídia do corpo da requisição"));
    }

    [Fact]
    public void Comparador_compara_nome_onde_obrigatoriedade_e_tipo_dos_parametros()
    {
        JsonNode Documento(string parametros, string noCaminho = "[]") => Doc($$"""{ "/x/{id}": { "parameters": {{noCaminho}}, "get": { "operationId": "op", "parameters": {{parametros}}, "responses": { "204": { "description": "ok" } } } } }""");
        const string Id = """{ "name": "id", "in": "path", "required": true, "schema": { "type": "integer" } }""";
        const string Busca = """{ "name": "busca", "in": "query", "schema": { "type": "string" } }""";

        var contrato = Documento($"[{Busca}]", $"[{Id}]"); // o parâmetro de caminho vem do nível do caminho
        Assert.Empty(Comparar(contrato, Documento($"[{Id}, {Busca}]"), "op"));
        Assert.Contains(Comparar(contrato, Documento($"[{Id}]"), "op"), d => d.Contains("query:busca") && d.Contains("existe no contrato"));
        Assert.Contains(Comparar(contrato, Documento($"[{Id}, {Busca.Replace("\"string\"", "\"integer\"")}]"), "op"), d => d.Contains("query:busca") && d.Contains("tipo"));
        Assert.Contains(Comparar(contrato, Documento($"[{Id}, {Busca.Replace("\"schema\"", "\"required\": true, \"schema\"")}]"), "op"), d => d.Contains("query:busca") && d.Contains("opcional"));
    }

    [Fact]
    public void Comparador_acusa_format_e_enum_diferentes_nos_parametros()
    {
        // Achado da revisão final: só o tipo do parâmetro era comparado; date x date-time e enum de query passavam em branco.
        JsonNode Documento(string schema) => Doc($$"""{ "/x": { "get": { "operationId": "op", "parameters": [{ "name": "q", "in": "query", "schema": {{schema}} }], "responses": { "204": { "description": "ok" } } } } }""");

        var contrato = Documento("""{ "type": "string", "format": "date" }""");
        Assert.Empty(Comparar(contrato, Documento("""{ "type": "string", "format": "date" }"""), "op"));
        Assert.Contains(Comparar(contrato, Documento("""{ "type": "string", "format": "date-time" }"""), "op"), d => d.Contains("query:q") && d.Contains("formato"));

        var comEnum = Documento("""{ "type": "string", "enum": ["ASC", "DESC"] }""");
        Assert.Empty(Comparar(comEnum, Documento("""{ "type": "string", "enum": ["DESC", "ASC"] }"""), "op"));
        Assert.Contains(Comparar(comEnum, Documento("""{ "type": "string", "enum": ["ASC"] }"""), "op"), d => d.Contains("query:q") && d.Contains("enum"));
    }

    [Fact]
    public void Comparador_acusa_corpo_obrigatorio_de_um_lado_e_opcional_do_outro()
    {
        JsonNode Documento(string required) => Doc($$"""{ "/x": { "post": { "operationId": "op", "requestBody": { {{required}} "content": { "application/json": { "schema": { "type": "object" } } } }, "responses": { "204": { "description": "ok" } } } } }""");

        Assert.Empty(Comparar(Documento("\"required\": \"true\","), Documento("\"required\": true,"), "op")); // YAML vira texto; JSON vira boolean: é a mesma coisa
        Assert.Contains(Comparar(Documento("\"required\": \"true\","), Documento(string.Empty), "op"), d => d.Contains("obrigatório no contrato") && d.Contains("opcional na API"));
        Assert.Contains(Comparar(Documento(string.Empty), Documento("\"required\": true,"), "op"), d => d.Contains("opcional no contrato") && d.Contains("obrigatório na API"));
    }

    [Fact]
    public void OperacoesSemOperationId_lista_a_rota_que_ficou_sem_WithName()
    {
        // Uma operação sem operationId não entra no índice nem no escopo: antes sumia dos dois testes em silêncio.
        var documento = Doc("""{ "/a": { "get": { "operationId": "a", "responses": {} }, "post": { "responses": {} } }, "/b": { "parameters": [], "delete": { "responses": {} } } }""");

        var semId = ComparadorDeContrato.OperacoesSemOperationId(documento);

        Assert.Equal(["DELETE /b", "POST /a"], semId.Order());
        Assert.Empty(ComparadorDeContrato.OperacoesSemOperationId(Doc("""{ "/a": { "get": { "operationId": "a", "responses": {} } } }""")));
    }

    [Fact]
    public void Comparador_acusa_quando_se_tira_o_campo_cargo_de_UsuarioResposta_do_contrato_real_em_memoria()
    {
        // Controle positivo com o contrato verdadeiro: sem falso positivo no que bate e com o alarme no que não bate.
        var real = LocalizadorDoContrato.CarregarContrato();
        var escopo = Escopo("entrar", "renovarSessao", "sair", "obterUsuarioLogado");
        Assert.Empty(ComparadorDeContrato.Comparar(real, real.DeepClone(), escopo));

        var alterado = real.DeepClone();
        var usuario = alterado["components"]!["schemas"]!["UsuarioResposta"]!.AsObject();
        usuario["properties"]!.AsObject().Remove("cargo");
        var obrigatorios = usuario["required"]!.AsArray();
        obrigatorios.Remove(obrigatorios.First(n => n!.ToString() == "cargo"));

        var divergencias = ComparadorDeContrato.Comparar(real, alterado, escopo);

        Assert.Contains(divergencias, d => d.Contains("campo 'cargo' existe no contrato"));
        Assert.Contains(divergencias, d => d.StartsWith("entrar:")); // aparece em todas as operações que devolvem UsuarioResposta
        Assert.Contains(divergencias, d => d.StartsWith("obterUsuarioLogado:"));
    }
}
