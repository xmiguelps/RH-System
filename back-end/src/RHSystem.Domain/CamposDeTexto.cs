namespace RHSystem.Domain;

/// <summary>
/// A normalização de texto que as entidades de cadastro repetem: <c>Trim</c> antes de contar, nome obrigatório e com tamanho máximo (o da coluna) e descrição opcional (vazia vira <c>null</c>). Recusa com
/// <see cref="ArgumentException"/> (o tipo exato), com o nome do campo na mensagem, antes de qualquer entidade mudar.
/// </summary>
internal static class CamposDeTexto
{
    public static string Obrigatorio(string? valor, string campo, int maximo)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            throw new ArgumentException($"O campo {campo} é obrigatório.", campo);
        }

        return AteOMaximo(texto, campo, maximo);
    }

    public static string? Opcional(string? valor, string campo, int maximo)
    {
        var texto = valor?.Trim();
        return string.IsNullOrEmpty(texto) ? null : AteOMaximo(texto, campo, maximo);
    }

    /// <summary>O instante cortado ao segundo (as colunas <c>DATETIME</c> do grupo não têm fração e o MySQL arredondaria em vez de cortar).</summary>
    public static DateTime AoSegundo(DateTime instante) => new(instante.Ticks - (instante.Ticks % TimeSpan.TicksPerSecond), instante.Kind);

    private static string AteOMaximo(string texto, string campo, int maximo) =>
        texto.Length <= maximo ? texto : throw new ArgumentException($"O campo {campo} pode ter até {maximo} caracteres.", campo);
}
