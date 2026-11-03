using System.Net;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Configuracao;

[Collection(MySqlCollection.Nome)]
public class DocumentacaoTests(MySqlFixture mysql)
{
    private static readonly string[] Rotas = ["/openapi/v1.json", "/scalar/v1"];

    [Fact]
    public async Task Documentacao_so_responde_quando_Documentacao_Habilitada_e_true()
    {
        var api = await ApiCompartilhada.ObterAsync(mysql); // habilitada no ApiFactory
        var habilitada = api.CreateClient();
        var desabilitada = api.WithWebHostBuilder(b => b.UseSetting("Documentacao:Habilitada", "false")).CreateClient();

        foreach (var rota in Rotas)
        {
            Assert.Equal(HttpStatusCode.OK, (await habilitada.GetAsync(rota)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await desabilitada.GetAsync(rota)).StatusCode); // a fatia 1a mede de novo se o 404 continua
        }
    }

    [Fact]
    public async Task Documento_openapi_e_UI_respondem_sem_login_quando_habilitados()
    {
        // Decisão 9 da revisão: vale também depois que a fatia 1a fechar o resto por omissão (este teste roda de novo lá).
        var cliente = (await ApiCompartilhada.ObterAsync(mysql)).CreateClient(); // sem Authorization

        var documento = await cliente.GetAsync("/openapi/v1.json");
        var ui = await cliente.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, documento.StatusCode);
        Assert.Equal("application/json", documento.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Equal("text/html", ui.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_UI_le_o_documento_nativo_e_nao_gera_um_segundo_documento()
    {
        var cliente = (await ApiCompartilhada.ObterAsync(mysql)).CreateClient();

        var html = await cliente.GetStringAsync("/scalar/v1");

        Assert.Contains("openapi/v1.json", html); // aponta para o documento nativo (o Scalar escreve o caminho sem a barra inicial)
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/swagger/v1/swagger.json")).StatusCode); // nenhum outro gerador
    }
}
