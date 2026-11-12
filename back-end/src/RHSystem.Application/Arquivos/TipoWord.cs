using System.IO.Compression;

namespace RHSystem.Application.Arquivos;

/// <summary>
/// O Word, pelo conteúdo (decisão 9 do plano da fatia 5): <c>.docx</c> é um ZIP (<c>PK 03 04</c>) que tem <c>word/document.xml</c>; <c>.doc</c> é um arquivo OLE2 (<c>D0 CF 11 E0 A1 B1 1A E1</c>). Um ZIP qualquer renomeado <c>.docx</c> não passa (falta o documento) e um OLE2 que não é Word
/// (<c>.xls</c>, <c>.ppt</c> renomeados) passa: o arquivo só sai como ANEXO, nunca é aberto pelo sistema. Fica FORA de <see cref="IdentificadorDeTipoDeArquivo"/> de propósito: documentos, holerites e atestados continuam sem aceitar Word.
/// </summary>
public static class TipoWord
{
    public static readonly TipoDeArquivo Doc = new("application/msword", ".doc");

    public static readonly TipoDeArquivo Docx = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx");

    private const string DocumentoDoDocx = "word/document.xml";

    private static ReadOnlySpan<byte> AssinaturaDoZip => [0x50, 0x4B, 0x03, 0x04];

    private static ReadOnlySpan<byte> AssinaturaDoOle2 => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>Os primeiros bytes são a assinatura de um arquivo OLE2 (o <c>.doc</c>).</summary>
    public static bool EhOle2(ReadOnlySpan<byte> inicio) => inicio.StartsWith(AssinaturaDoOle2);

    /// <summary>Os primeiros bytes são a assinatura de um ZIP (candidato a <c>.docx</c>).</summary>
    public static bool ComecaComoZip(ReadOnlySpan<byte> inicio) => inicio.StartsWith(AssinaturaDoZip);

    /// <summary>O ZIP tem o <c>word/document.xml</c>. Só lê a lista de entradas (nada é descompactado) e ZIP quebrado é "não".</summary>
    public static bool TemODocumentoDoWord(Stream conteudo)
    {
        try
        {
            using var zip = new ZipArchive(conteudo, ZipArchiveMode.Read, leaveOpen: true);
            return zip.GetEntry(DocumentoDoDocx) is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }
}
