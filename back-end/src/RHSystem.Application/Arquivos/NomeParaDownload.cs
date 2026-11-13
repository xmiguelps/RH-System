using System.Globalization;
using System.Text;

namespace RHSystem.Application.Arquivos;

/// <summary>
/// O nome com que o arquivo é baixado: o que o usuário mandou, limpo para ir num cabeçalho <c>Content-Disposition</c>. Sem diretório (<c>/</c> e <c>\</c>: fica o último pedaço), sem aspas, sem caractere de controle,
/// quebra de linha, separador de linha ou de parágrafo, nem caractere de formatação invisível (os que invertem a direção do texto e disfarçam uma extensão), com no máximo 100 caracteres. Vazio (ou só pontos e espaços)
/// vira <c>"documento"</c> mais a extensão. Limpar um nome já limpo não o muda.
/// </summary>
public static class NomeParaDownload
{
    public const int TamanhoMaximo = 100;

    public static string Limpar(string? nomeOriginal, string extensao)
    {
        var nome = nomeOriginal ?? string.Empty;
        var barra = nome.LastIndexOfAny(['/', '\\']);
        if (barra >= 0)
        {
            nome = nome[(barra + 1)..];
        }

        var limpo = new StringBuilder(nome.Length);
        foreach (var letra in nome)
        {
            if (letra != '"' && !Proibido(letra))
            {
                limpo.Append(letra);
            }
        }

        nome = limpo.ToString().Trim();
        if (nome.Length > TamanhoMaximo)
        {
            nome = nome[..TamanhoMaximo];
            if (char.IsHighSurrogate(nome[^1]))
            {
                nome = nome[..^1]; // não deixa um par substituto cortado ao meio
            }

            nome = nome.TrimEnd();
        }

        return nome.All(letra => letra == '.' || char.IsWhiteSpace(letra)) ? "documento" + extensao : nome;
    }

    /// <summary>
    /// <c>&lt;prefixo&gt;&lt;texto&gt;&lt;extensão&gt;</c> para o arquivo gerado pela aplicação (o currículo, o certificado): barra e contrabarra do texto viram <c>-</c> (não viram diretório), o resto passa pela limpeza e o texto é cortado ANTES de juntar a extensão, então o total nunca passa de
    /// <see cref="TamanhoMaximo"/> caracteres e a extensão nunca se perde. Texto vazio vira <c>documento</c>.
    /// </summary>
    public static string Montar(string prefixo, string texto, string extensao)
    {
        var limpo = Limpar(texto.Replace('/', '-').Replace('\\', '-'), string.Empty);
        var cabe = Math.Max(1, TamanhoMaximo - prefixo.Length - extensao.Length);
        if (limpo.Length > cabe)
        {
            limpo = limpo[..cabe];
            if (char.IsHighSurrogate(limpo[^1]))
            {
                limpo = limpo[..^1]; // não deixa um par substituto cortado ao meio
            }

            limpo = limpo.TrimEnd();
        }

        return prefixo + limpo + extensao;
    }

    private static bool Proibido(char letra) =>
        char.GetUnicodeCategory(letra) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
