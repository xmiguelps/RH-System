using System.Net;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Configuracao;

[Collection(MySqlCollection.Nome)]
public class CorsTests(MySqlFixture mysql)
{
    private static async Task<HttpResponseMessage> ComOrigemAsync(HttpClient cliente, string origem, HttpMethod? metodo = null, string rota = "/health")
    {
        using var pedido = new HttpRequestMessage(metodo ?? HttpMethod.Get, rota);
        pedido.Headers.Add("Origin", origem);
        if (metodo == HttpMethod.Options)
        {
            pedido.Headers.Add("Access-Control-Request-Method", "POST");
            pedido.Headers.Add("Access-Control-Request-Headers", "content-type, authorization");
        }

        return await cliente.SendAsync(pedido);
    }

    [Fact]
    public async Task Cors_so_permite_as_origens_configuradas()
    {
        var cliente = (await ApiCompartilhada.ObterAsync(mysql)).CreateClient();

        var permitida = await ComOrigemAsync(cliente, ApiFactory.OrigemPermitida);
        var outra = await ComOrigemAsync(cliente, "https://site-malicioso.exemplo");
        var prevoo = await ComOrigemAsync(cliente, ApiFactory.OrigemPermitida, HttpMethod.Options, "/api/auth/entrar");
        var prevooDeOutra = await ComOrigemAsync(cliente, "https://site-malicioso.exemplo", HttpMethod.Options, "/api/auth/entrar");

        Assert.Equal(ApiFactory.OrigemPermitida, permitida.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(outra.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(HttpStatusCode.NoContent, prevoo.StatusCode);
        Assert.Equal(ApiFactory.OrigemPermitida, prevoo.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("authorization", string.Join(",", prevoo.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.False(prevooDeOutra.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_expoe_Content_Disposition_e_Retry_After()
    {
        // O navegador esconde do JavaScript os cabeçalhos que não são "seguros" para CORS: sem isso o nome do arquivo baixado e a espera do 429
        // não chegariam à tela do front.
        var cliente = (await ApiCompartilhada.ObterAsync(mysql)).CreateClient();

        var resposta = await ComOrigemAsync(cliente, ApiFactory.OrigemPermitida);

        var expostos = string.Join(",", resposta.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Contains("Content-Disposition", expostos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry-After", expostos, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sem_origens_configuradas_nenhuma_origem_e_permitida()
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);
        var cliente = api.WithWebHostBuilder(b => b.UseSetting("Cors:OrigensPermitidas:0", string.Empty)).CreateClient();

        var resposta = await ComOrigemAsync(cliente, ApiFactory.OrigemPermitida);

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
