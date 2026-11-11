namespace RHSystem.Application.Colaboradores;

/// <summary>
/// O telefone que o gestor pode ver: o RH recebe o número inteiro, o gestor só DDD, o primeiro dígito do número (celular) e os quatro últimos. O banco guarda texto
/// livre; aqui só contam os dígitos.
/// </summary>
public static class TelefoneMascarado
{
    /// <summary>
    /// <c>11987657783</c> vira <c>(11) 9****-7783</c> e <c>1133447783</c> vira <c>(11) ****-7783</c>. Nulo, vazio ou com outra quantidade de dígitos (não dá para saber
    /// onde fica o DDD) devolve <c>null</c>: o campo some em vez de mostrar errado.
    /// </summary>
    public static string? Mascarar(string? telefone)
    {
        var digitos = SoDigitos(telefone);
        return digitos?.Length switch
        {
            11 => $"({digitos[..2]}) {digitos[2]}****-{digitos[^4..]}",
            10 => $"({digitos[..2]}) ****-{digitos[^4..]}",
            _ => null,
        };
    }

    /// <summary>Só os dígitos ASCII; nulo continua nulo.</summary>
    public static string? SoDigitos(string? telefone) =>
        telefone is null ? null : new string(telefone.Where(c => c is >= '0' and <= '9').ToArray());
}
