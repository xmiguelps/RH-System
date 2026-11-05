using System.Security.Cryptography;
using System.Text;
using RHSystem.Application.Acesso;

namespace RHSystem.Infrastructure.Acesso;

public sealed class GeradorDeTokenDeSenha : IGeradorDeTokenDeSenha
{
    public TokenDeSenhaGerado Novo()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return new TokenDeSenhaGerado(token, Hash(token));
    }

    public string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
