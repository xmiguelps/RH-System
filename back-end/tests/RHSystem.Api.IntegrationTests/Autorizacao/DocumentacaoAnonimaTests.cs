using System.Net;
using Microsoft.AspNetCore.Hosting;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Autorizacao.ApiDeAutorizacao;

namespace RHSystem.Api.IntegrationTests.Autorizacao;

/// <summary>O documento OpenAPI e a UI da documentação ficam fora das famílias, anônimos, e a regra de reserva (rota sem política exige login) os deixa assim.</summary>
[Collection(MySqlCollection.Nome)]
public class DocumentacaoAnonimaTests(MySqlFixture mysql)
{
    private static readonly string[] Rotas = ["/openapi/v1.json", "/scalar/v1"];

    [Fact]
    public async Task Documento_openapi_e_UI_da_documentacao_respondem_200_ao_anonimo_com_a_reserva_ligada()
    {
        var cliente = (await ObterAsync(mysql)).CreateClient(); // Documentacao:Habilitada = true, sem Authorization

        foreach (var rota in Rotas)
        {
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, rota)).StatusCode);
        }

        // A exclusão vale só para o documento e a UI: o mesmo cliente, com a mesma API, continua barrado em rota sem política.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(cliente, "/__teste/sem-familia")).StatusCode);
    }

    [Fact]
    public async Task Documento_openapi_e_UI_da_documentacao_somem_com_Documentacao_Habilitada_false()
    {
        var api = await ObterAsync(mysql);
        var cliente = api.WithWebHostBuilder(b => b.UseSetting("Documentacao:Habilitada", "false")).CreateClient();

        foreach (var rota in Rotas)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(cliente, rota)).StatusCode); // 404, não 401
        }
    }
}
