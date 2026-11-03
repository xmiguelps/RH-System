namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Lê <c>tabelas-sem-mapeamento.txt</c>: um nome de tabela por linha. Aceita comentário depois do nome (<c>SEQUENCIAS  # só por SQL cru</c>),
/// linha só de comentário e linha vazia.
/// </summary>
public static class TabelasSemMapeamento
{
    public const string NomeDoArquivo = "tabelas-sem-mapeamento.txt";

    public static IReadOnlyList<string> Interpretar(IEnumerable<string> linhas) =>
        linhas
            .Select(l => l.Split('#', 2)[0].Trim())
            .Where(l => l.Length > 0)
            .ToList();

    /// <summary>O arquivo é copiado para a saída do teste (ver o csproj), na pasta <c>Persistencia</c>.</summary>
    public static IReadOnlyList<string> Ler() =>
        Interpretar(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Persistencia", NomeDoArquivo)));
}
