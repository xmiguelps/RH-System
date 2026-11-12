using System.Text.RegularExpressions;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>
/// A prévia da lista (<c>previa</c>). <b>Simples</b>: a mensagem inteira. <b>Detalhado</b>: o início do conteúdo em texto puro (sem markdown) e sem reticências, até 200 caracteres: os títulos (<c>#</c>) saem, as linhas de um parágrafo se juntam com espaço
/// e entram parágrafos <b>inteiros</b> enquanto o total couber em 200; se o primeiro sozinho passar, corta em 200. (A regra é deduzida dos dois exemplos do contrato, o 398 e o 409, que são casos de teste.) Conteúdo só de títulos usa o texto deles: a
/// prévia nunca sai vazia.
/// </summary>
public static partial class PreviaDoAviso
{
    public const int Maximo = 200;

    public static string Montar(TipoDeAviso tipo, string conteudo)
    {
        var texto = conteudo.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (tipo == TipoDeAviso.Simples)
        {
            return texto;
        }

        var paragrafos = new List<string>();
        var titulos = new List<string>();
        foreach (var bloco in LinhaEmBranco().Split(texto))
        {
            var linhasDoParagrafo = new List<string>();
            foreach (var linha in bloco.Split('\n'))
            {
                var limpa = Limpar(linha, out var ehTitulo);
                if (limpa.Length == 0)
                {
                    continue;
                }

                (ehTitulo ? titulos : linhasDoParagrafo).Add(limpa);
            }

            if (linhasDoParagrafo.Count > 0)
            {
                paragrafos.Add(string.Join(' ', linhasDoParagrafo));
            }
        }

        if (paragrafos.Count == 0)
        {
            return ContagemDeCaracteres.Cortar(string.Join(' ', titulos), Maximo);
        }

        if (ContagemDeCaracteres.De(paragrafos[0]) > Maximo)
        {
            return ContagemDeCaracteres.Cortar(paragrafos[0], Maximo);
        }

        var previa = paragrafos[0];
        foreach (var paragrafo in paragrafos.Skip(1))
        {
            var candidata = $"{previa} {paragrafo}";
            if (ContagemDeCaracteres.De(candidata) > Maximo)
            {
                break;
            }

            previa = candidata;
        }

        return previa;
    }

    /// <summary>Uma linha sem as marcas de markdown (e se era título).</summary>
    private static string Limpar(string linha, out bool ehTitulo)
    {
        ehTitulo = false;
        if (RegraHorizontal().IsMatch(linha) || linha.TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var titulo = Titulo().Match(linha);
        if (titulo.Success)
        {
            ehTitulo = true;
            linha = titulo.Groups[1].Value;
        }
        else
        {
            linha = Citacao().Replace(linha, string.Empty);
            linha = Marcador().Replace(linha, string.Empty);
        }

        linha = Imagem().Replace(linha, "$1");
        linha = Link().Replace(linha, "$1");
        linha = CodigoEmLinha().Replace(linha, "$1");
        linha = Negrito().Replace(linha, "$2");
        linha = Italico().Replace(linha, "$1");
        linha = ItalicoComSublinhado().Replace(linha, "$1");
        linha = Riscado().Replace(linha, "$1");
        return Espacos().Replace(linha, " ").Trim();
    }

    [GeneratedRegex(@"\n[ \t]*\n")]
    private static partial Regex LinhaEmBranco();

    [GeneratedRegex(@"^\s{0,3}#{1,6}(?:\s+(.*?))?\s*#*\s*$")]
    private static partial Regex Titulo();

    [GeneratedRegex(@"^\s*([-*_]\s*){3,}$")]
    private static partial Regex RegraHorizontal();

    [GeneratedRegex(@"^\s*>+\s?")]
    private static partial Regex Citacao();

    [GeneratedRegex(@"^\s*(?:[-*+]|\d+[.)])\s+")]
    private static partial Regex Marcador();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Imagem();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"`([^`]*)`")]
    private static partial Regex CodigoEmLinha();

    [GeneratedRegex(@"(\*\*|__)(.+?)\1")]
    private static partial Regex Negrito();

    [GeneratedRegex(@"\*([^*\s][^*]*)\*")]
    private static partial Regex Italico();

    [GeneratedRegex(@"(?<!\w)_([^_\s][^_]*)_(?!\w)")]
    private static partial Regex ItalicoComSublinhado();

    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex Riscado();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();
}
