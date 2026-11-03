using System.Net;
using System.Text.Json.Nodes;
using RHSystem.Api.IntegrationTests.Infra;
using Xunit.Abstractions;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// SONDA: mede o que o gerador OpenAPI nativo do .NET 10 escreve, porque o comparador e o teste de divergência se apoiam nisso. Depois do
/// ajuste ficam como regressão do gerador. O que cada um mediu vem no <c>docs/</c> do README (Task 9, Step 6).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SondaDoOpenApiNativoTests(MySqlFixture mysql, ITestOutputHelper saida)
{
    private async Task<JsonNode> DocumentoAsync()
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);
        return JsonNode.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json"))!;
    }

    [Fact]
    public async Task Documento_declara_openapi_3_1()
    {
        var versao = (await DocumentoAsync())["openapi"]!.GetValue<string>();

        saida.WriteLine($"openapi = {versao}"); // medido: 3.1.1 (o contrato é 3.1.0; mesma família, só o número do patch muda)
        Assert.StartsWith("3.1", versao);
    }

    [Fact]
    public async Task Propriedades_nao_anulaveis_dos_records_saem_em_required()
    {
        var esquemas = (await DocumentoAsync())["components"]!["schemas"]!;

        string[] Required(string schema) => esquemas[schema]!["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        Assert.Equal(["acessoExpiraEmUtc", "refreshToken", "tokenDeAcesso", "usuario"], Required("SessaoResposta").Order());
        Assert.Equal(["cargo", "email", "funcionarioId", "id", "nome", "perfil", "setor"], Required("UsuarioResposta").Order());
        Assert.Equal(["email", "senha"], Required("EntrarComando").Order());
        Assert.Equal(["refreshToken"], Required("RenovarComando"));
    }

    [Fact]
    public async Task Propriedade_anulavel_sai_como_type_com_null_ou_oneOf_com_null()
    {
        var propriedades = (await DocumentoAsync())["components"]!["schemas"]!["UsuarioResposta"]!["properties"]!;

        foreach (var nome in new[] { "cargo", "setor" })
        {
            var propriedade = propriedades[nome]!;
            saida.WriteLine($"{nome} = {propriedade.ToJsonString()}"); // medido: { "type": ["null", "string"] }
            var comTypeArray = propriedade["type"] is JsonArray tipos && tipos.Any(t => t!.GetValue<string>() == "null");
            var comOneOf = propriedade["oneOf"] is JsonArray alternativas && alternativas.Any(a => a?["type"]?.ToString() == "null");
            Assert.True(comTypeArray || comOneOf, $"{nome} não sai como anulável: {propriedade.ToJsonString()}");
        }
    }

    [Fact]
    public async Task Enum_em_texto_sai_como_enum_de_strings()
    {
        var perfil = (await DocumentoAsync())["components"]!["schemas"]!["Perfil"]!;

        saida.WriteLine($"Perfil = {perfil.ToJsonString()}"); // medido: { "enum": ["Funcionario", "Gestor", "RH"] }
        Assert.Equal(["Funcionario", "Gestor", "RH"], perfil["enum"]!.AsArray().Select(v => v!.GetValue<string>()).Order());
        Assert.Equal("#/components/schemas/Perfil", (await DocumentoAsync())["components"]!["schemas"]!["UsuarioResposta"]!["properties"]!["perfil"]!["$ref"]!.GetValue<string>());
    }

    [Fact]
    public async Task Inteiros_saem_como_integer_e_nao_como_integer_ou_string()
    {
        // O .NET aceita número escrito como texto por padrão e o documento passa a dizer ["integer","string"]; a API é estrita (Strict).
        var id = (await DocumentoAsync())["components"]!["schemas"]!["UsuarioResposta"]!["properties"]!["id"]!;

        saida.WriteLine($"id = {id.ToJsonString()}"); // medido: { "type": "integer", "format": "int32" }
        Assert.Equal("integer", id["type"]!.ToString());
    }

    [Fact]
    public async Task Corpo_da_requisicao_sai_obrigatorio_e_sem_null()
    {
        var caminhos = (await DocumentoAsync())["paths"]!;

        foreach (var (rota, esquema) in new[] { ("/api/auth/entrar", "EntrarComando"), ("/api/auth/renovar", "RenovarComando"), ("/api/auth/sair", "RenovarComando") })
        {
            var corpo = caminhos[rota]!["post"]!["requestBody"]!;
            Assert.Equal("true", corpo["required"]!.ToString());
            Assert.Equal($"#/components/schemas/{esquema}", corpo["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Respostas_de_erro_saem_como_application_problem_json()
    {
        var respostas = (await DocumentoAsync())["paths"]!["/api/auth/entrar"]!["post"]!["responses"]!.AsObject();

        Assert.Equal(["200", "400", "401", "403", "429", "500"], respostas.Select(r => r.Key).Order());
        foreach (var codigo in new[] { "400", "401", "403", "429", "500" })
        {
            Assert.NotNull(respostas[codigo]!["content"]!["application/problem+json"]);
        }

        Assert.NotNull(respostas["200"]!["content"]!["application/json"]);
    }

    [Fact]
    public async Task Rota_do_documento_e_openapi_v1_json_e_nao_pede_login()
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);

        var resposta = await api.CreateClient().GetAsync("/openapi/v1.json"); // sem Authorization

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
    }
}
