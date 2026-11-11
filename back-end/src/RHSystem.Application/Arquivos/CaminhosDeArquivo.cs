using System.Text.RegularExpressions;

namespace RHSystem.Application.Arquivos;

/// <summary>
/// Os caminhos dos arquivos no armazenamento: <c>pasta/GUID.extensao</c>, tudo em minúsculas. O nome do arquivo é um GUID novo e <b>nunca</b> vem do usuário (o nome que ele mandou vai só para a coluna do banco e para
/// <see cref="NomeParaDownload"/>), então um caminho não tem como carregar <c>..</c>, barra inicial, barra invertida nem texto de ninguém.
/// </summary>
public static partial class CaminhosDeArquivo
{
    /// <summary>"documentos/9f1c…(32 hex).pdf": a pasta (um ou mais segmentos <c>[a-z0-9-]</c> separados por <c>/</c>), 32 hex e a extensão.</summary>
    [GeneratedRegex(@"^([a-z0-9-]+/)+[0-9a-f]{32}\.[a-z0-9]{1,10}\z")]
    private static partial Regex FormatoDoCaminho();

    [GeneratedRegex(@"^[a-z0-9-]+(/[a-z0-9-]+)*\z")]
    private static partial Regex FormatoDaPasta();

    [GeneratedRegex(@"^[a-z0-9]{1,10}\z")]
    private static partial Regex FormatoDaExtensao();

    /// <summary>Um caminho novo na pasta dada. <paramref name="extensao"/> vem com ou sem o ponto (<c>".pdf"</c>, o que o <see cref="IdentificadorDeTipoDeArquivo"/> devolve). Os dois saem em minúsculas.</summary>
    /// <exception cref="ArgumentException">A pasta ou a extensão tem algo fora de <c>[a-z0-9-]</c> (e <c>/</c> entre os segmentos da pasta).</exception>
    public static string Novo(string pasta, string extensao)
    {
        var pastaNormal = (pasta ?? string.Empty).ToLowerInvariant();
        if (!FormatoDaPasta().IsMatch(pastaNormal))
        {
            throw new ArgumentException("A pasta só pode ter letras minúsculas, números e hífen, em segmentos separados por barra.", nameof(pasta));
        }

        var extensaoNormal = (extensao ?? string.Empty).ToLowerInvariant();
        if (extensaoNormal.StartsWith('.'))
        {
            extensaoNormal = extensaoNormal[1..];
        }

        if (!FormatoDaExtensao().IsMatch(extensaoNormal))
        {
            throw new ArgumentException("A extensão só pode ter letras minúsculas e números (até 10).", nameof(extensao));
        }

        return $"{pastaNormal}/{Guid.NewGuid():N}.{extensaoNormal}";
    }

    /// <summary>O caminho tem a forma de <see cref="Novo"/>? Recusa <c>..</c>, barra inicial, maiúscula, barra invertida, segmento vazio, nome que não é um GUID, quebra de linha no fim e <c>null</c>.</summary>
    public static bool EhValido(string? caminho) => caminho is not null && FormatoDoCaminho().IsMatch(caminho);
}
