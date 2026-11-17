using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RHSystem.Infrastructure.Captcha;

namespace RHSystem.Api.IntegrationTests.Captcha;

/// <summary>
/// A conferência no provedor de captcha (Cloudflare Turnstile): um <c>POST</c> com <c>secret</c> e <c>response</c> em <c>application/x-www-form-urlencoded</c>; só <c>success: true</c> confirma. Qualquer falha (rede, tempo esgotado, HTTP de erro, resposta que não é o JSON esperado) é "não confirmou", nunca "passou", e nem o token nem a chave secreta vão a log.
/// Sem banco: o provedor é um manipulador HTTP falso.
/// </summary>
public class VerificadorDeCaptchaTests
{
    private const string Segredo = "segredo-de-teste-que-nunca-pode-vazar";
    private const string Token = "token-do-widget-0123456789";

    private sealed class ManipuladorFalso(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<(Uri? Url, HttpMethod Metodo, string? TipoDoCorpo, string Corpo)> Chamadas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken ct)
        {
            var corpo = requisicao.Content is null ? string.Empty : await requisicao.Content.ReadAsStringAsync(ct);
            Chamadas.Add((requisicao.RequestUri, requisicao.Method, requisicao.Content?.Headers.ContentType?.MediaType, corpo));
            return await responder(requisicao, ct);
        }
    }

    private sealed class LogDeTeste : ILogger<VerificadorDeCaptcha>
    {
        public List<(LogLevel Nivel, string Texto)> Linhas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Linhas.Add((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " | " + exception)));
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static OpcoesDoCaptcha Opcoes() => new() { Ativo = true, ChaveDoSite = "chave-publica", ChaveSecreta = Segredo, EnderecoDeVerificacao = "https://captcha.exemplo.test/turnstile/v0/siteverify", TimeoutSegundos = 5 };

    private static VerificadorDeCaptcha Criar(ManipuladorFalso manipulador, OpcoesDoCaptcha? opcoes = null, ILogger<VerificadorDeCaptcha>? log = null) =>
        new(opcoes ?? Opcoes(), new HttpClient(manipulador), log ?? NullLogger<VerificadorDeCaptcha>.Instance);

    [Fact]
    public async Task Token_confirmado_vale_e_a_chamada_e_um_POST_form_urlencoded_com_a_chave_secreta_e_o_token_ao_endereco_configurado()
    {
        var manipulador = new ManipuladorFalso((_, _) => Task.FromResult(Json("{\"success\":true,\"challenge_ts\":\"2026-10-07T12:42:00Z\",\"hostname\":\"rh.exemplo.test\",\"error-codes\":[]}")));

        var confirmou = await Criar(manipulador).ConfirmarAsync(Token, CancellationToken.None);

        Assert.True(confirmou);
        var chamada = Assert.Single(manipulador.Chamadas);
        Assert.Equal(("https://captcha.exemplo.test/turnstile/v0/siteverify", HttpMethod.Post, "application/x-www-form-urlencoded"), (chamada.Url!.ToString(), chamada.Metodo, chamada.TipoDoCorpo));
        Assert.Equal($"secret={Segredo}&response={Token}", chamada.Corpo);
    }

    [Fact]
    public async Task O_token_com_caracteres_especiais_vai_codificado_no_corpo()
    {
        var manipulador = new ManipuladorFalso((_, _) => Task.FromResult(Json("{\"success\":true}")));

        await Criar(manipulador).ConfirmarAsync("a&b=c d+e", CancellationToken.None);

        Assert.Equal($"secret={Segredo}&response=a%26b%3Dc+d%2Be", Assert.Single(manipulador.Chamadas).Corpo);
    }

    [Theory]
    [InlineData("{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}")]
    [InlineData("{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}")]
    [InlineData("{\"error-codes\":[]}")]
    [InlineData("{\"success\":\"true\"}")]
    [InlineData("{\"success\":null}")]
    [InlineData("não é json")]
    [InlineData("")]
    [InlineData("[true]")]
    public async Task O_provedor_que_recusa_ou_responde_algo_que_nao_e_success_true_nao_confirma(string resposta)
    {
        var manipulador = new ManipuladorFalso((_, _) => Task.FromResult(Json(resposta)));

        Assert.False(await Criar(manipulador).ConfirmarAsync(Token, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Erro_http_do_provedor_nao_confirma_mesmo_com_success_true_no_corpo(HttpStatusCode status)
    {
        var manipulador = new ManipuladorFalso((_, _) => Task.FromResult(Json("{\"success\":true}", status)));

        Assert.False(await Criar(manipulador).ConfirmarAsync(Token, CancellationToken.None));
    }

    [Fact]
    public async Task Falha_de_rede_nao_confirma_e_o_log_nao_leva_o_token_nem_a_chave_secreta()
    {
        var log = new LogDeTeste();
        var manipulador = new ManipuladorFalso((_, _) => throw new HttpRequestException($"sem rota para {Segredo} {Token}"));

        var confirmou = await Criar(manipulador, log: log).ConfirmarAsync(Token, CancellationToken.None);

        Assert.False(confirmou);
        var linha = Assert.Single(log.Linhas);
        Assert.Equal(LogLevel.Warning, linha.Nivel);
        Assert.DoesNotContain(Token, linha.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(Segredo, linha.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_log_de_recusa_do_provedor_nao_leva_o_token_nem_a_chave_secreta()
    {
        var log = new LogDeTeste();
        var manipulador = new ManipuladorFalso((_, _) => Task.FromResult(Json("{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}")));

        await Criar(manipulador, log: log).ConfirmarAsync(Token, CancellationToken.None);

        Assert.All(log.Linhas, l =>
        {
            Assert.DoesNotContain(Token, l.Texto, StringComparison.Ordinal);
            Assert.DoesNotContain(Segredo, l.Texto, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task O_tempo_esgotado_nao_confirma_e_volta_dentro_do_limite_configurado()
    {
        var opcoes = Opcoes();
        opcoes.TimeoutSegundos = 1;
        var manipulador = new ManipuladorFalso(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json("{\"success\":true}");
        });
        var relogio = System.Diagnostics.Stopwatch.StartNew();

        var confirmou = await Criar(manipulador, opcoes).ConfirmarAsync(Token, CancellationToken.None);

        Assert.False(confirmou);
        Assert.InRange(relogio.Elapsed, TimeSpan.FromMilliseconds(800), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task O_cancelamento_de_quem_chamou_cancela_a_espera_em_vez_de_virar_recusa()
    {
        using var cancelamento = new CancellationTokenSource();
        var manipulador = new ManipuladorFalso(async (_, ct) =>
        {
            await cancelamento.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return Json("{\"success\":true}");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Criar(manipulador).ConfirmarAsync(Token, cancelamento.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Token_vazio_nem_chama_o_provedor(string token)
    {
        var manipulador = new ManipuladorFalso((_, _) => Task.FromResult(Json("{\"success\":true}")));

        Assert.False(await Criar(manipulador).ConfirmarAsync(token, CancellationToken.None));
        Assert.Empty(manipulador.Chamadas);
    }

    [Fact]
    public async Task Token_com_mais_de_2048_caracteres_nem_chama_o_provedor_e_com_2048_chama()
    {
        var manipulador = new ManipuladorFalso((_, _) => Task.FromResult(Json("{\"success\":true}")));
        var verificador = Criar(manipulador);

        Assert.False(await verificador.ConfirmarAsync(new string('x', 2049), CancellationToken.None));
        Assert.Empty(manipulador.Chamadas);
        Assert.True(await verificador.ConfirmarAsync(new string('x', 2048), CancellationToken.None));
        Assert.Single(manipulador.Chamadas);
    }

    [Fact]
    public void Ativo_e_a_chave_do_site_vem_das_opcoes_e_desligado_nao_expoe_chave_nenhuma()
    {
        var ligado = Criar(new ManipuladorFalso((_, _) => Task.FromResult(Json("{}"))));
        var opcoesDesligadas = Opcoes();
        opcoesDesligadas.Ativo = false;
        var desligado = Criar(new ManipuladorFalso((_, _) => Task.FromResult(Json("{}"))), opcoesDesligadas);

        Assert.Equal((true, "chave-publica"), (ligado.Ativo, ligado.ChaveDoSite));
        Assert.Equal((false, null), (desligado.Ativo, desligado.ChaveDoSite));
    }

    // ---------------------------------------------------------------- a configuração

    [Fact]
    public void Desligado_a_configuracao_vazia_vale()
    {
        new OpcoesDoCaptcha().Validar();
    }

    [Theory]
    [InlineData("", "segredo", "Captcha:ChaveDoSite")]
    [InlineData("  ", "segredo", "Captcha:ChaveDoSite")]
    [InlineData("chave", "", "Captcha:ChaveSecreta")]
    [InlineData("chave", "  ", "Captcha:ChaveSecreta")]
    public void Ligado_exige_a_chave_do_site_e_a_chave_secreta_com_mensagem_clara(string chaveDoSite, string chaveSecreta, string citada)
    {
        var opcoes = new OpcoesDoCaptcha { Ativo = true, ChaveDoSite = chaveDoSite, ChaveSecreta = chaveSecreta };

        var erro = Assert.Throws<InvalidOperationException>(opcoes.Validar);

        Assert.Contains(citada, erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(chaveSecreta.Trim().Length > 0 ? chaveSecreta : "\0", erro.Message, StringComparison.Ordinal); // a mensagem nunca repete a chave
    }

    [Theory]
    [InlineData("http://captcha.exemplo.test/siteverify")]
    [InlineData("captcha/siteverify")]
    [InlineData("")]
    [InlineData("ftp://captcha.exemplo.test/siteverify")]
    public void Ligado_exige_um_endereco_de_verificacao_https(string endereco)
    {
        var opcoes = new OpcoesDoCaptcha { Ativo = true, ChaveDoSite = "chave", ChaveSecreta = "segredo", EnderecoDeVerificacao = endereco };

        var erro = Assert.Throws<InvalidOperationException>(opcoes.Validar);

        Assert.Contains("Captcha:EnderecoDeVerificacao", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public void O_tempo_limite_fica_entre_1_e_60_segundos(int segundos)
    {
        var opcoes = new OpcoesDoCaptcha { Ativo = true, ChaveDoSite = "chave", ChaveSecreta = "segredo", TimeoutSegundos = segundos };

        var erro = Assert.Throws<InvalidOperationException>(opcoes.Validar);

        Assert.Contains("Captcha:TimeoutSegundos", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ligado_com_tudo_certo_e_o_endereco_padrao_da_Cloudflare_vale()
    {
        new OpcoesDoCaptcha { Ativo = true, ChaveDoSite = "1x00000000000000000000AA", ChaveSecreta = "1x0000000000000000000000000000000AA" }.Validar();

        Assert.Equal("https://challenges.cloudflare.com/turnstile/v0/siteverify", OpcoesDoCaptcha.EnderecoPadrao);
    }
}
