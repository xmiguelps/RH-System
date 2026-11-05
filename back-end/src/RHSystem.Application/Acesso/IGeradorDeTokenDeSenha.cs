namespace RHSystem.Application.Acesso;

/// <summary>Um token novo e o hash que vai para o banco. O token em claro só existe no e-mail (e na carga da fila até o envio); o banco guarda só o <paramref name="Hash"/>.</summary>
public sealed record TokenDeSenhaGerado(string Token, string Hash);

public interface IGeradorDeTokenDeSenha
{
    /// <summary><c>Token</c> = 32 bytes aleatórios em 64 hexadecimais minúsculos; <c>Hash</c> = SHA-256 do TEXTO do token em 64 hexadecimais (o que o <c>CHECK</c> do <c>V001</c> confere).</summary>
    TokenDeSenhaGerado Novo();

    /// <summary>O hash de um token que chegou de fora (o link): o mesmo cálculo de <see cref="Novo"/>.</summary>
    string Hash(string token);
}
