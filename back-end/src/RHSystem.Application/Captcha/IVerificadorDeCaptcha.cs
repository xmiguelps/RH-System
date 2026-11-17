using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Mensagens;

namespace RHSystem.Application.Captcha;

/// <summary>
/// A conferência "não sou um robô" da candidatura pública (decisão do dono, 2026-11-17). <see cref="Ativo"/> vem de <c>Captcha:Ativo</c>: desligado, nada é exigido e a página não mostra a caixinha. <see cref="ChaveDoSite"/> é a chave PÚBLICA que a página entrega ao front para desenhar o widget (a secreta nunca sai da configuração).
/// </summary>
public interface IVerificadorDeCaptcha
{
    bool Ativo { get; }

    string? ChaveDoSite { get; }

    /// <summary><c>true</c> só se o provedor confirmou o token. Falha de rede, resposta estranha ou tempo esgotado é <c>false</c>: a conferência nunca deixa passar por defeito, e o token e a chave secreta nunca vão a log.</summary>
    Task<bool> ConfirmarAsync(string token, CancellationToken ct);
}

/// <summary>O que a página pública entrega ao front quando o captcha está ligado: a chave pública do widget.</summary>
public sealed record CaptchaDaPagina(string ChaveDoSite);

/// <summary>
/// A regra da candidatura: com o captcha ligado, sem token é 400 em <c>errors.captchaToken</c> e token que o provedor não confirma é 422 <c>CAPTCHA_INVALIDO</c>. Roda ANTES de qualquer outra regra do envio (depois do limite por IP), inclusive do 404 do código do link, para uma pessoa
/// sem captcha nunca descobrir se um código existe.
/// </summary>
public static class ConferenciaDoCaptcha
{
    public const int TamanhoMaximoDoToken = 2048;

    public static async Task ExigirAsync(IVerificadorDeCaptcha verificador, string? token, CancellationToken ct)
    {
        if (!verificador.Ativo)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ErroDeValidacaoException("captchaToken", "Confirme que você não é um robô.");
        }

        if (!await verificador.ConfirmarAsync(token, ct))
        {
            throw MensagensDosFormularios.CaptchaInvalido.ComoProblema();
        }
    }
}
