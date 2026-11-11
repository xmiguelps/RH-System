namespace RHSystem.Application.Emails;

public sealed record MensagemDeEmail(string Para, string Assunto, string Texto, string Html);

/// <summary>
/// O envio falhou. <see cref="Permanente"/> (o SMTP respondeu 5xx) não adianta repetir; o resto (4xx, rede, tempo esgotado) é temporário. A mensagem da exceção
/// pode trazer o endereço, então quem registra a falha usa só o TIPO da exceção e o <see cref="StatusSmtp"/>.
/// </summary>
public sealed class FalhaDeEnvioException(string mensagem, bool permanente, Exception? interna = null, string? statusSmtp = null) : Exception(mensagem, interna)
{
    public bool Permanente { get; } = permanente;

    /// <summary>O código de resposta do servidor (por exemplo "550"), quando houve.</summary>
    public string? StatusSmtp { get; } = statusSmtp;
}

/// <summary>Trocável: SMTP (Mailpit local), em memória (testes), o provedor do Azure depois.</summary>
public interface IEnviadorDeEmail
{
    /// <exception cref="FalhaDeEnvioException">O envio falhou (temporária ou permanente).</exception>
    Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken ct);
}
