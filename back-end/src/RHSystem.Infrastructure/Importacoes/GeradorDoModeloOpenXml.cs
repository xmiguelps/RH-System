using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RHSystem.Application.Importacoes;

namespace RHSystem.Infrastructure.Importacoes;

/// <summary>
/// Gera o modelo com o SDK Open XML. Aba 1, "Colaboradores": só os 11 títulos (em negrito), com CPF e telefone em formato TEXTO (o Excel não tira o zero à esquerda) e as duas datas em <c>dd/mm/yyyy</c>. Aba 2, "Valores válidos":
/// só para consulta, uma coluna por lista. Texto em linha (<c>inlineStr</c>): o arquivo não precisa de tabela de textos e o leitor da própria importação o lê do mesmo jeito.
/// </summary>
public sealed class GeradorDoModeloOpenXml : IGeradorDoModelo
{
    private const uint EstiloTitulo = 1;
    private const uint EstiloTexto = 2;
    private const uint EstiloData = 3;

    public byte[] Gerar(ValoresValidosDoModelo valores)
    {
        using var memoria = new MemoryStream();
        using (var documento = SpreadsheetDocument.Create(memoria, SpreadsheetDocumentType.Workbook))
        {
            var pasta = documento.AddWorkbookPart();
            pasta.Workbook = new Workbook();
            var estilos = pasta.AddNewPart<WorkbookStylesPart>();
            estilos.Stylesheet = new Stylesheet(
                new NumberingFormats(new NumberingFormat { NumberFormatId = 164, FormatCode = "dd/mm/yyyy" }),
                new Fonts(new Font(), new Font(new Bold())),
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
                new Borders(new Border()),
                new CellFormats(
                    new CellFormat(), // 0: geral
                    new CellFormat { FontId = 1, ApplyFont = true }, // 1: título
                    new CellFormat { NumberFormatId = 49, ApplyNumberFormat = true }, // 2: texto (@)
                    new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true })); // 3: dd/mm/yyyy

            var abas = pasta.Workbook.AppendChild(new Sheets());
            AdicionarAba(pasta, abas, 1, "Colaboradores", ColunasDoModelo(), [ColunasDaImportacao.Todas.Select(c => ColunasDaImportacao.Titulo(c)).ToArray()]);
            AdicionarAba(pasta, abas, 2, "Valores válidos", ColunasDosValores(), LinhasDosValores(valores));
        }

        return memoria.ToArray();
    }

    private static Columns ColunasDoModelo()
    {
        var colunas = new Columns();
        uint posicao = 1;
        foreach (var coluna in ColunasDaImportacao.Todas)
        {
            colunas.Append(new Column
            {
                Min = posicao,
                Max = posicao,
                Width = LarguraDe(coluna),
                CustomWidth = true,
                Style = EstiloDa(coluna),
            });
            posicao++;
        }

        return colunas;
    }

    private static uint EstiloDa(ColunaDaImportacao coluna) => coluna switch
    {
        ColunaDaImportacao.Cpf or ColunaDaImportacao.Telefone => EstiloTexto,
        ColunaDaImportacao.DataNascimento or ColunaDaImportacao.DataAdmissao => EstiloData,
        _ => 0,
    };

    private static double LarguraDe(ColunaDaImportacao coluna) => coluna switch
    {
        ColunaDaImportacao.Nome or ColunaDaImportacao.EmailCorporativo or ColunaDaImportacao.Beneficios => 32,
        ColunaDaImportacao.Cargo or ColunaDaImportacao.Setor => 24,
        _ => 18,
    };

    private static Columns ColunasDosValores()
    {
        var colunas = new Columns();
        for (uint posicao = 1; posicao <= 5; posicao++)
        {
            colunas.Append(new Column { Min = posicao, Max = posicao, Width = 28, CustomWidth = true });
        }

        return colunas;
    }

    private static IReadOnlyList<string?[]> LinhasDosValores(ValoresValidosDoModelo valores)
    {
        string[][] listas = [[.. valores.Cargos], [.. valores.Setores], [.. ValoresValidosDoModelo.TiposDeContrato], [.. ValoresValidosDoModelo.NiveisDeAcesso], [.. valores.Beneficios]];
        var linhas = new List<string?[]> { new string?[] { "Cargos", "Setores", "Tipos de contrato", "Níveis de acesso", "Benefícios" } };
        for (var indice = 0; indice < listas.Max(l => l.Length); indice++)
        {
            linhas.Add(listas.Select(l => indice < l.Length ? l[indice] : null).ToArray());
        }

        return linhas;
    }

    private static void AdicionarAba(WorkbookPart pasta, Sheets abas, uint id, string nome, Columns colunas, IReadOnlyList<string?[]> linhas)
    {
        var parte = pasta.AddNewPart<WorksheetPart>();
        var dados = new SheetData();
        parte.Worksheet = new Worksheet(colunas, dados);
        for (var numero = 0; numero < linhas.Count; numero++)
        {
            var linha = new Row { RowIndex = (uint)(numero + 1) };
            for (var coluna = 0; coluna < linhas[numero].Length; coluna++)
            {
                if (linhas[numero][coluna] is { } texto)
                {
                    linha.Append(new Cell
                    {
                        CellReference = $"{(char)('A' + coluna)}{numero + 1}",
                        DataType = CellValues.InlineString,
                        StyleIndex = numero == 0 ? EstiloTitulo : 0,
                        InlineString = new InlineString(new Text(texto)),
                    });
                }
            }

            dados.Append(linha);
        }

        abas.Append(new Sheet { Id = pasta.GetIdOfPart(parte), SheetId = id, Name = nome });
    }
}
