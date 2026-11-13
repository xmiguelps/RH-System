namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>
/// O código de validação do certificado: UUID versão 4 em minúsculas (36 caracteres), gerado pela aplicação. O trigger do banco cria a linha com um UUID versão 1 (<c>UUID()</c>, previsível) e a aplicação o troca na mesma transação; um código que não é versão 4 NUNCA sai em resposta nem em PDF e a conferência pública não o aceita.
/// </summary>
public static class CodigoDoCertificado
{
    public const int Tamanho = 36;

    public static string Novo() => Guid.NewGuid().ToString("D");

    public static bool EhVersao4(string? codigo)
    {
        if (codigo is null || codigo.Length != Tamanho || !Guid.TryParseExact(codigo, "D", out _))
        {
            return false;
        }

        var minusculo = codigo.All(c => c is '-' or (>= '0' and <= '9') or (>= 'a' and <= 'f'));
        return minusculo && codigo[14] == '4' && codigo[19] is '8' or '9' or 'a' or 'b';
    }
}
