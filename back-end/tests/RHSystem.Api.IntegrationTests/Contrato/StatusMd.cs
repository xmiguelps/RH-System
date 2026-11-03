namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Lê a seção "Rotas implementadas" do <c>contrato/STATUS.md</c>: uma linha por rota, e a coluna <c>operationId</c> é a chave. Só as linhas da
/// tabela entre este <c>##</c> e o próximo <c>##</c> contam; outras seções ao fim do arquivo (por exemplo "Pendências conhecidas") não atrapalham.
/// </summary>
internal static class StatusMd
{
    private const string Titulo = "## Rotas implementadas";

    public static string Caminho() => Path.Combine(Path.GetDirectoryName(LocalizadorDoContrato.CaminhoDoOpenApi())!, "STATUS.md");

    public static IReadOnlySet<string> OperationIdsDaSecaoRotasImplementadas()
    {
        var linhas = File.ReadAllLines(Caminho());
        var inicio = Array.FindIndex(linhas, l => l.Trim() == Titulo);
        Assert.True(inicio >= 0, $"O contrato/STATUS.md não tem a seção \"{Titulo}\".");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linha in linhas.Skip(inicio + 1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal)))
        {
            if (!linha.TrimStart().StartsWith('|'))
            {
                continue;
            }

            var colunas = linha.Split('|').Select(c => c.Trim()).ToArray(); // [vazio, Rota, operationId, Situação, Observação, vazio]
            var id = colunas.Length > 2 ? colunas[2].Trim('`') : string.Empty;
            if (id.Length == 0 || id == "operationId" || id.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            ids.Add(id);
        }

        return ids;
    }
}
