using System.Text.RegularExpressions;
using RHSystem.Infrastructure.Esquema;

namespace RHSystem.Api.IntegrationTests.Esquema;

/// <summary>
/// O DER/MER em texto (<c>Banco de Dados/Modelagem/DER_gerado.md</c>) tem de estar em dia com os scripts: mesmas tabelas e mesmas chaves estrangeiras. Mudou o esquema? Rode <c>python3 "Banco de Dados/Modelagem/gerar_der.py"</c> e versione o resultado.
/// (As colunas só se conferem rodando o gerador: <c>gerar_der.py --conferir</c> sai com código 1 se o arquivo estiver desatualizado.)
/// </summary>
public partial class DerGeradoTests
{
    [GeneratedRegex(@"^[ \t]*CREATE[ \t]+TABLE[ \t]+(?:IF[ \t]+NOT[ \t]+EXISTS[ \t]+)?`?(?<nome>\w+)`?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateTable();

    [GeneratedRegex(@"\bFOREIGN[ \t\r\n]+KEY[ \t\r\n]*\([^)]*\)[ \t\r\n]*REFERENCES[ \t\r\n]+`?\w+`?[ \t\r\n]*\(", RegexOptions.IgnoreCase)]
    private static partial Regex ChaveEstrangeira();

    [GeneratedRegex(@"^    (?<nome>[A-Z_0-9]+) \{$", RegexOptions.Multiline)]
    private static partial Regex EntidadeDoDer();

    [GeneratedRegex(@"^    \w+ \|\|--o\{ \w+ : ", RegexOptions.Multiline)]
    private static partial Regex RelacaoDoDer();

    private static string ArquivoDoDer()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var candidato = Path.Combine(pasta.FullName, "Banco de Dados", "Modelagem", "DER_gerado.md");
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new FileNotFoundException("Banco de Dados/Modelagem/DER_gerado.md não foi encontrado: rode gerar_der.py.");
    }

    private static string SemComentarios(string sql) => Regex.Replace(Regex.Replace(sql, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"--[^\r\n]*", string.Empty);

    [Fact]
    public void O_DER_gerado_tem_as_mesmas_tabelas_e_as_mesmas_chaves_estrangeiras_dos_scripts()
    {
        var scripts = new ScriptsEmbutidos().Carregar().Select(s => SemComentarios(s.Conteudo)).ToList();
        var tabelas = scripts.SelectMany(s => CreateTable().Matches(s).Select(m => m.Groups["nome"].Value.ToUpperInvariant())).ToHashSet();
        var chaves = scripts.Sum(s => ChaveEstrangeira().Matches(s).Count);
        var der = File.ReadAllText(ArquivoDoDer());

        var tabelasDoDer = EntidadeDoDer().Matches(der).Select(m => m.Groups["nome"].Value).ToHashSet();
        Assert.Equal(tabelas.Order().ToList(), tabelasDoDer.Order().ToList());
        Assert.Equal(chaves, RelacaoDoDer().Matches(der).Count);
        Assert.Contains($"São **{tabelas.Count} tabelas** e **{chaves} chaves estrangeiras**", der, StringComparison.Ordinal);
    }
}
