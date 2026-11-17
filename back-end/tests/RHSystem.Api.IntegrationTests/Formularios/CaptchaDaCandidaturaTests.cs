using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Captcha;
using RHSystem.Application.Formularios.Regras;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.CandidaturasDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// O captcha da candidatura pública (decisão do dono, 2026-11-17), no banco REAL com o relógio em 2026-10-07T12:42Z. Ligado, a página entrega a chave pública (<c>captcha.chaveDoSite</c>) e o envio exige <c>captchaToken</c>: sem ele 400 em <c>errors.captchaToken</c>, com um que o provedor não confirma 422 <c>CAPTCHA_INVALIDO</c>.
/// A conferência roda ANTES de qualquer outra regra do envio (inclusive do 404 do código do link) e nada é gravado quando ela recusa. Desligado (o padrão), nada muda. O provedor é um verificador falso que só aceita <c>token-bom</c>.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CaptchaDaCandidaturaTests(MySqlFixture mysql)
{
    private const string ChaveDoSite = "chave-publica-de-teste";

    private sealed class VerificadorDeCaptchaDeTeste(bool ativo) : IVerificadorDeCaptcha
    {
        public List<string> Recebidos { get; } = [];

        public bool Ativo => ativo;

        public string? ChaveDoSite => ativo ? CaptchaDaCandidaturaTests.ChaveDoSite : null;

        public Task<bool> ConfirmarAsync(string token, CancellationToken ct)
        {
            lock (Recebidos)
            {
                Recebidos.Add(token);
            }

            return Task.FromResult(token == "token-bom");
        }
    }

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(CaptchaDaCandidaturaTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);
        return api;
    }

    private static HttpClient ClienteCom(ApiDeAcesso api, IVerificadorDeCaptcha verificador) =>
        api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IVerificadorDeCaptcha>();
            servicos.AddSingleton(verificador);
        })).CreateClient();

    private static MultipartFormDataContent ComCaptcha(MultipartFormDataContent corpo, string? token)
    {
        if (token is not null)
        {
            corpo.Add(new StringContent(token, Encoding.UTF8), "captchaToken");
        }

        return corpo;
    }

    private static Task<long> Candidatos(ApiDeAcesso api, int formulario) => ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {formulario}");

    // ---------------------------------------------------------------- a página

    [Fact]
    public async Task A_pagina_entrega_a_chave_publica_quando_ligado_e_captcha_nulo_quando_desligado()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap1");

        var ligado = await CorpoAsync(await ClienteCom(api, new VerificadorDeCaptchaDeTeste(true)).GetAsync($"/api/publico/formularios/{e.Token}"));
        var desligado = await CorpoAsync(await ClienteCom(api, new VerificadorDeCaptchaDeTeste(false)).GetAsync($"/api/publico/formularios/{e.Token}"));

        Assert.Equal(ChaveDoSite, ligado.GetProperty("captcha").GetProperty("chaveDoSite").GetString());
        Assert.Equal(JsonValueKind.Null, desligado.GetProperty("captcha").ValueKind);
    }

    [Fact]
    public async Task A_pagina_segue_o_contrato_ligado_e_desligado()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap2");

        await (await ClienteCom(api, new VerificadorDeCaptchaDeTeste(true)).GetAsync($"/api/publico/formularios/{e.Token}")).AfirmarConformeAoContratoAsync("obterFormularioPublico");
        await (await ClienteCom(api, new VerificadorDeCaptchaDeTeste(false)).GetAsync($"/api/publico/formularios/{e.Token}")).AfirmarConformeAoContratoAsync("obterFormularioPublico");
    }

    // ---------------------------------------------------------------- o envio

    [Fact]
    public async Task Ligado_com_o_token_bom_a_candidatura_grava_e_o_verificador_recebeu_o_token_do_campo()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap3");
        var verificador = new VerificadorDeCaptchaDeTeste(true);

        var resposta = await EnviarAsync(ClienteCom(api, verificador), e.Token, ComCaptcha(Candidatura(RespostasOk(e), email: "captcha.bom@email.com.br"), "token-bom"));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("responderFormularioPublico");
        Assert.Equal(1L, await Candidatos(api, e.Id));
        Assert.Equal(["token-bom"], verificador.Recebidos);
    }

    [Fact]
    public async Task Ligado_sem_o_campo_captchaToken_e_400_em_errors_captchaToken_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap4");
        var verificador = new VerificadorDeCaptchaDeTeste(true);
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;

        var resposta = await EnviarAsync(ClienteCom(api, verificador), e.Token, ComCaptcha(Candidatura(RespostasOk(e, comArquivo: true), curriculo: ("cv.pdf", ArquivosDeTeste.Pdf(1024)), arquivos: [("anexo.pdf", ArquivosDeTeste.Pdf(1024))]), token: null));

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("responderFormularioPublico");
        var erros = (await CorpoAsync(resposta)).GetProperty("errors");
        Assert.Equal("Confirme que você não é um robô.", erros.GetProperty("captchaToken")[0].GetString());
        Assert.Equal((0L, gravadosAntes, 0), (await Candidatos(api, e.Id), api.Api.Armazenamento.Gravados.Count, verificador.Recebidos.Count)); // nem candidato, nem arquivo, nem chamada ao provedor
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ligado_com_o_campo_em_branco_tambem_e_400_sem_chamar_o_provedor(string token)
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap5");
        var verificador = new VerificadorDeCaptchaDeTeste(true);

        var resposta = await EnviarAsync(ClienteCom(api, verificador), e.Token, ComCaptcha(Candidatura(RespostasOk(e)), token));

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Empty(verificador.Recebidos);
    }

    [Fact]
    public async Task Ligado_com_o_token_que_o_provedor_nao_confirma_e_422_CAPTCHA_INVALIDO_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap6");
        var verificador = new VerificadorDeCaptchaDeTeste(true);
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;

        var resposta = await EnviarAsync(ClienteCom(api, verificador), e.Token, ComCaptcha(Candidatura(RespostasOk(e, comArquivo: true), curriculo: ("cv.pdf", ArquivosDeTeste.Pdf(1024)), arquivos: [("anexo.pdf", ArquivosDeTeste.Pdf(1024))]), "token-falso"));

        var problema = await CorpoAsync(resposta);
        Assert.Equal((422, "CAPTCHA_INVALIDO", "Não foi possível confirmar que você não é um robô. Tente de novo."), ((int)resposta.StatusCode, problema.GetProperty("codigo").GetString(), problema.GetProperty("detail").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("responderFormularioPublico");
        Assert.Equal((0L, gravadosAntes), (await Candidatos(api, e.Id), api.Api.Armazenamento.Gravados.Count));
    }

    [Fact]
    public async Task O_captcha_vem_antes_de_tudo_um_codigo_que_nao_existe_sem_captcha_e_400_e_nao_404()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap7");
        var cliente = ClienteCom(api, new VerificadorDeCaptchaDeTeste(true));
        var inexistente = TokenDoLink.Novo();

        var semCaptcha = await EnviarAsync(cliente, inexistente, Candidatura(RespostasOk(e)));
        var semCaptchaNoCodigoReal = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e)));
        var comCaptcha = await EnviarAsync(cliente, inexistente, ComCaptcha(Candidatura(RespostasOk(e)), "token-bom"));

        Assert.Equal((400, 400, 404), ((int)semCaptcha.StatusCode, (int)semCaptchaNoCodigoReal.StatusCode, (int)comCaptcha.StatusCode)); // o captcha é a primeira conferência, até do 404 (a página pública é que mostra se o código existe)
    }

    [Fact]
    public async Task O_captcha_vem_antes_dos_erros_de_campo_e_com_o_captcha_bom_os_erros_de_campo_voltam_a_aparecer()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap8");
        var cliente = ClienteCom(api, new VerificadorDeCaptchaDeTeste(true));

        var campoInvalido = await EnviarAsync(cliente, e.Token, ComCaptcha(Candidatura(RespostasOk(e), nome: null, email: "nao-e-email"), "token-falso"));
        var captchaBomCampoInvalido = await EnviarAsync(cliente, e.Token, ComCaptcha(Candidatura(RespostasOk(e), nome: null, email: "nao-e-email"), "token-bom"));

        Assert.Equal(422, (int)campoInvalido.StatusCode);
        var erros = (await CorpoAsync(captchaBomCampoInvalido)).GetProperty("errors");
        Assert.Equal(400, (int)captchaBomCampoInvalido.StatusCode);
        Assert.True(erros.TryGetProperty("nome", out _) && erros.TryGetProperty("email", out _) && !erros.TryGetProperty("captchaToken", out _));
    }

    [Fact]
    public async Task Cada_tentativa_consulta_o_provedor_uma_vez_e_o_mesmo_e_mail_ainda_so_candidata_uma_vez()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap9");
        var verificador = new VerificadorDeCaptchaDeTeste(true);
        var cliente = ClienteCom(api, verificador);

        var primeira = await EnviarAsync(cliente, e.Token, ComCaptcha(Candidatura(RespostasOk(e), email: "mesma.pessoa@email.com.br"), "token-bom"));
        var segunda = await EnviarAsync(cliente, e.Token, ComCaptcha(Candidatura(RespostasOk(e), email: "mesma.pessoa@email.com.br"), "token-bom"));

        Assert.Equal((201, 409), ((int)primeira.StatusCode, (int)segunda.StatusCode));
        Assert.Equal(2, verificador.Recebidos.Count);
    }

    [Fact]
    public async Task Desligado_o_campo_captchaToken_e_ignorado_e_o_provedor_nunca_e_chamado()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap10");
        var verificador = new VerificadorDeCaptchaDeTeste(false);

        var semToken = await EnviarAsync(ClienteCom(api, verificador), e.Token, Candidatura(RespostasOk(e), email: "sem.captcha1@email.com.br"));
        var comToken = await EnviarAsync(ClienteCom(api, verificador), e.Token, ComCaptcha(Candidatura(RespostasOk(e), email: "sem.captcha2@email.com.br"), "qualquer-coisa"));

        Assert.Equal((201, 201), ((int)semToken.StatusCode, (int)comToken.StatusCode));
        Assert.Empty(verificador.Recebidos);
    }

    [Fact]
    public async Task O_token_do_captcha_nunca_vai_a_auditoria_nem_a_fila_de_e_mails()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap11");

        await EnviarAsync(ClienteCom(api, new VerificadorDeCaptchaDeTeste(true)), e.Token, ComCaptcha(Candidatura(RespostasOk(e), email: "privacidade@email.com.br"), "token-bom"));

        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO LIKE '%token-bom%'"));
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS WHERE CARGA LIKE '%token-bom%'"));
    }

    // ---------------------------------------------------------------- a configuração de verdade (achado da revisão independente)

    private static HttpClient ClienteComConfiguracao(ApiDeAcesso api, params (string Chave, string Valor)[] ajustes) =>
        api.Api.WithWebHostBuilder(construtor =>
        {
            foreach (var (chave, valor) in ajustes)
            {
                construtor.UseSetting(chave, valor);
            }
        }).CreateClient();

    [Fact]
    public async Task Com_Captcha_Ativo_e_as_chaves_na_configuracao_o_verificador_de_verdade_liga_o_captcha()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap12");
        var cliente = ClienteComConfiguracao(api, ("Captcha:Ativo", "true"), ("Captcha:ChaveDoSite", "1x00000000000000000000AA"), ("Captcha:ChaveSecreta", "1x0000000000000000000000000000000AA"));

        var pagina = await CorpoAsync(await cliente.GetAsync($"/api/publico/formularios/{e.Token}"));
        var semToken = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), email: "configuracao.real@email.com.br"));

        Assert.Equal("1x00000000000000000000AA", pagina.GetProperty("captcha").GetProperty("chaveDoSite").GetString());
        Assert.Equal(400, (int)semToken.StatusCode); // o verificador real exige o campo (sem ele nem chega ao provedor)
        Assert.Equal(0L, await Candidatos(api, e.Id));
        Assert.DoesNotContain("1x0000000000000000000000000000000AA", pagina.GetRawText(), StringComparison.Ordinal); // a chave secreta nunca sai
    }

    [Fact]
    public async Task Sem_a_configuracao_o_captcha_vem_desligado_pelo_padrao_do_appsettings()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCap13");

        var pagina = await CorpoAsync(await api.Api.ClienteAnonimo().GetAsync($"/api/publico/formularios/{e.Token}"));
        var envio = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(RespostasOk(e), email: "padrao.desligado@email.com.br"));

        Assert.Equal(JsonValueKind.Null, pagina.GetProperty("captcha").ValueKind);
        Assert.Equal(201, (int)envio.StatusCode);
    }

    [Theory]
    [InlineData("Captcha:ChaveSecreta")]
    [InlineData("Captcha:ChaveDoSite")]
    public async Task Com_Captcha_Ativo_a_API_nao_sobe_sem_uma_das_duas_chaves_e_a_mensagem_cita_a_chave_que_falta(string faltando)
    {
        var api = await ApiAsync();
        var ajustes = new List<(string, string)> { ("Captcha:Ativo", "true"), ("Captcha:ChaveDoSite", "chave-publica"), ("Captcha:ChaveSecreta", "segredo-que-nunca-pode-aparecer") };
        ajustes.RemoveAll(a => a.Item1 == faltando);

        var erro = Assert.ThrowsAny<Exception>(() => ClienteComConfiguracao(api, [.. ajustes]));

        Assert.Contains(faltando, erro.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("segredo-que-nunca-pode-aparecer", erro.ToString(), StringComparison.Ordinal);
    }
}
