using RHSystem.Infrastructure.Emails;

namespace RHSystem.Api.IntegrationTests.Configuracao;

/// <summary>
/// <c>App:UrlDoFront</c> é a base dos links de convite e de redefinição. Com o envio de e-mail ligado e sem ela, o renderizador lança, a fila marca cada convite como <c>FALHOU</c> e o RH vê "convite enviado": a API
/// não pode subir assim (configuração faltando falha na inicialização, não no primeiro e-mail).
/// </summary>
public class OpcoesDoAppTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost:5173")]
    [InlineData("/definir-senha")]
    [InlineData("ftp://front.exemplo.com.br")]
    public void Com_o_envio_ligado_a_URL_ausente_ou_que_nao_e_http_recusa_com_o_nome_da_chave(string url)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => new OpcoesDoApp { UrlDoFront = url }.Validar(envioAtivo: true));

        Assert.Contains("App:UrlDoFront", erro.Message);
    }

    [Theory]
    [InlineData("https://app.exemplo.com.br")]
    [InlineData("http://localhost:5173")]
    [InlineData("https://app.exemplo.com.br/rh")]
    public void Com_o_envio_ligado_a_URL_http_ou_https_absoluta_passa(string url) => new OpcoesDoApp { UrlDoFront = url }.Validar(envioAtivo: true);

    [Theory]
    [InlineData("")]
    [InlineData("qualquer coisa")]
    public void Com_o_envio_desligado_a_URL_pode_faltar_porque_nada_sai(string url) => new OpcoesDoApp { UrlDoFront = url }.Validar(envioAtivo: false);
}
