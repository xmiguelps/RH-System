namespace RHSystem.Application.Arquivos;

/// <summary>
/// O vídeo de um módulo de treinamento (decisão do dono, 2026-11-17): o tipo vem do CONTEÚDO, como em <see cref="IdentificadorDeTipoDeArquivo"/>, mas num identificador à parte, para documento, atestado e currículo nunca passarem a aceitar vídeo. MP4: <c>ftyp</c> nos bytes 4 a 7 (qualquer marca: o
/// navegador que não toca o formato mostra o erro dele, sem risco). WebM: a assinatura EBML (<c>1A 45 DF A3</c>) com o <c>DocType</c> <c>webm</c> nos primeiros 64 bytes (o Matroska, <c>matroska</c>, fica de fora).
/// </summary>
public static class IdentificadorDeVideo
{
    /// <summary>Quantos bytes do começo a identificação lê: o suficiente para o <c>DocType</c> do cabeçalho EBML.</summary>
    public const int BytesLidos = 64;

    private static ReadOnlySpan<byte> AssinaturaEbml => [0x1A, 0x45, 0xDF, 0xA3];

    public static TipoDeArquivo? Identificar(ReadOnlySpan<byte> inicio)
    {
        if (inicio.Length >= 8 && inicio.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return new TipoDeArquivo("video/mp4", ".mp4");
        }

        if (inicio.StartsWith(AssinaturaEbml) && inicio[..Math.Min(inicio.Length, BytesLidos)].IndexOf("webm"u8) >= 0)
        {
            return new TipoDeArquivo("video/webm", ".webm");
        }

        return null;
    }
}
