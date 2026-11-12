using System.Reflection;
using PdfSharp.Fonts;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// A única fonte do PDF: Noto Sans (regular e negrito), embutida no assembly (<c>Fontes/NotoSans-*.ttf</c>, licença SIL OFL 1.1, o texto dela está em <c>Ponto/Fontes/LICENCA-NotoSans-OFL.txt</c>), com todos os acentos do português. <b>Nenhuma fonte do sistema é
/// usada</b>: num contêiner Linux sem fontes o PDF sai igual. Qualquer nome de família pedido resolve para ela (itálico não existe: o PDF não o pede).
/// </summary>
public sealed class FonteDoPdf : IFontResolver
{
    public const string Familia = "Noto Sans";

    private const string Regular = "NotoSans#Regular";
    private const string Negrito = "NotoSans#Bold";

    private static readonly Lazy<byte[]> BytesDaRegular = new(() => LerDoAssembly("Fontes/NotoSans-Regular.ttf"));
    private static readonly Lazy<byte[]> BytesDoNegrito = new(() => LerDoAssembly("Fontes/NotoSans-Bold.ttf"));
    private static readonly Lock Trava = new();

    /// <summary>Põe esta fonte como a do PDFsharp, uma vez só no processo (a configuração é global).</summary>
    public static void Registrar()
    {
        lock (Trava)
        {
            if (GlobalFontSettings.FontResolver is not FonteDoPdf)
            {
                GlobalFontSettings.FontResolver = new FonteDoPdf();
            }
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(isBold ? Negrito : Regular);

    public byte[]? GetFont(string faceName) => faceName switch
    {
        Regular => BytesDaRegular.Value,
        Negrito => BytesDoNegrito.Value,
        _ => null,
    };

    private static byte[] LerDoAssembly(string nome)
    {
        using var fluxo = typeof(FonteDoPdf).Assembly.GetManifestResourceStream(nome) ?? throw new InvalidOperationException($"A fonte embutida '{nome}' não está no assembly.");
        using var copia = new MemoryStream();
        fluxo.CopyTo(copia);
        return copia.ToArray();
    }
}
