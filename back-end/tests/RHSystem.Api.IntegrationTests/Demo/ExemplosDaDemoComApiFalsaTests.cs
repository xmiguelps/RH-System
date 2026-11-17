using System.Net;
using System.Text;
using RHSystem.Demo.Exemplos;

namespace RHSystem.Api.IntegrationTests.Demo;

/// <summary>O que a ferramenta da demo diz quando o endereço dado não é o da API ou a API barra o login: respostas fabricadas, sem banco e sem API de verdade.</summary>
public class ExemplosDaDemoComApiFalsaTests
{
    private sealed class RespostaFixa(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }

    private static EnviadorDeExemplos Enviador(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new RespostaFixa(responder)) { BaseAddress = new Uri("https://rh-demo.exemplo") }, new DateOnly(2026, 10, 7), "rhsystem-demo.example", "Demo#RH2026");

    [Fact]
    public async Task O_login_barrado_por_excesso_de_tentativas_manda_esperar_um_minuto_e_nao_mexer_na_senha()
    {
        var enviador = Enviador(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => enviador.EnviarAsync());

        Assert.Contains("429", erro.Message);
        Assert.Contains("minuto", erro.Message);
        Assert.DoesNotContain("senha", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Uma_pagina_de_site_no_lugar_da_API_manda_conferir_o_endereco_em_vez_de_quebrar_com_uma_excecao_de_JSON()
    {
        var enviador = Enviador(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<!doctype html><html><body>RH System</body></html>", Encoding.UTF8, "text/html") });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => enviador.EnviarAsync());

        Assert.Contains("--url", erro.Message);
    }

    [Fact]
    public async Task Um_JSON_de_outra_forma_tambem_manda_conferir_o_endereco()
    {
        var enviador = Enviador(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[1, 2, 3]", Encoding.UTF8, "application/json") });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => enviador.EnviarAsync());

        Assert.Contains("--url", erro.Message);
    }

    [Fact]
    public async Task Um_redirecionamento_para_outro_endereco_diz_qual_endereco_usar()
    {
        var enviador = Enviador(pedido => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://outro.exemplo/api/auth/entrar"), // o endereço final, depois de o HttpClient seguir o redirecionamento
        });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => enviador.EnviarAsync());

        Assert.Contains("https://outro.exemplo", erro.Message);
        Assert.Contains("--url", erro.Message);
    }
}
