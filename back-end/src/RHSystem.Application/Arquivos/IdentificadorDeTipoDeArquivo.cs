namespace RHSystem.Application.Arquivos;

/// <summary>O tipo que o conteúdo revelou: o <c>Content-Type</c> e a extensão (com o ponto).</summary>
public sealed record TipoDeArquivo(string TipoDeConteudo, string Extensao);

/// <summary>
/// O tipo do arquivo vem do CONTEÚDO (a assinatura nos primeiros bytes), nunca do nome nem do <c>Content-Type</c> que o cliente mandou: um <c>.exe</c> chamado <c>.pdf</c> é recusado e um PDF chamado <c>.jpg</c> é aceito
/// como PDF. Só a assinatura inteira, no começo, vale. Quem precisar de outro tipo (xlsx na fatia 2) ACRESCENTA, não renomeia.
/// </summary>
public static class IdentificadorDeTipoDeArquivo
{
    private static ReadOnlySpan<byte> AssinaturaDoPdf => "%PDF-"u8;

    private static ReadOnlySpan<byte> AssinaturaDoJpeg => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> AssinaturaDoPng => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>PDF (<c>%PDF-</c>), JPEG (<c>FF D8 FF</c>) ou PNG (a assinatura de 8 bytes); o resto, <c>null</c>.</summary>
    public static TipoDeArquivo? Identificar(ReadOnlySpan<byte> inicio)
    {
        if (inicio.StartsWith(AssinaturaDoPdf))
        {
            return new TipoDeArquivo("application/pdf", ".pdf");
        }

        if (inicio.StartsWith(AssinaturaDoJpeg))
        {
            return new TipoDeArquivo("image/jpeg", ".jpg");
        }

        if (inicio.StartsWith(AssinaturaDoPng))
        {
            return new TipoDeArquivo("image/png", ".png");
        }

        return null;
    }
}
