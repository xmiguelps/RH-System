namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Conteúdos de arquivo para os testes: o INÍCIO de cada um é a assinatura real do tipo (o tipo vem do conteúdo, não do nome) e o resto é enchimento até o tamanho pedido. Os nomes de arquivo ficam por conta
/// de quem chama: o teste da fraude é justamente mandar um <c>.exe</c> com nome de <c>.pdf</c>.
/// </summary>
public static class ArquivosDeTeste
{
    public static byte[] Pdf(int tamanho = 1024) => Preenchido("%PDF-1.7\n"u8, tamanho);

    public static byte[] Jpeg(int tamanho = 1024) => Preenchido([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00], tamanho);

    public static byte[] Png(int tamanho = 1024) => Preenchido([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], tamanho);

    /// <summary>Um executável do Windows (começa por <c>MZ</c>): não é nenhum dos tipos aceitos.</summary>
    public static byte[] Exe(int tamanho = 1024) => Preenchido("MZ"u8, tamanho);

    public static byte[] Texto(int tamanho = 64) => Preenchido("texto simples sem assinatura"u8, tamanho);

    public static byte[] Gif(int tamanho = 64) => Preenchido("GIF89a"u8, tamanho);

    private static byte[] Preenchido(ReadOnlySpan<byte> inicio, int tamanho)
    {
        if (tamanho < inicio.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(tamanho), $"O tamanho mínimo deste tipo é {inicio.Length} bytes.");
        }

        var bytes = new byte[tamanho];
        inicio.CopyTo(bytes);
        for (var i = inicio.Length; i < tamanho; i++)
        {
            bytes[i] = (byte)(0x20 + (i % 90)); // texto ASCII visível: nada que parecesse outra assinatura
        }

        return bytes;
    }
}
