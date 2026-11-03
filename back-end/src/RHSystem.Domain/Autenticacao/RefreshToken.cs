namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>REFRESH_TOKENS</c>. O token em si nunca é guardado: só o hash.</summary>
public class RefreshToken
{
    private RefreshToken()
    {
    }

    public int Id { get; private set; }

    public int UsuarioId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CriadoEm { get; private set; }

    public DateTime ExpiraEm { get; private set; }

    public DateTime? RevogadoEm { get; private set; }

    public bool EstaAtivo(DateTime agoraUtc) => RevogadoEm is null && agoraUtc < ExpiraEm;

    public static RefreshToken Criar(int usuarioId, string tokenHash, DateTime agoraUtc, TimeSpan validade) =>
        new()
        {
            UsuarioId = usuarioId,
            TokenHash = tokenHash,
            CriadoEm = agoraUtc,
            ExpiraEm = agoraUtc + validade,
        };

    public void Revogar(DateTime agoraUtc) => RevogadoEm ??= agoraUtc;
}
