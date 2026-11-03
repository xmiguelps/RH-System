using System.Text.RegularExpressions;

namespace RHSystem.Domain.Autenticacao;

/// <summary>Convite (primeiro acesso) ou redefinição de senha: o texto no banco é <c>CONVITE</c> ou <c>REDEFINICAO</c>.</summary>
public enum TipoDeTokenDeSenha
{
    Convite,
    Redefinicao,
}

/// <summary>
/// Uma linha de <c>USUARIOS_TOKENS_SENHA</c>: o link de convite ou de redefinição de senha. O token em si nunca é guardado, só o hash
/// (SHA-256 em hexadecimal). As datas vêm do relógio da aplicação (UTC): nenhuma entidade lê o relógio da máquina.
/// </summary>
public partial class UsuarioTokenSenha
{
    private UsuarioTokenSenha()
    {
    }

    public int Id { get; private set; }

    public int UsuarioId { get; private set; }

    public TipoDeTokenDeSenha Tipo { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CriadoEm { get; private set; }

    public DateTime ExpiraEm { get; private set; }

    public DateTime? UsadoEm { get; private set; }

    public DateTime? InvalidadoEm { get; private set; }

    /// <summary>Recusa o que os <c>CHECK</c> do banco recusariam: hash que não tem 64 hexadecimais e validade que não é positiva.</summary>
    public static UsuarioTokenSenha Criar(int usuarioId, TipoDeTokenDeSenha tipo, string tokenHash, DateTime agoraUtc, TimeSpan validade)
    {
        if (tokenHash is null || !Hexadecimal64().IsMatch(tokenHash))
        {
            throw new ArgumentException("O hash do token tem 64 caracteres hexadecimais (SHA-256).", nameof(tokenHash));
        }

        if (validade <= TimeSpan.Zero)
        {
            throw new ArgumentException("A validade do token tem de ser maior que zero.", nameof(validade));
        }

        return new UsuarioTokenSenha
        {
            UsuarioId = usuarioId,
            Tipo = tipo,
            TokenHash = tokenHash,
            CriadoEm = agoraUtc,
            ExpiraEm = agoraUtc + validade,
        };
    }

    [GeneratedRegex(@"^[0-9A-Fa-f]{64}\z")]
    private static partial Regex Hexadecimal64();
}
