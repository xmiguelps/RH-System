using System.Text.Json;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Emails;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>O e-mail de cada decisão de férias (<c>FERIAS_DECIDIDAS</c>): o texto das cinco decisões, o motivo e as datas sugeridas quando há, e todo valor interpolado no Html com <c>HtmlEncode</c>; a auditoria do envio nunca leva o destinatário nem o motivo.</summary>
public class ResultadoFeriasRenderizadorTests
{
    private static readonly ResultadoFeriasRenderizador Renderizador = new();

    private static JsonElement Carga(string decisao, string? motivo = null, string nome = "Bruno Lima", string decididoPor = "Carlos Ribeiro", string? sugestaoInicio = null, string? sugestaoFim = null) =>
        JsonSerializer.SerializeToElement(new { nome, dataInicio = "2026-11-16", dataFim = "2026-11-30", decisao, decididoPor, motivo, sugestaoInicio, sugestaoFim });

    [Fact]
    public void O_tipo_e_o_da_lista_fechada_e_a_auditoria_fica_na_area_de_ferias_sem_destinatario()
    {
        var modelo = Renderizador.Renderizar(Carga("GESTOR_APROVOU"), "bruno@empresa.com.br");

        Assert.Equal("FERIAS_DECIDIDAS", Renderizador.Tipo);
        Assert.Equal((AuditoriaArea.Ferias, "E-mail do resultado do pedido de férias enviado", "Colaborador Bruno Lima"), (modelo.AreaDeAuditoria, modelo.DescricaoDeAuditoria, modelo.RegistroAfetado));
        Assert.DoesNotContain("bruno@empresa.com.br", modelo.Texto + modelo.Html + modelo.RegistroAfetado + modelo.DescricaoDeAuditoria, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GESTOR_APROVOU", "O seu pedido de férias de 16/11/2026 a 30/11/2026 foi aprovado pelo gestor (Carlos Ribeiro). Ele agora segue para a confirmação do RH.")]
    [InlineData("RH_CONFIRMOU", "O seu pedido de férias de 16/11/2026 a 30/11/2026 foi confirmado pelo RH (Carlos Ribeiro). Boas férias!")]
    public void As_decisoes_que_aprovam_dizem_quem_decidiu_e_o_proximo_passo(string decisao, string esperado)
    {
        var modelo = Renderizador.Renderizar(Carga(decisao), "x@empresa.com.br");

        Assert.Contains(esperado, modelo.Texto, StringComparison.Ordinal);
        Assert.Equal("Resposta do seu pedido de férias", modelo.Assunto);
        Assert.StartsWith("Olá, Bruno Lima!", modelo.Texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GESTOR_RECUSOU", "foi recusado pelo gestor (Carlos Ribeiro).")]
    [InlineData("RH_RECUSOU", "foi recusado pelo RH (Carlos Ribeiro).")]
    public void As_recusas_levam_o_motivo(string decisao, string trecho)
    {
        var modelo = Renderizador.Renderizar(Carga(decisao, "Equipe desfalcada no período."), "x@empresa.com.br");

        Assert.Contains(trecho, modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("Motivo: Equipe desfalcada no período.", modelo.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sugestao_traz_as_datas_sugeridas_e_diz_onde_responder()
    {
        var modelo = Renderizador.Renderizar(Carga("GESTOR_SUGERIU", sugestaoInicio: "2026-12-07", sugestaoFim: "2026-12-21"), "x@empresa.com.br");

        Assert.Equal("Sugestão de outra data para as suas férias", modelo.Assunto);
        Assert.Contains("sugeriu outras datas para o seu pedido de férias de 16/11/2026 a 30/11/2026: de 07/12/2026 a 21/12/2026.", modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("\"Minhas férias\"", modelo.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo_valor_do_html_passa_por_HtmlEncode()
    {
        var modelo = Renderizador.Renderizar(Carga("GESTOR_RECUSOU", "<script>alert('x')</script> & mais", nome: "Bruno <b>Lima</b>", decididoPor: "Carlos <i>Ribeiro</i>"), "x@empresa.com.br");

        Assert.DoesNotContain("<script>", modelo.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", modelo.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<i>", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("&amp; mais", modelo.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Carga_incompleta_ainda_gera_uma_mensagem_sem_lancar()
    {
        var modelo = Renderizador.Renderizar(JsonSerializer.SerializeToElement(new { decisao = "RH_CONFIRMOU" }), "x@empresa.com.br");

        Assert.Contains("Olá, colaborador!", modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("foi confirmado pelo RH", modelo.Texto, StringComparison.Ordinal);
    }
}
