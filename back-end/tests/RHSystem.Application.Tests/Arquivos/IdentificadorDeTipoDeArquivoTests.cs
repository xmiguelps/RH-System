using RHSystem.Application.Arquivos;

namespace RHSystem.Application.Tests.Arquivos;

public class IdentificadorDeTipoDeArquivoTests
{
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Identificar_reconhece_PDF_JPEG_e_PNG_pelo_conteudo_e_devolve_null_para_exe_texto_html_gif_e_vazio()
    {
        Assert.Equal(new TipoDeArquivo("application/pdf", ".pdf"), IdentificadorDeTipoDeArquivo.Identificar("%PDF-1.7\n%âãÏÓ"u8));
        Assert.Equal(new TipoDeArquivo("image/jpeg", ".jpg"), IdentificadorDeTipoDeArquivo.Identificar(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 }));
        Assert.Equal(new TipoDeArquivo("image/png", ".png"), IdentificadorDeTipoDeArquivo.Identificar([.. AssinaturaPng, 0x00, 0x00, 0x00, 0x0D]));

        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar("MZ\u0090\u0000\u0003\u0000"u8)); // executável do Windows, mesmo que o nome diga .pdf
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar("texto simples, sem assinatura"u8));
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar("<html><body>oi</body></html>"u8));
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar("GIF89a\u0001\u0000\u0001\u0000"u8));
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar([]));
    }

    [Fact]
    public void Identificar_so_aceita_a_assinatura_completa_no_inicio_do_conteudo()
    {
        // Os limites: a assinatura inteira e nada a menos.
        Assert.Equal("application/pdf", IdentificadorDeTipoDeArquivo.Identificar("%PDF-"u8)?.TipoDeConteudo);
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar("%PDF"u8)); // curto demais
        Assert.Equal("image/jpeg", IdentificadorDeTipoDeArquivo.Identificar(new byte[] { 0xFF, 0xD8, 0xFF })?.TipoDeConteudo);
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar(new byte[] { 0xFF, 0xD8 }));
        Assert.Equal("image/png", IdentificadorDeTipoDeArquivo.Identificar(AssinaturaPng)?.TipoDeConteudo);
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar(AssinaturaPng.AsSpan(0, 7)));

        // Cada byte da assinatura conta: um byte diferente em qualquer posição recusa.
        for (var i = 0; i < AssinaturaPng.Length; i++)
        {
            var torta = (byte[])AssinaturaPng.Clone();
            torta[i] ^= 0x01;
            Assert.Null(IdentificadorDeTipoDeArquivo.Identificar(torta));
        }

        // Só vale no começo: o mesmo texto depois de uma linha em branco (ou de qualquer byte) é outro arquivo.
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar("\n%PDF-1.7"u8));
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar(new byte[] { 0x00, 0xFF, 0xD8, 0xFF }));
    }
}
