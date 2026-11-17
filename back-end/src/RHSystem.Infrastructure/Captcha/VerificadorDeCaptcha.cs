using System.Text.Json;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Captcha;

namespace RHSystem.Infrastructure.Captcha;

/// <summary>A seção <c>Captcha</c>. Desligado (o padrão do <c>appsettings.json</c>) a candidatura pública não pede nada; ligado exige <see cref="ChaveDoSite"/> (pública) e <see cref="ChaveSecreta"/> (só por user-secrets, variável de ambiente ou configuração do App Service: nunca no repositório).</summary>
public sealed class OpcoesDoCaptcha
{
    public const string Secao = "Captcha";

    public const string EnderecoPadrao = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public bool Ativo { get; set; }

    public string ChaveDoSite { get; set; } = string.Empty;

    public string ChaveSecreta { get; set; } = string.Empty;

    public string EnderecoDeVerificacao { get; set; } = EnderecoPadrao;

    public int TimeoutSegundos { get; set; } = 5;

    /// <summary>Configuração errada falha na inicialização, com mensagem clara (que cita a chave de configuração e nunca o valor), e não no primeiro envio. Desligado, nada é conferido.</summary>
    public void Validar()
    {
        if (!Ativo)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ChaveDoSite))
        {
            throw new InvalidOperationException("Com Captcha:Ativo ligado falta Captcha:ChaveDoSite (a chave pública do widget) na configuração.");
        }

        if (string.IsNullOrWhiteSpace(ChaveSecreta))
        {
            throw new InvalidOperationException("Com Captcha:Ativo ligado falta Captcha:ChaveSecreta (só por user-secrets, variável de ambiente ou configuração do App Service) na configuração.");
        }

        if (!Uri.TryCreate(EnderecoDeVerificacao, UriKind.Absolute, out var endereco) || endereco.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Captcha:EnderecoDeVerificacao precisa ser um endereço https completo.");
        }

        if (TimeoutSegundos is < 1 or > 60)
        {
            throw new InvalidOperationException("Captcha:TimeoutSegundos precisa ficar entre 1 e 60.");
        }
    }
}

/// <summary>A conferência no provedor (Cloudflare Turnstile): <c>POST</c> do <c>secret</c> e do <c>response</c> (o token) em <c>application/x-www-form-urlencoded</c> ao endereço de verificação; vale <c>success: true</c> da resposta JSON.</summary>
public sealed class VerificadorDeCaptcha(OpcoesDoCaptcha opcoes, HttpClient http, ILogger<VerificadorDeCaptcha> log) : IVerificadorDeCaptcha
{
    public bool Ativo => opcoes.Ativo;

    public string? ChaveDoSite => opcoes.Ativo ? opcoes.ChaveDoSite : null;

    public async Task<bool> ConfirmarAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > ConferenciaDoCaptcha.TamanhoMaximoDoToken)
        {
            return false;
        }

        // O tempo esgotado vira "não confirmou"; o cancelamento de quem chamou (a requisição abortada) continua sendo cancelamento.
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(opcoes.TimeoutSegundos));
        try
        {
            using var corpo = new FormUrlEncodedContent([KeyValuePair.Create("secret", opcoes.ChaveSecreta), KeyValuePair.Create("response", token)]);
            using var resposta = await http.PostAsync(opcoes.EnderecoDeVerificacao, corpo, limite.Token);
            if (!resposta.IsSuccessStatusCode)
            {
                log.LogWarning("O provedor de captcha respondeu HTTP {Status}.", (int)resposta.StatusCode);
                return false;
            }

            using var json = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(limite.Token), cancellationToken: limite.Token);
            if (json.RootElement is { ValueKind: JsonValueKind.Object } raiz && raiz.TryGetProperty("success", out var sucesso) && sucesso.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            // Os códigos de erro são do provedor (invalid-input-secret avisa de chave secreta errada); nunca o token nem a chave.
            log.LogWarning("O provedor de captcha não confirmou o token (códigos: {Codigos}).", CodigosDeErro(json.RootElement));
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            log.LogWarning("O provedor de captcha não respondeu em {Segundos} s.", opcoes.TimeoutSegundos);
            return false;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or JsonException or InvalidOperationException)
        {
            // Só o tipo: a mensagem da exceção pode trazer o endereço, o token ou a chave.
            log.LogWarning("Não foi possível conferir o captcha com o provedor ({Tipo}).", excecao.GetType().Name);
            return false;
        }
    }

    private static string CodigosDeErro(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("error-codes", out var codigos) || codigos.ValueKind != JsonValueKind.Array)
        {
            return "nenhum";
        }

        var lidos = codigos.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String).Take(5).Select(c => (c.GetString() ?? string.Empty)[..Math.Min(40, c.GetString()?.Length ?? 0)]).ToList();
        return lidos.Count == 0 ? "nenhum" : string.Join(',', lidos);
    }
}
