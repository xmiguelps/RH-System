namespace RHSystem.Domain.Colaboradores;

/// <summary>
/// O CPF como o banco o guarda: 11 dígitos, sem pontuação. <see cref="EhValido"/> faz a mesma conta do trigger
/// <c>TRG_FUNCIONARIOS_DETALHES_BEFORE_INSERT</c> (<c>V002</c>), para recusar antes de chegar ao banco com uma mensagem do contrato.
/// </summary>
public static class Cpf
{
    /// <summary>Só os dígitos ASCII (<c>0-9</c>, o que o <c>CHECK</c> do banco aceita); nulo vira texto vazio.</summary>
    public static string Normalizar(string? texto) =>
        texto is null ? string.Empty : new string(texto.Where(c => c is >= '0' and <= '9').ToArray());

    /// <summary>
    /// Recebe só os dígitos (quem tem texto com pontuação chama <see cref="Normalizar"/> antes): 11 dígitos, não todos iguais, com os dois dígitos
    /// verificadores certos.
    /// </summary>
    public static bool EhValido(string cpf)
    {
        if (cpf is null || cpf.Length != 11 || !cpf.All(c => c is >= '0' and <= '9') || cpf.All(c => c == cpf[0]))
        {
            return false;
        }

        return DigitoVerificador(cpf, 9) == cpf[9] - '0' && DigitoVerificador(cpf, 10) == cpf[10] - '0';
    }

    private static int DigitoVerificador(string cpf, int quantidade)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (cpf[i] - '0') * (quantidade + 1 - i);
        }

        var digito = soma * 10 % 11;
        return digito == 10 ? 0 : digito;
    }
}
