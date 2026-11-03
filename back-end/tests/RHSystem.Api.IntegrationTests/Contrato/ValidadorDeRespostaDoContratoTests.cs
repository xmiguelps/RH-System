using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// O validador é pequeno de propósito: tipo (com <c>null</c> só onde o schema aceita), <c>required</c>, chaves que o schema não declara, <c>enum</c>,
/// <c>date-time</c> terminando em <c>Z</c>, <c>date</c>, itens de <c>array</c>, <c>oneOf</c> e <c>allOf</c>. Os schemas dos testes são os já resolvidos
/// (sem <c>$ref</c>), como o validador os recebe.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ValidadorDeRespostaDoContratoTests(MySqlFixture mysql)
{
    private static JsonNode Schema(string json) => JsonNode.Parse(json)!;

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static IReadOnlyList<string> Validar(string schema, string json) => ValidadorDeRespostaDoContrato.Validar(Schema(schema), Json(json));

    [Fact]
    public async Task Validador_aceita_a_resposta_real_de_entrar()
    {
        // Controle positivo com o contrato verdadeiro: o login do RH semeado, validado contra a resposta 200 de `entrar`.
        var (api, _) = await ApiComElenco.ObterAsync(mysql);

        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/entrar", new { email = ApiFactory.EmailRh, senha = ApiFactory.SenhaPadrao });

        Assert.Equal(System.Net.HttpStatusCode.OK, resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("entrar");
    }

    [Fact]
    public async Task Resposta_que_nao_bate_com_o_contrato_e_acusada_com_o_caminho_do_campo()
    {
        // Controle negativo com a API verdadeira: a resposta de /api/auth/eu validada contra o schema de OUTRA operação (entrar) tem de falhar.
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);
        var resposta = await api.ClienteComo(elenco.Rh).GetAsync("/api/auth/eu");

        var erro = await Assert.ThrowsAnyAsync<Exception>(() => resposta.AfirmarConformeAoContratoAsync("entrar"));

        Assert.Contains("tokenDeAcesso", erro.Message);
    }

    [Fact]
    public void Validador_acusa_campo_a_mais_campo_faltando_tipo_errado_enum_invalido_e_data_sem_Z()
    {
        const string schema = """
            { "type": "object", "required": ["id", "nome"], "properties": {
                "id": { "type": "integer" },
                "nome": { "type": "string" },
                "situacao": { "type": "string", "enum": ["ATIVO", "INATIVO"] },
                "criadoEm": { "type": "string", "format": "date-time" },
                "dia": { "type": "string", "format": "date" } } }
            """;

        Assert.Empty(Validar(schema, """{ "id": 1, "nome": "Ana", "situacao": "ATIVO", "criadoEm": "2026-10-07T12:42:00Z", "dia": "2026-10-07" }"""));

        var maisUm = Validar(schema, """{ "id": 1, "nome": "Ana", "segredo": "x" }""");
        Assert.Contains(maisUm, p => p.Contains("segredo") && p.Contains("não declara"));

        var faltando = Validar(schema, """{ "id": 1 }""");
        Assert.Contains(faltando, p => p.Contains("nome") && p.Contains("obrigatório"));

        var tipoErrado = Validar(schema, """{ "id": "1", "nome": "Ana" }""");
        Assert.Contains(tipoErrado, p => p.Contains("$.id") && p.Contains("integer"));

        var enumInvalido = Validar(schema, """{ "id": 1, "nome": "Ana", "situacao": "Ativo" }""");
        Assert.Contains(enumInvalido, p => p.Contains("$.situacao") && p.Contains("ATIVO"));

        var semZ = Validar(schema, """{ "id": 1, "nome": "Ana", "criadoEm": "2026-10-07T09:42:00-03:00" }""");
        Assert.Contains(semZ, p => p.Contains("$.criadoEm") && p.Contains("Z"));

        var dataRuim = Validar(schema, """{ "id": 1, "nome": "Ana", "dia": "07/10/2026" }""");
        Assert.Contains(dataRuim, p => p.Contains("$.dia") && p.Contains("yyyy-MM-dd"));
    }

    [Fact]
    public void Validador_aceita_null_onde_o_schema_permite_e_acusa_onde_nao_permite()
    {
        const string schema = """
            { "type": "object", "required": ["a", "b"], "properties": {
                "a": { "type": ["string", "null"] },
                "b": { "type": "string" } } }
            """;

        Assert.Empty(Validar(schema, """{ "a": null, "b": "x" }"""));
        Assert.Contains(Validar(schema, """{ "a": "x", "b": null }"""), p => p.Contains("$.b") && p.Contains("null"));
        Assert.Contains(Validar(schema, """{ "a": 3, "b": "x" }"""), p => p.Contains("$.a"));
    }

    [Fact]
    public void Validador_trata_oneOf_com_null_como_o_contrato_de_cargo_e_setor()
    {
        // As duas formas de escrever "texto ou null" que o contrato e o gerador usam.
        const string comOneOf = """{ "oneOf": [ { "type": "string" }, { "type": "null" } ] }""";
        const string comTipoLista = """{ "type": ["string", "null"] }""";

        foreach (var schema in new[] { comOneOf, comTipoLista })
        {
            Assert.Empty(Validar(schema, "\"Comercial\""));
            Assert.Empty(Validar(schema, "null"));
            Assert.NotEmpty(Validar(schema, "42"));
        }
    }

    [Fact]
    public void Validador_trata_allOf_de_Paginacao_mais_itens()
    {
        // O contrato usa allOf com Paginacao em toda lista paginada: sem unir properties e required dos membros, "total" e "itens" virariam chaves não declaradas.
        const string schema = """
            { "allOf": [
                { "type": "object", "required": ["pagina", "tamanhoPagina", "total", "totalPaginas"], "properties": {
                    "pagina": { "type": "integer" }, "tamanhoPagina": { "type": "integer" }, "total": { "type": "integer" }, "totalPaginas": { "type": "integer" } } },
                { "type": "object", "required": ["itens"], "properties": { "itens": { "type": "array", "items": { "type": "object", "required": ["id"], "properties": { "id": { "type": "integer" } } } } } } ] }
            """;

        Assert.Empty(Validar(schema, """{ "itens": [ { "id": 1 }, { "id": 2 } ], "pagina": 1, "tamanhoPagina": 20, "total": 2, "totalPaginas": 1 }"""));
        Assert.Contains(Validar(schema, """{ "itens": [], "pagina": 1, "tamanhoPagina": 20, "totalPaginas": 0 }"""), p => p.Contains("total") && p.Contains("obrigatório"));
        Assert.Contains(Validar(schema, """{ "pagina": 1, "tamanhoPagina": 20, "total": 0, "totalPaginas": 0 }"""), p => p.Contains("itens") && p.Contains("obrigatório"));
        Assert.Contains(Validar(schema, """{ "itens": [], "pagina": 1, "tamanhoPagina": 20, "total": 0, "totalPaginas": 0, "extra": 1 }"""), p => p.Contains("extra") && p.Contains("não declara"));
        Assert.Contains(Validar(schema, """{ "itens": [ { "id": "x" } ], "pagina": 1, "tamanhoPagina": 20, "total": 1, "totalPaginas": 1 }"""), p => p.Contains("$.itens[0].id"));
    }

    [Fact]
    public void Validador_com_additionalProperties_aceita_as_chaves_do_mapa_e_valida_o_valor()
    {
        const string schema = """{ "type": "object", "additionalProperties": { "type": "array", "items": { "type": "string" } } }""";

        Assert.Empty(Validar(schema, """{ "email": ["O e-mail é obrigatório."], "senha": [] }"""));
        Assert.Contains(Validar(schema, """{ "email": "texto" }"""), p => p.Contains("$.email"));
    }
}
