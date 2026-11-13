namespace RHSystem.Domain.Treinamentos;

/// <summary>
/// Um módulo do treinamento (<c>TREINAMENTOS_MODULOS</c>): a ordem é única por treinamento (<c>UQ (ID_TREINAMENTO, ORDEM)</c>, reordenar em duas fases) e cada tipo exige o seu campo (<c>CHK_TREINAMENTOS_MODULOS_CONTEUDO</c>). <c>CAMINHO_ARQUIVO</c> e <c>NOME_ARQUIVO_ORIGINAL</c> andam juntos e o caminho nunca sai da API.
/// </summary>
public class TreinamentoModulo
{
    private TreinamentoModulo()
    {
    }

    public int Id { get; private set; }

    public int TreinamentoId { get; private set; }

    public int Ordem { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public TipoDeModulo Tipo { get; private set; }

    public short? DuracaoMinutos { get; private set; }

    public string? UrlExterna { get; private set; }

    public string? CaminhoArquivo { get; private set; }

    public string? ConteudoTexto { get; private set; }

    public string? NomeArquivoOriginal { get; private set; }

    public static TreinamentoModulo Criar(int treinamentoId, int ordem, string titulo, TipoDeModulo tipo, short? duracaoMinutos, string? urlExterna, string? caminhoArquivo, string? conteudoTexto, string? nomeArquivoOriginal) =>
        new()
        {
            TreinamentoId = treinamentoId,
            Ordem = ordem,
            Titulo = titulo,
            Tipo = tipo,
            DuracaoMinutos = duracaoMinutos,
            UrlExterna = urlExterna,
            CaminhoArquivo = caminhoArquivo,
            ConteudoTexto = conteudoTexto,
            NomeArquivoOriginal = nomeArquivoOriginal,
        };
}
