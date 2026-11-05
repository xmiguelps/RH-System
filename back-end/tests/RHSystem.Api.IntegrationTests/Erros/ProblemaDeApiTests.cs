using System.Text.Json;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Erros;

/// <summary>
/// A <c>ProblemaDeApiException</c> vira a resposta que o contrato descreve: título próprio, <c>codigo</c>, campos extras (<c>cadastroExistente</c>) e cabeçalhos (<c>Retry-After</c>).
/// Rotas de teste em <c>/__teste/erros/problema*</c>, sem login e fora do documento.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ProblemaDeApiTests(MySqlFixture mysql)
{
    private async Task<HttpClient> ClienteAsync() => (await ApiCompartilhada.ObterAsync(mysql)).CreateClient();

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta) =>
        JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());

    [Fact]
    public async Task ProblemaDeApi_vira_o_status_title_detail_e_codigo_pedidos_com_application_problem_json()
    {
        var resposta = await (await ClienteAsync()).GetAsync("/__teste/erros/problema");

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var corpo = await LerAsync(resposta);
        Assert.Equal(422, corpo.GetProperty("status").GetInt32());
        Assert.Equal("Link inválido ou vencido.", corpo.GetProperty("title").GetString()); // o título é o da rota, não o genérico do 422
        Assert.Equal("Este link não vale mais. Peça um novo ao RH ou use \"Esqueci minha senha\".", corpo.GetProperty("detail").GetString());
        Assert.Equal("LINK_INVALIDO_OU_VENCIDO", corpo.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task ProblemaDeApi_sem_codigo_nao_leva_a_propriedade_codigo()
    {
        var corpo = await LerAsync(await (await ClienteAsync()).GetAsync("/__teste/erros/problema-sem-codigo"));

        Assert.Equal("Título próprio.", corpo.GetProperty("title").GetString());
        Assert.False(corpo.TryGetProperty("codigo", out _));
    }

    [Fact]
    public async Task ProblemaDeApi_leva_as_extensoes_como_objeto_em_camelCase()
    {
        var resposta = await (await ClienteAsync()).GetAsync("/__teste/erros/problema-com-extensao");

        Assert.Equal(409, (int)resposta.StatusCode);
        var corpo = await LerAsync(resposta);
        // A chave foi dada como "CadastroExistente" e o objeto como propriedades em PascalCase: as duas saem em camelCase (o contrato: ColaboradorDuplicadoProblema).
        var existente = corpo.GetProperty("cadastroExistente");
        Assert.Equal(12, existente.GetProperty("funcionarioId").GetInt32());
        Assert.Equal("Bruno Lima", existente.GetProperty("nome").GetString());
        Assert.False(existente.GetProperty("ativo").GetBoolean());
        Assert.False(corpo.TryGetProperty("CadastroExistente", out _));
    }

    [Fact]
    public async Task ProblemaDeApi_nao_deixa_extensao_sobrescrever_title_status_detail_traceId_nem_codigo()
    {
        var resposta = await (await ClienteAsync()).GetAsync("/__teste/erros/problema-com-extensao-reservada");

        Assert.Equal(422, (int)resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        var corpo = JsonSerializer.Deserialize<JsonElement>(texto);
        Assert.Equal("Título de verdade.", corpo.GetProperty("title").GetString());
        Assert.Equal(422, corpo.GetProperty("status").GetInt32());
        Assert.Equal("Detalhe de verdade.", corpo.GetProperty("detail").GetString());
        Assert.Equal("CODIGO_DE_VERDADE", corpo.GetProperty("codigo").GetString());
        Assert.NotEqual("traceId-falso", corpo.GetProperty("traceId").GetString());
        Assert.False(corpo.TryGetProperty("type", out var tipo) && tipo.ValueKind == JsonValueKind.String && tipo.GetString() == "tipo-falso");
        Assert.False(corpo.TryGetProperty("instance", out _));
        Assert.Equal(5, corpo.GetProperty("outra").GetInt32()); // a extensão comum passa
        Assert.DoesNotContain("falso", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProblemaDeApi_sempre_tem_traceId_e_nunca_stack_trace()
    {
        var cliente = await ClienteAsync();

        foreach (var (rota, status) in new[] { ("problema", 422), ("problema-sem-codigo", 422), ("problema-com-extensao", 409), ("problema-429", 429) })
        {
            var resposta = await cliente.GetAsync($"/__teste/erros/{rota}");
            Assert.Equal(status, (int)resposta.StatusCode); // sem isto um 404 de rota que não existe passaria: ele também tem traceId e não tem stack trace
            var texto = await resposta.Content.ReadAsStringAsync();
            var corpo = JsonSerializer.Deserialize<JsonElement>(texto);
            Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("traceId").GetString()), rota);
            Assert.DoesNotContain("   at ", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("ProblemaDeApiException", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("RHSystem.", texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ProblemaDeApi_escreve_os_cabecalhos_pedidos_como_Retry_After_no_429()
    {
        var resposta = await (await ClienteAsync()).GetAsync("/__teste/erros/problema-429");

        Assert.Equal(429, (int)resposta.StatusCode);
        Assert.Equal("37", Assert.Single(resposta.Headers.GetValues("Retry-After")));
        var corpo = await LerAsync(resposta);
        Assert.Equal("Muitas tentativas.", corpo.GetProperty("title").GetString());
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }
}
