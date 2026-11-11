using System.Globalization;
using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using RHSystem.Application.Emails;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O envio por SMTP (MailKit): serve ao Mailpit local e a qualquer provedor SMTP (o provedor em produção é decisão do dono). <c>5xx</c> é permanente;
/// <c>4xx</c>, falha de rede e tempo esgotado são temporários. Nada aqui registra o endereço.
/// </summary>
public sealed class SmtpEnviadorDeEmail(OpcoesDeEmail opcoes) : IEnviadorDeEmail
{
    private const int TempoLimiteEmMilissegundos = 30_000;

    public async Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken ct)
    {
        MimeMessage mime;
        try
        {
            mime = Montar(mensagem);
        }
        catch (Exception erro) when (erro is ParseException or ArgumentException or FormatException)
        {
            throw new FalhaDeEnvioException("A mensagem não pôde ser montada (endereço ou texto inválido).", permanente: true, erro);
        }

        using var cliente = new SmtpClient { Timeout = TempoLimiteEmMilissegundos };
        try
        {
            await cliente.ConnectAsync(opcoes.Smtp.Servidor, opcoes.Smtp.Porta, Seguranca(opcoes.Smtp.Seguranca), ct);
            if (!string.IsNullOrEmpty(opcoes.Smtp.Usuario))
            {
                await cliente.AuthenticateAsync(opcoes.Smtp.Usuario, opcoes.Smtp.Senha, ct);
            }

            await cliente.SendAsync(mime, ct);
            await cliente.DisconnectAsync(quit: true, CancellationToken.None);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // quem cancelou foi o chamador (o serviço está parando), não o tempo limite
        }
        catch (Exception erro)
        {
            throw Classificar(erro);
        }
    }

    /// <summary>
    /// <c>5xx</c> do servidor = permanente; <c>4xx</c>, rede, protocolo, TLS, autenticação e tempo esgotado = temporária (a falta de senha ou o provedor fora do ar passam
    /// e o e-mail sai quando voltar). <b>Exceção: remetente recusado (<c>MAIL FROM</c>, por exemplo 550 5.7.1 por domínio não verificado) também é temporária</b>: é problema de
    /// CONFIGURAÇÃO que vale para toda mensagem, e dá-la como permanente apagaria a carga (e o token do convite) de tudo o que o ciclo tentasse; corrigido o remetente, a fila
    /// manda. Já a caixa do destinatário e o conteúdo recusados valem só para aquela mensagem: permanentes. O status vai junto, porque a mensagem da exceção pode trazer o
    /// endereço e não é guardada.
    /// </summary>
    public static FalhaDeEnvioException Classificar(Exception erro) => erro switch
    {
        FalhaDeEnvioException ja => ja,
        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } remetente => new FalhaDeEnvioException(
            "O servidor SMTP recusou o remetente (configuração); vai tentar de novo.",
            permanente: false,
            remetente,
            ((int)remetente.StatusCode).ToString(CultureInfo.InvariantCulture)),
        SmtpCommandException comando => new FalhaDeEnvioException(
            "O servidor SMTP recusou o comando.",
            permanente: (int)comando.StatusCode >= 500,
            comando,
            ((int)comando.StatusCode).ToString(CultureInfo.InvariantCulture)),
        AuthenticationException or SmtpProtocolException or SslHandshakeException or ProtocolException or ServiceNotConnectedException or ServiceNotAuthenticatedException
            or IOException or SocketException or TimeoutException or OperationCanceledException =>
            new FalhaDeEnvioException("Falha temporária de conexão ou de protocolo com o servidor SMTP.", permanente: false, erro),
        _ => new FalhaDeEnvioException("Falha inesperada no envio; vai tentar de novo.", permanente: false, erro),
    };

    private MimeMessage Montar(MensagemDeEmail mensagem)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(opcoes.NomeDoRemetente, opcoes.Remetente));
        mime.To.Add(MailboxAddress.Parse(mensagem.Para));
        mime.Subject = mensagem.Assunto;
        mime.Body = new BodyBuilder { TextBody = mensagem.Texto, HtmlBody = mensagem.Html }.ToMessageBody();
        return mime;
    }

    private static SecureSocketOptions Seguranca(SegurancaDoSmtp seguranca) => seguranca switch
    {
        SegurancaDoSmtp.Nenhuma => SecureSocketOptions.None,
        SegurancaDoSmtp.StartTls => SecureSocketOptions.StartTls,
        SegurancaDoSmtp.Ssl => SecureSocketOptions.SslOnConnect,
        _ => throw new ArgumentOutOfRangeException(nameof(seguranca), seguranca, "Segurança do SMTP desconhecida."),
    };
}
