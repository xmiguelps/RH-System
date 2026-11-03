using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Configuracao;

[Collection(MySqlCollection.Nome)]
public class LimiteDeTentativasTests(MySqlFixture mysql)
{
    private static async Task<HttpResponseMessage> TentarAsync(HttpClient cliente, string? ipDoProxy = null)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/auth/entrar")
        {
            Content = JsonContent.Create(new { email = ApiFactory.EmailRh, senha = "senha errada" }),
        };
        if (ipDoProxy is not null)
        {
            pedido.Headers.Add("X-Forwarded-For", ipDoProxy);
        }

        return await cliente.SendAsync(pedido);
    }

    private async Task<HttpClient> ClienteComLimiteDe5Async(bool comProxy = false)
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);
        return api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Autenticacao:LimiteLoginPorMinuto", "5");
            b.UseSetting("ProxyReverso:Habilitado", comProxy ? "true" : "false");
        }).CreateClient();
    }

    [Fact]
    public async Task Sexta_tentativa_de_login_no_mesmo_minuto_devolve_429_ProblemDetails_com_Retry_After()
    {
        var cliente = await ClienteComLimiteDe5Async();

        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await TentarAsync(cliente)).StatusCode); // dentro do limite: a resposta de sempre
        }

        var sexta = await TentarAsync(cliente);

        Assert.Equal(HttpStatusCode.TooManyRequests, sexta.StatusCode);
        Assert.Equal("application/problem+json", sexta.Content.Headers.ContentType?.MediaType);
        var corpo = JsonSerializer.Deserialize<JsonElement>(await sexta.Content.ReadAsStringAsync());
        Assert.Equal("Muitas tentativas.", corpo.GetProperty("title").GetString());
        Assert.Equal("Muitas tentativas. Aguarde um minuto e tente de novo.", corpo.GetProperty("detail").GetString());
        Assert.Equal(429, corpo.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("traceId").GetString()));
        var esperar = int.Parse(sexta.Headers.GetValues("Retry-After").Single());
        Assert.InRange(esperar, 1, 60);
    }

    [Fact]
    public async Task O_limite_vale_so_para_o_login_e_nao_para_as_outras_rotas()
    {
        var cliente = await ClienteComLimiteDe5Async();
        for (var i = 0; i < 6; i++)
        {
            await TentarAsync(cliente);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await TentarAsync(cliente)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/auth/eu")).StatusCode); // nada de 429 aqui
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Com_ProxyReverso_habilitado_o_limite_e_por_IP_do_cabecalho_X_Forwarded_For()
    {
        var cliente = await ClienteComLimiteDe5Async(comProxy: true);

        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await TentarAsync(cliente, "203.0.113.10")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await TentarAsync(cliente, "203.0.113.10")).StatusCode); // o primeiro IP esgotou o balde
        Assert.Equal(HttpStatusCode.Unauthorized, (await TentarAsync(cliente, "203.0.113.20")).StatusCode); // outro IP: balde próprio
    }

    [Fact]
    public async Task Sem_ProxyReverso_o_cabecalho_X_Forwarded_For_e_ignorado_e_todos_dividem_o_mesmo_balde()
    {
        // Controle do teste acima: sem o proxy habilitado, quem manda o cabeçalho não escolhe o próprio IP (senão fugiria do limite).
        var cliente = await ClienteComLimiteDe5Async(comProxy: false);

        for (var i = 1; i <= 5; i++)
        {
            await TentarAsync(cliente, $"198.51.100.{i}");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await TentarAsync(cliente, "198.51.100.99")).StatusCode);
    }

    [Fact]
    public async Task Proxy_habilitado_sem_proxies_confiaveis_avisa_no_log_e_com_a_lista_nao_avisa()
    {
        // Achado da revisão final: com o proxy ligado e a lista vazia só o 127.0.0.1 é confiável, o X-Forwarded-For de um proxy de verdade
        // é ignorado e todo mundo cai no mesmo balde do limite de login. Ninguém percebia: agora a subida avisa.
        var api = await ApiCompartilhada.ObterAsync(mysql);

        var semLista = new ColetorDeLogs();
        await api.WithWebHostBuilder(b =>
        {
            b.UseSetting("ProxyReverso:Habilitado", "true");
            b.ConfigureLogging(l => l.AddProvider(semLista));
        }).CreateClient().GetAsync("/health");

        var comLista = new ColetorDeLogs();
        await api.WithWebHostBuilder(b =>
        {
            b.UseSetting("ProxyReverso:Habilitado", "true");
            b.UseSetting("ProxyReverso:ProxiesConfiaveis:0", "10.0.0.0/8");
            b.ConfigureLogging(l => l.AddProvider(comLista));
        }).CreateClient().GetAsync("/health");

        Assert.Contains(semLista.Mensagens, m => m.StartsWith("[Warning]") && m.Contains("ProxiesConfiaveis") && m.Contains("127.0.0.1"));
        Assert.DoesNotContain(comLista.Mensagens, m => m.Contains("ProxiesConfiaveis"));
    }
}
