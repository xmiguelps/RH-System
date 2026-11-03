using System.Net;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Configuracao;

[Collection(MySqlCollection.Nome)]
public class SaudeTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Health_devolve_200_com_banco_e_503_com_connection_string_para_porta_fechada() // Review Focus 5
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);

        var saudavel = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, saudavel.StatusCode);
        Assert.Equal("""{"status":"ok"}""", await saudavel.Content.ReadAsStringAsync()); // exatamente o corpo do contrato

        // Não se para o contêiner compartilhado: a "queda do banco" é uma API apontando para uma porta fechada (a subida não toca no banco).
        var semBanco = api.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Padrao", "Server=127.0.0.1;Port=1;User ID=ninguem;Password=nada;Database=nenhum;Connection Timeout=3"))
            .CreateClient();

        var doente = await semBanco.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, doente.StatusCode);
        Assert.DoesNotContain("Exception", await doente.Content.ReadAsStringAsync()); // nada de erro interno vazando
    }

    [Fact]
    public async Task Health_nao_pede_login()
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);

        var resposta = await api.CreateClient().GetAsync("/health"); // sem Authorization

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
