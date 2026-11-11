using System.Security.Cryptography;

namespace RHSystem.Application.Acesso;

/// <summary>
/// O "hash" de quem recebeu o convite e ainda não escolheu a senha. A coluna <c>USUARIOS.SENHA</c> é obrigatória e o trigger do banco exige 40 caracteres ou mais; este valor
/// os tem e <b>nunca confere com senha alguma</b>: é Base64 de bytes aleatórios cujo primeiro byte é <c>0xFF</c>, que não é nenhum marcador de formato do
/// <c>PasswordHasher</c> do ASP.NET (<c>0x00</c> e <c>0x01</c>), então <c>Verificar</c> devolve falso sem lançar. Quem entra com a conta provisória é tratado como e-mail inexistente.
/// </summary>
public static class HashProvisorio
{
    public static string Gerar()
    {
        var bytes = new byte[48]; // 64 caracteres em Base64
        RandomNumberGenerator.Fill(bytes);
        bytes[0] = 0xFF;
        return Convert.ToBase64String(bytes);
    }
}
