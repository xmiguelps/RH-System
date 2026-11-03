using Microsoft.AspNetCore.Identity;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Autenticacao;

/// <summary>
/// Hash de senha com o <see cref="PasswordHasher{TUser}"/> do ASP.NET Core Identity (PBKDF2 com sal aleatório). O hash tem bem mais de 40
/// caracteres, que é o mínimo que o trigger de USUARIOS aceita ("armazene a senha como hash").
/// </summary>
public sealed class SenhaHasher : ISenhaHasher
{
    public const int TamanhoMaximoDaSenha = 128;

    private readonly PasswordHasher<Usuario> _hasher = new();

    public string Gerar(string senha)
    {
        if (senha.Length > TamanhoMaximoDaSenha)
        {
            throw new ArgumentException($"A senha pode ter no máximo {TamanhoMaximoDaSenha} caracteres.", nameof(senha));
        }

        return _hasher.HashPassword(null!, senha); // o usuário não entra no cálculo
    }

    public bool Verificar(string hash, string senha)
    {
        if (string.IsNullOrEmpty(hash) || senha.Length > TamanhoMaximoDaSenha)
        {
            return false;
        }

        try
        {
            return _hasher.VerifyHashedPassword(null!, hash, senha) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false; // o valor guardado não é um hash em Base64: nunca autentica, e também nunca derruba o login com um erro 500
        }
    }
}
