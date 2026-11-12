using System.Text.Json;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Emails;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>Os textos dos e-mails de holerite e de advertência, sem banco: tudo que vem da carga passa por HtmlEncode, o que falta cai num texto padrão e a auditoria do envio diz a área e o colaborador (nunca o destinatário).</summary>
public class RenderizadoresDeComunicacaoTests
{
    private static JsonElement Carga(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    // ---------------------------------------------------------------- holerite publicado

    [Fact]
    public void O_holerite_publicado_codifica_o_nome_e_a_competencia_no_html_e_audita_o_colaborador()
    {
        var modelo = new HoleritePublicadoRenderizador().Renderizar(Carga("""{"nome":"<i>Ana</i>","competencia":"<b>de março</b>","versao":1}"""), "ana@empresa.com.br");

        Assert.DoesNotContain("<i>", modelo.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;i&gt;", modelo.Html, StringComparison.Ordinal);
        Assert.Equal((AuditoriaArea.Holerites, "Colaborador <i>Ana</i>"), (modelo.AreaDeAuditoria, modelo.RegistroAfetado));
        Assert.DoesNotContain("ana@empresa", modelo.RegistroAfetado, StringComparison.Ordinal);
    }

    [Fact]
    public void Campo_da_carga_que_nao_e_texto_cai_no_texto_padrao_sem_estourar()
    {
        var modelo = new HoleritePublicadoRenderizador().Renderizar(Carga("""{"nome":5,"competencia":true,"versao":"2"}"""), "ana@empresa.com.br");

        Assert.Contains("Olá, colaborador!", modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("do mês", modelo.Texto, StringComparison.Ordinal);
        Assert.Equal("Colaborador colaborador", modelo.RegistroAfetado);
    }

    // ---------------------------------------------------------------- advertência registrada

    [Fact]
    public void A_advertencia_registrada_diz_so_o_tipo_e_o_dia_e_a_copia_ao_RH_diz_quem_registrou()
    {
        var pessoa = new AdvertenciaRegistradaRenderizador().Renderizar(Carga("""{"nome":"Diego","tipo":"escrita","data":"2026-10-05","paraRh":false,"pessoa":"Diego","gestor":"Carlos"}"""), "diego@empresa.com.br");
        var copia = new AdvertenciaRegistradaRenderizador().Renderizar(Carga("""{"nome":"Ana","tipo":"verbal","data":"2026-10-05","paraRh":true,"pessoa":"<b>Diego</b>","gestor":"Carlos"}"""), "ana@empresa.com.br");

        Assert.Equal("Advertência registrada em seu nome", pessoa.Assunto);
        Assert.Contains("Foi registrada uma advertência escrita em seu nome, referente a 05/10/2026.", pessoa.Texto, StringComparison.Ordinal);
        Assert.Equal((AuditoriaArea.Advertencias, "Colaborador Diego"), (pessoa.AreaDeAuditoria, pessoa.RegistroAfetado));
        Assert.Equal("Advertência registrada para <b>Diego</b>", copia.Assunto);
        Assert.Contains("Carlos registrou uma advertência verbal de 05/10/2026 para <b>Diego</b>.", copia.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", copia.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;Diego&lt;/b&gt;", copia.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_advertencia_registrada_sem_dados_cai_nos_textos_padrao()
    {
        var modelo = new AdvertenciaRegistradaRenderizador().Renderizar(Carga("{}"), "x@empresa.com.br");

        Assert.Contains("Olá, colaborador!", modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("advertência verbal em seu nome, referente a data não informada.", modelo.Texto, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- resposta à contestação

    [Fact]
    public void A_resposta_do_RH_traz_a_decisao_e_o_texto_codificado()
    {
        var acatada = new AdvertenciaRespondidaRenderizador().Renderizar(Carga("""{"nome":"Diego","decisao":"ACATAR","resposta":"Conferimos <tudo>.","data":"2026-10-05"}"""), "diego@empresa.com.br");
        var mantida = new AdvertenciaRespondidaRenderizador().Renderizar(Carga("""{"nome":"Diego","decisao":"MANTER","resposta":"Mantemos.","data":"2026-10-05"}"""), "diego@empresa.com.br");

        Assert.Equal("Sua contestação foi acatada", acatada.Assunto);
        Assert.Contains("e acatou: a advertência foi cancelada.", acatada.Texto, StringComparison.Ordinal);
        Assert.Contains("Conferimos <tudo>.", acatada.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<tudo>", acatada.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;tudo&gt;", acatada.Html, StringComparison.Ordinal);
        Assert.Equal("Resposta à sua contestação de advertência", mantida.Assunto);
        Assert.Contains("e manteve a advertência.", mantida.Texto, StringComparison.Ordinal);
        Assert.Equal((AuditoriaArea.Advertencias, "Colaborador Diego"), (mantida.AreaDeAuditoria, mantida.RegistroAfetado));
    }
}
