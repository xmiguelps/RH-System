using System.Text.Json;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Acesso;
using RHSystem.Infrastructure.Emails;

namespace RHSystem.Api.IntegrationTests.Acesso;

public class RenderizadoresDeSenhaTests
{
    private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly OpcoesDoApp App = new() { UrlDoFront = "https://app.exemplo.com.br" };

    private static JsonElement Carga(string nome) =>
        JsonSerializer.SerializeToElement(new { nome, token = Token, expiraEmUtc = new DateTime(2026, 10, 14, 12, 42, 0, DateTimeKind.Utc) }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void Convite_renderiza_assunto_link_com_o_token_validade_de_7_dias_e_auditoria_sem_token()
    {
        var renderizador = new ConviteRenderizador(App, new OpcoesDeSenha());

        var modelo = renderizador.Renderizar(Carga("Bruno Lima"), "bruno.lima@empresa.com.br");

        Assert.Equal("CONVITE", renderizador.Tipo);
        Assert.Equal("Seu acesso ao RH-System", modelo.Assunto);
        var link = $"https://app.exemplo.com.br/definir-senha?token={Token}";
        Assert.Contains(link, modelo.Texto, StringComparison.Ordinal);
        Assert.Contains(link, modelo.Html, StringComparison.Ordinal);
        Assert.Contains("7 dias", modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("uma vez", modelo.Texto, StringComparison.Ordinal); // uso único
        Assert.Contains("Bruno Lima", modelo.Texto, StringComparison.Ordinal);
        Assert.Equal((AuditoriaArea.Acesso, "E-mail de convite enviado", "Colaborador Bruno Lima"), (modelo.AreaDeAuditoria, modelo.DescricaoDeAuditoria, modelo.RegistroAfetado));
        Assert.DoesNotContain(Token, modelo.DescricaoDeAuditoria, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, modelo.RegistroAfetado, StringComparison.Ordinal);
        Assert.DoesNotContain("bruno.lima@", modelo.DescricaoDeAuditoria + modelo.RegistroAfetado, StringComparison.Ordinal); // o destinatário não vai para a auditoria
    }

    [Fact]
    public void Redefinicao_renderiza_assunto_link_validade_de_1_hora_e_codifica_HTML_do_nome()
    {
        var renderizador = new RedefinicaoSenhaRenderizador(App, new OpcoesDeSenha());

        var modelo = renderizador.Renderizar(Carga("<b>Ana</b> & \"Cia\""), "ana@empresa.com.br");

        Assert.Equal("REDEFINICAO_SENHA", renderizador.Tipo);
        Assert.Equal("Redefinição de senha do RH-System", modelo.Assunto);
        Assert.Contains($"https://app.exemplo.com.br/definir-senha?token={Token}", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("1 hora", modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("uma vez", modelo.Texto, StringComparison.Ordinal);
        // O nome vem codificado no Html (nada de marcação injetada) e cru no texto simples.
        Assert.DoesNotContain("<b>Ana</b>", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;Ana&lt;/b&gt;", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("<b>Ana</b>", modelo.Texto, StringComparison.Ordinal);
        Assert.Equal((AuditoriaArea.Acesso, "E-mail de redefinição de senha enviado"), (modelo.AreaDeAuditoria, modelo.DescricaoDeAuditoria));
        Assert.DoesNotContain(Token, modelo.DescricaoDeAuditoria + modelo.RegistroAfetado, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(60, "1 hora")]
    [InlineData(120, "2 horas")]
    [InlineData(90, "90 minutos")]
    [InlineData(30, "30 minutos")]
    public void A_validade_da_redefinicao_sai_em_horas_ou_minutos_como_configurada(int minutos, string esperado)
    {
        var renderizador = new RedefinicaoSenhaRenderizador(App, new OpcoesDeSenha { RedefinicaoMinutos = minutos });

        Assert.Contains(esperado, renderizador.Renderizar(Carga("Ana"), "ana@empresa.com.br").Texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "1 dia")]
    [InlineData(7, "7 dias")]
    [InlineData(14, "14 dias")]
    public void A_validade_do_convite_sai_em_dias_como_configurada(int dias, string esperado)
    {
        var renderizador = new ConviteRenderizador(App, new OpcoesDeSenha { ConviteDias = dias });

        Assert.Contains(esperado, renderizador.Renderizar(Carga("Ana"), "ana@empresa.com.br").Texto, StringComparison.Ordinal);
    }

    [Fact]
    public void A_barra_final_da_URL_do_front_nao_duplica_e_o_token_e_escapado_na_URL()
    {
        var renderizador = new ConviteRenderizador(new OpcoesDoApp { UrlDoFront = "https://app.exemplo.com.br/" }, new OpcoesDeSenha());

        var texto = renderizador.Renderizar(Carga("Ana"), "ana@empresa.com.br").Texto;

        Assert.Contains($"https://app.exemplo.com.br/definir-senha?token={Token}", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("br//definir-senha", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Sem_a_URL_do_front_configurada_o_renderizador_lanca_em_vez_de_mandar_link_quebrado()
    {
        var renderizador = new ConviteRenderizador(new OpcoesDoApp(), new OpcoesDeSenha());

        Assert.Throws<InvalidOperationException>(() => renderizador.Renderizar(Carga("Ana"), "ana@empresa.com.br"));
    }

    [Fact]
    public void Carga_sem_token_ou_sem_nome_lanca_e_nao_manda_e_mail_sem_link()
    {
        var renderizador = new ConviteRenderizador(App, new OpcoesDeSenha());
        var semToken = JsonSerializer.SerializeToElement(new { nome = "Ana" });

        Assert.ThrowsAny<Exception>(() => renderizador.Renderizar(semToken, "ana@empresa.com.br"));
    }

    [Fact]
    public void OpcoesDeSenha_validam_os_tres_valores()
    {
        new OpcoesDeSenha().Validar();
        Assert.Throws<InvalidOperationException>(() => new OpcoesDeSenha { ConviteDias = 0 }.Validar());
        Assert.Throws<InvalidOperationException>(() => new OpcoesDeSenha { RedefinicaoMinutos = 0 }.Validar());
        Assert.Throws<InvalidOperationException>(() => new OpcoesDeSenha { IntervaloMinimoEntreRedefinicoesSegundos = -1 }.Validar());
    }
}
