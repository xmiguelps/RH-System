using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RHSystem.Infrastructure.Esquema;

/// <summary>
/// Lê os scripts embutidos no assembly (recursos <c>Scripts/V{nnn}__{nome}.sql</c>, vindos de <c>Banco de Dados/Scripts/</c>),
/// ordena por versão e recusa versão repetida ou nome fora do padrão.
/// </summary>
public sealed partial class ScriptsEmbutidos : IFonteDeScripts
{
    private const string Prefixo = "Scripts/";

    private readonly Assembly _assembly;

    public ScriptsEmbutidos()
        : this(typeof(ScriptsEmbutidos).Assembly)
    {
    }

    public ScriptsEmbutidos(Assembly assembly)
    {
        _assembly = assembly;
    }

    [GeneratedRegex(@"^Scripts/V(?<versao>\d{3,})__(?<nome>[A-Za-z0-9_]+)\.sql$")]
    private static partial Regex PadraoDoRecurso();

    public IReadOnlyList<ScriptSql> Carregar()
    {
        var scripts = new List<ScriptSql>();

        foreach (var recurso in _assembly.GetManifestResourceNames()
                     .Where(nome => nome.StartsWith(Prefixo, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var casamento = PadraoDoRecurso().Match(recurso);
            if (!casamento.Success)
            {
                throw new InvalidOperationException(
                    $"O script embutido '{recurso}' está fora do padrão Scripts/Vnnn__descricao.sql (versão com 3 ou mais dígitos, e descrição só com letras, números e sublinhado).");
            }

            var versao = int.Parse(casamento.Groups["versao"].Value, CultureInfo.InvariantCulture);
            var repetido = scripts.Find(s => s.Versao == versao);
            if (repetido is not null)
            {
                throw new InvalidOperationException(
                    $"A versão {versao} aparece em dois scripts: '{repetido.Nome}' e '{casamento.Groups["nome"].Value}'. Cada versão deve ter um único script.");
            }

            using var fluxo = _assembly.GetManifestResourceStream(recurso)
                ?? throw new InvalidOperationException($"Não foi possível abrir o script embutido '{recurso}'.");
            using var leitor = new StreamReader(fluxo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            scripts.Add(new ScriptSql(versao, casamento.Groups["nome"].Value, leitor.ReadToEnd()));
        }

        return scripts.OrderBy(s => s.Versao).ToList();
    }
}
