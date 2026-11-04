using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Emails;
using RHSystem.Infrastructure.Emails;

namespace RHSystem.Api.IntegrationTests.Emails;

/// <summary>
/// O envio por SMTP de verdade, contra o Mailpit em contêiner (imagem fixada, uma vez por classe). Fica na coleção do MySQL só para não rodar ao mesmo tempo que os
/// testes de banco (a memória da máquina é curta).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SmtpEnviadorDeEmailTests(MailpitFixture mailpit) : IClassFixture<MailpitFixture>
{
    private OpcoesDeEmail Opcoes(string? servidor = null, int? porta = null) => new()
    {
        Remetente = "nao-responda@rh-system.local",
        NomeDoRemetente = "RH System",
        Smtp = new OpcoesDeSmtp { Servidor = servidor ?? mailpit.Servidor, Porta = porta ?? mailpit.PortaSmtp, Seguranca = SegurancaDoSmtp.Nenhuma },
    };

    private static JsonElement Propriedade(JsonElement objeto, string nome) =>
        objeto.EnumerateObject().First(p => string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase)).Value;

    [Fact]
    public async Task Envia_para_o_Mailpit_e_a_mensagem_aparece_na_API_do_Mailpit_com_assunto_e_acentos()
    {
        var assunto = $"Confirmação de férias — João {Guid.NewGuid():N}";
        var enviador = new SmtpEnviadorDeEmail(Opcoes());

        await enviador.EnviarAsync(new MensagemDeEmail("ana.souza@empresa.com.br", assunto, "Olá, ação de férias aprovada.", "<p>Olá, <b>ação</b> de férias aprovada.</p>"), CancellationToken.None);

        using var http = new HttpClient { BaseAddress = new Uri(mailpit.UrlDaApi) };
        var lista = await http.GetFromJsonAsync<JsonElement>("/api/v1/messages");
        var achada = Propriedade(lista, "messages").EnumerateArray().Single(m => Propriedade(m, "Subject").GetString() == assunto); // o assunto com acentos e travessão chegou inteiro
        Assert.Equal("ana.souza@empresa.com.br", Propriedade(Propriedade(achada, "To")[0], "Address").GetString());
        Assert.Equal("nao-responda@rh-system.local", Propriedade(Propriedade(achada, "From"), "Address").GetString());
        var detalhe = await http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{Propriedade(achada, "ID").GetString()}");
        Assert.Contains("ação de férias aprovada", Propriedade(detalhe, "Text").GetString(), StringComparison.Ordinal);
        Assert.Contains("<b>ação</b>", Propriedade(detalhe, "HTML").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falha_de_conexao_vira_FalhaDeEnvioException_temporaria()
    {
        int portaFechada;
        using (var escuta = new TcpListener(IPAddress.Loopback, 0))
        {
            escuta.Start();
            portaFechada = ((IPEndPoint)escuta.LocalEndpoint).Port;
        } // fechada logo em seguida: ninguém atende

        var enviador = new SmtpEnviadorDeEmail(Opcoes("127.0.0.1", portaFechada));

        var falha = await Assert.ThrowsAsync<FalhaDeEnvioException>(() =>
            enviador.EnviarAsync(new MensagemDeEmail("ana@empresa.com.br", "Assunto", "Texto", "<p>Texto</p>"), CancellationToken.None));

        Assert.False(falha.Permanente);
    }

    [Fact]
    public async Task Endereco_invalido_e_falha_permanente_sem_nem_conectar()
    {
        var enviador = new SmtpEnviadorDeEmail(Opcoes("127.0.0.1", 1)); // se tentasse conectar, seria falha temporária de conexão

        var falha = await Assert.ThrowsAsync<FalhaDeEnvioException>(() =>
            enviador.EnviarAsync(new MensagemDeEmail("isto não é um e-mail", "Assunto", "Texto", "<p>Texto</p>"), CancellationToken.None));

        Assert.True(falha.Permanente);
    }

    [Fact]
    public void Classificacao_de_falhas_5xx_permanente_4xx_e_rede_temporaria()
    {
        // Exceções do MailKit fabricadas aqui: o que o servidor responde decide se repetir adianta.
        var permanente = SmtpEnviadorDeEmail.Classificar(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 caixa inexistente"));
        var temporaria4xx = SmtpEnviadorDeEmail.Classificar(new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.ServiceNotAvailable, "421 volte depois"));
        var ocupada = SmtpEnviadorDeEmail.Classificar(new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.MailboxBusy, "450 caixa ocupada"));

        Assert.True(permanente.Permanente);
        Assert.Equal("550", permanente.StatusSmtp);
        Assert.False(temporaria4xx.Permanente);
        Assert.Equal("421", temporaria4xx.StatusSmtp);
        Assert.False(ocupada.Permanente);
        Assert.Equal("450", ocupada.StatusSmtp);

        Exception[] deRede =
        [
            new IOException("conexão caiu"),
            new SocketException((int)SocketError.ConnectionRefused),
            new TimeoutException("tempo esgotado"),
            new SmtpProtocolException("resposta ilegível"),
            new AuthenticationException("senha recusada"),
            new OperationCanceledException("tempo limite do cliente"),
            new InvalidOperationException("algo que não sabemos classificar"),
        ];
        Assert.All(deRede, erro =>
        {
            var falha = SmtpEnviadorDeEmail.Classificar(erro);
            Assert.False(falha.Permanente, erro.GetType().Name); // rede, protocolo, autenticação e o desconhecido: tentar de novo
            Assert.Same(erro, falha.InnerException);
            Assert.Null(falha.StatusSmtp);
        });
    }
}
