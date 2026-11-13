using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Infrastructure.Ponto;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// O certificado de conclusão em PDF (PDFsharp e MigraDoc): UMA página A4 em paisagem com a empresa (se configurada), "Certificado de conclusão", o nome da pessoa, o título do treinamento (o do dia da emissão), a carga horária (se algum módulo tem duração), a data e o número, o código de validação e, só quando o endereço do front está configurado, onde conferir. A fonte é a
/// mesma embutida do espelho de ponto (<see cref="FonteDoPdf"/>), então os acentos saem certos em qualquer máquina. O código que chega aqui é SEMPRE versão 4 (o garantidor recusa o resto antes).
/// </summary>
public sealed class GeradorDePdfDoCertificado : IGeradorDePdfDoCertificado
{
    public byte[] Gerar(CertificadoParaPdf entrada)
    {
        FonteDoPdf.Registrar();
        var renderizador = new PdfDocumentRenderer { Document = Montar(entrada) };
        renderizador.RenderDocument();
        using var saida = new MemoryStream();
        renderizador.PdfDocument.Save(saida, false);
        return saida.ToArray();
    }

    /// <summary>"1h40", "2h" e "45 min": a carga horária como se lê num certificado.</summary>
    public static string CargaHoraria(int minutos) => minutos < 60
        ? string.Create(CultureInfo.InvariantCulture, $"{minutos} min")
        : minutos % 60 == 0 ? string.Create(CultureInfo.InvariantCulture, $"{minutos / 60}h") : string.Create(CultureInfo.InvariantCulture, $"{minutos / 60}h{minutos % 60:00}");

    private static Document Montar(CertificadoParaPdf entrada)
    {
        var documento = new Document();
        documento.Info.Title = $"Certificado de conclusão - {entrada.TituloDoTreinamento}";
        documento.Info.Author = entrada.NomeDaEmpresa ?? "RH System";

        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = FonteDoPdf.Familia;
        normal.Font.Size = 12;

        var secao = documento.AddSection();
        secao.PageSetup.PageFormat = PageFormat.A4;
        secao.PageSetup.Orientation = Orientation.Landscape;
        secao.PageSetup.LeftMargin = Unit.FromCentimeter(2.5);
        secao.PageSetup.RightMargin = Unit.FromCentimeter(2.5);
        secao.PageSetup.TopMargin = Unit.FromCentimeter(2.2);
        secao.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        if (entrada.NomeDaEmpresa is { } empresa)
        {
            Centralizado(secao, empresa, 14, negrito: true, depois: 14);
        }

        Centralizado(secao, "Certificado de conclusão", 28, negrito: true, depois: 22);
        Centralizado(secao, "Certificamos que", 12, negrito: false, depois: 8);
        Centralizado(secao, entrada.NomeDaPessoa, 22, negrito: true, depois: 12);
        Centralizado(secao, "concluiu o treinamento", 12, negrito: false, depois: 8);
        Centralizado(secao, entrada.TituloDoTreinamento, 18, negrito: true, depois: 8);
        if (entrada.CargaHorariaMinutos is { } minutos && minutos > 0)
        {
            Centralizado(secao, $"com carga horária de {CargaHoraria(minutos)}", 12, negrito: false, depois: 18);
        }
        else
        {
            Centralizado(secao, string.Empty, 12, negrito: false, depois: 10);
        }

        Centralizado(secao, string.Create(CultureInfo.InvariantCulture, $"Emitido em {entrada.EmitidoEm:dd/MM/yyyy} · Certificado {entrada.Numero}"), 11, negrito: false, depois: 6);
        Centralizado(secao, $"Código de validação: {entrada.Codigo}", 10, negrito: false, depois: 4);
        if (entrada.EnderecoDaConferencia is { } endereco)
        {
            Centralizado(secao, $"Confira a autenticidade em {endereco} informando o código acima.", 9, negrito: false, depois: 0);
        }

        return documento;
    }

    private static void Centralizado(Section secao, string texto, double tamanho, bool negrito, double depois)
    {
        var paragrafo = secao.AddParagraph(texto);
        paragrafo.Format.Alignment = ParagraphAlignment.Center;
        paragrafo.Format.Font.Size = tamanho;
        paragrafo.Format.Font.Bold = negrito;
        paragrafo.Format.SpaceAfter = Unit.FromPoint(depois);
    }
}
