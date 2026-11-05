using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Autenticacao;

/// <summary>
/// Emite o token de acesso (JWT assinado com HMAC SHA-256, vida curta) e os refresh tokens (32 bytes aleatórios em Base64Url; no banco
/// só o hash SHA-256 em hexadecimal). O JWT leva <c>sub</c> (id do usuário), <c>fid</c> (id do funcionário), <c>email</c>, <c>nome</c> e
/// <c>perfil</c> (texto).
/// </summary>
public sealed class EmissorDeTokensJwt : IEmissorDeTokens
{
    private const int TamanhoMinimoDaChave = 32;

    private readonly OpcoesJwt _opcoes;
    private readonly IRelogio _relogio;
    private readonly SigningCredentials _credenciais;
    private readonly JsonWebTokenHandler _handler = new();

    public EmissorDeTokensJwt(OpcoesJwt opcoes, IRelogio relogio)
    {
        if (Encoding.UTF8.GetByteCount(opcoes.Chave) < TamanhoMinimoDaChave)
        {
            throw new InvalidOperationException($"Jwt:Chave precisa ter pelo menos {TamanhoMinimoDaChave} bytes.");
        }

        _opcoes = opcoes;
        _relogio = relogio;
        _credenciais = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoes.Chave)), SecurityAlgorithms.HmacSha256);
    }

    public TimeSpan ValidadeDoRefresh => TimeSpan.FromDays(_opcoes.RefreshDias);

    public AcessoEmitido EmitirAcesso(Usuario usuario, string? hashDoRefreshDaSessao = null)
    {
        var perfil = usuario.Funcionario.Perfil
            ?? throw new InvalidOperationException("Não se emite token para um funcionário sem perfil de acesso configurado.");
        var agora = _relogio.AgoraUtc;
        var expiraEm = agora.AddMinutes(_opcoes.AcessoMinutos);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["fid"] = usuario.FuncionarioId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["email"] = usuario.Funcionario.EmailCorporativo ?? string.Empty,
            ["nome"] = usuario.Funcionario.Nome,
            ["perfil"] = perfil.ToString(),
        };
        if (hashDoRefreshDaSessao is not null)
        {
            claims["rth"] = hashDoRefreshDaSessao; // o hash do refresh da sessão: a troca de senha mantém só este
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Audiencia,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            SigningCredentials = _credenciais,
            Claims = claims,
        });

        return new AcessoEmitido(token, expiraEm);
    }

    public RefreshGerado NovoRefresh()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new RefreshGerado(token, HashDoRefresh(token));
    }

    public string HashDoRefresh(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
