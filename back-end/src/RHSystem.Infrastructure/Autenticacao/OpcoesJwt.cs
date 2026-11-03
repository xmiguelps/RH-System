namespace RHSystem.Infrastructure.Autenticacao;

/// <summary>A seção <c>Jwt</c> da configuração. A chave nunca vai para o repositório: user-secrets, variável de ambiente ou App Service.</summary>
public sealed class OpcoesJwt
{
    /// <summary>Chave de assinatura (HMAC SHA-256), com pelo menos 32 bytes.</summary>
    public string Chave { get; set; } = string.Empty;

    public string Emissor { get; set; } = string.Empty;

    public string Audiencia { get; set; } = string.Empty;

    /// <summary>Vida do token de acesso, em minutos.</summary>
    public int AcessoMinutos { get; set; } = 15;

    /// <summary>Vida do refresh token, em dias.</summary>
    public int RefreshDias { get; set; } = 7;
}
