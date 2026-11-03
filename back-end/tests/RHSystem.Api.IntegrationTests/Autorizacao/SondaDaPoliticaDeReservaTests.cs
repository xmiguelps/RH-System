using System.Net;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Autorizacao.ApiDeAutorizacao;

namespace RHSystem.Api.IntegrationTests.Autorizacao;

/// <summary>
/// A regra de reserva (rota sem política exige login) e o que NÃO é rota. Nasceu como sonda que só anotava o status; a medida (2026-11-03, com a
/// reserva escrita como <c>RequireAuthenticatedUser</c> puro) foi: GET anônimo em <c>/nao-existe</c>, <c>/api/nao-existe</c>, <c>/api/rh/nao-existe</c>,
/// <c>/openapi/v1.json</c> e <c>/scalar/v1</c> (com a documentação desligada) deram <b>401</b> <c>application/problem+json</c>, e o 405 e o 415 do roteamento
/// também viravam 401. A reserva passou a exigir login só de <c>RouteEndpoint</c> (ver <c>AutorizacaoExtensions</c>), e estes testes fixam o resultado.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SondaDaPoliticaDeReservaTests(MySqlFixture mysql)
{
    [Theory]
    [InlineData("/nao-existe")]
    [InlineData("/api/nao-existe")]
    [InlineData("/api/rh/nao-existe")]
    public async Task Anonimo_em_rota_inexistente_recebe_404_e_nao_401_com_a_reserva_ligada(string rota)
    {
        var cliente = (await ObterAsync(mysql)).CreateClient();

        await AssertProblemaAsync(await GetAsync(cliente, rota), 404, "Não encontrado.", "O registro não foi encontrado.");
    }

    [Fact]
    public async Task Anonimo_com_metodo_errado_ou_tipo_de_midia_errado_recebe_405_e_415_e_nao_401()
    {
        var cliente = (await ObterAsync(mysql)).CreateClient();

        var metodoErrado = await GetAsync(cliente, "/api/auth/entrar"); // a rota só aceita POST
        var tipoErrado = await cliente.PostAsync("/api/auth/entrar", new StringContent("email=a&senha=b", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, metodoErrado.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, tipoErrado.StatusCode);
        Assert.Equal("application/problem+json", metodoErrado.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/problem+json", tipoErrado.Content.Headers.ContentType?.MediaType);
    }
}
