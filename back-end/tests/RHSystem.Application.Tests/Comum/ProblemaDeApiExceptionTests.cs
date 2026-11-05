using RHSystem.Application.Comum;

namespace RHSystem.Application.Tests.Comum;

public class ProblemaDeApiExceptionTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(399)]
    [InlineData(600)]
    [InlineData(0)]
    public void ProblemaDeApi_recusa_status_que_nao_e_de_erro(int status) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProblemaDeApiException(status, "Título.", "Detalhe."));

    [Fact]
    public void ProblemaDeApi_guarda_o_que_foi_dado_e_a_mensagem_e_o_detalhe()
    {
        var erro = new ProblemaDeApiException(422, "Título.", "Detalhe.", "CODIGO", new Dictionary<string, object?> { ["x"] = 1 }, new Dictionary<string, string> { ["Retry-After"] = "5" });

        Assert.Equal((422, "Título.", "Detalhe.", "CODIGO"), (erro.Status, erro.Titulo, erro.Detalhe, erro.Codigo));
        Assert.Equal("Detalhe.", erro.Message);
        Assert.Equal(1, erro.Extensoes!["x"]);
        Assert.Equal("5", erro.Cabecalhos!["Retry-After"]);
    }

    [Fact]
    public void ProblemaDeApi_recusa_titulo_ou_detalhe_vazio()
    {
        Assert.Throws<ArgumentException>(() => new ProblemaDeApiException(422, " ", "Detalhe."));
        Assert.Throws<ArgumentException>(() => new ProblemaDeApiException(422, "Título.", ""));
    }
}
