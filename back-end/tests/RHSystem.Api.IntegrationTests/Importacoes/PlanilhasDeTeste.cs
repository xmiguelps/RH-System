using System.Globalization;
using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RHSystem.Application.Importacoes;

namespace RHSystem.Api.IntegrationTests.Importacoes;

/// <summary>Texto da célula em várias partes (o Excel guarda assim o texto com formatação parcial): o leitor tem de juntar as partes.</summary>
public sealed record TextoRico(params string[] Partes);

/// <summary>Célula de fórmula: o Excel guarda o texto da fórmula e o último resultado; o leitor vale o resultado.</summary>
public sealed record FormulaGuardada(string Formula, string Resultado);

/// <summary>Data com o formato embutido número 14 do Excel (a tela do Brasil mostra <c>dd/mm/aaaa</c>); <see cref="DateOnly"/> puro usa o formato próprio <c>dd/mm/yyyy</c>.</summary>
public sealed record DataComFormatoEmbutido(DateOnly Data);

/// <summary>Número com um dos estilos da planilha de teste: 3 = <c>[Red]#,##0.00</c> (o <c>d</c> de Red, entre colchetes, não faz data); 4 = <c>0.00" dias"</c> (letra entre aspas não faz data); 5 = <c>[$-416]dd/mm/yyyy</c> (data com localidade entre colchetes).</summary>
public sealed record NumeroComEstilo(double Valor, int Estilo);

/// <summary>
/// Monta <c>.xlsx</c> para os testes da importação: com o próprio SDK (<see cref="Xlsx"/>), para o que o Excel faria, ou à mão (<see cref="XlsxManual"/>), para o que só um arquivo hostil faria. As células aceitam
/// <c>string</c>, número, <see cref="DateOnly"/>, <c>bool</c>, <see cref="TextoRico"/>, <see cref="FormulaGuardada"/>, <see cref="DataComFormatoEmbutido"/> e <c>null</c> (célula vazia). Uma LINHA <c>null</c> é linha em branco.
/// </summary>
public static class PlanilhasDeTeste
{
    public static readonly string[] TitulosDoModelo = ColunasDaImportacao.Todas.Select(ColunasDaImportacao.Titulo).ToArray();

    public const string EspacoDeNomes = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>Uma linha de dados com as 11 colunas na ordem do modelo.</summary>
    public static object?[] Linha(
        string? nome = "Ana Souza",
        string? cpf = "529.982.247-25",
        string? nascimento = "14/03/1989",
        string? telefone = "(11) 97766-3344",
        string? email = "ana.souza@empresa.com.br",
        string? admissao = "19/10/2026",
        string? contrato = "CLT",
        string? cargo = "Analista",
        string? setor = "Tecnologia",
        string? nivel = "Colaborador",
        string? beneficios = null) =>
        [nome, cpf, nascimento, telefone, email, admissao, contrato, cargo, setor, nivel, beneficios];

    public static byte[] Xlsx(
        IEnumerable<object?[]?> linhas,
        IReadOnlyList<string?>? titulos = null,
        string nomeDaAba = "Colaboradores",
        bool date1904 = false,
        int abasExtras = 0)
    {
        using var memoria = new MemoryStream();
        using (var documento = SpreadsheetDocument.Create(memoria, SpreadsheetDocumentType.Workbook))
        {
            var pasta = documento.AddWorkbookPart();
            pasta.Workbook = new Workbook();
            if (date1904)
            {
                pasta.Workbook.WorkbookProperties = new WorkbookProperties { Date1904 = true };
            }

            var estilos = pasta.AddNewPart<WorkbookStylesPart>();
            estilos.Stylesheet = new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat { NumberFormatId = 164, FormatCode = "dd/mm/yyyy" },
                    new NumberingFormat { NumberFormatId = 165, FormatCode = "[Red]#,##0.00" },
                    new NumberingFormat { NumberFormatId = 166, FormatCode = "0.00\" dias\"" },
                    new NumberingFormat { NumberFormatId = 167, FormatCode = "[$-416]dd/mm/yyyy" }),
                new Fonts(new Font()),
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
                new Borders(new Border()),
                new CellStyleFormats(new CellFormat { NumberFormatId = 14 }), // o Excel sempre traz os estilos de célula: os xf daqui NÃO contam como estilos de célula (o índice s de cada <c> aponta para cellXfs)
                new CellFormats(
                    new CellFormat(),
                    new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true }, // 1: data com formato próprio
                    new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true }, // 2: data com o formato embutido 14
                    new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true }, // 3: número
                    new CellFormat { NumberFormatId = 166, ApplyNumberFormat = true }, // 4: número com texto entre aspas
                    new CellFormat { NumberFormatId = 167, ApplyNumberFormat = true })); // 5: data com localidade entre colchetes

            var textos = pasta.AddNewPart<SharedStringTablePart>();
            textos.SharedStringTable = new SharedStringTable();
            var quantos = 0;
            int Guardar(SharedStringItem item)
            {
                textos.SharedStringTable.Append(item);
                return quantos++;
            }

            var abas = pasta.Workbook.AppendChild(new Sheets());
            uint idDaAba = 1;

            void AdicionarAba(string nome, IEnumerable<object?[]?> linhasDaAba)
            {
                var parte = pasta.AddNewPart<WorksheetPart>();
                var dados = new SheetData();
                parte.Worksheet = new Worksheet(dados);
                uint numero = 0;
                foreach (var linha in linhasDaAba)
                {
                    numero++;
                    if (linha is null)
                    {
                        continue;
                    }

                    var row = new Row { RowIndex = numero };
                    for (var coluna = 0; coluna < linha.Length; coluna++)
                    {
                        var celula = NovaCelula(linha[coluna], $"{NomeDaColuna(coluna)}{numero}", date1904, Guardar);
                        if (celula is not null)
                        {
                            row.Append(celula);
                        }
                    }

                    dados.Append(row);
                }

                abas.Append(new Sheet { Id = pasta.GetIdOfPart(parte), SheetId = idDaAba++, Name = nome });
            }

            var cabecalho = (titulos ?? TitulosDoModelo).Cast<object?>().ToArray();
            AdicionarAba(nomeDaAba, new object?[]?[] { cabecalho }.Concat(linhas));
            for (var extra = 0; extra < abasExtras; extra++)
            {
                AdicionarAba($"Extra {extra + 1}", [["x"]]);
            }
        }

        return memoria.ToArray();
    }

    private static string NomeDaColuna(int indice) => indice < 26 ? ((char)('A' + indice)).ToString() : $"{(char)('A' + (indice / 26) - 1)}{(char)('A' + (indice % 26))}";

    private static Cell? NovaCelula(object? valor, string referencia, bool date1904, Func<SharedStringItem, int> guardar)
    {
        static string Numero(double numero) => numero.ToString("R", CultureInfo.InvariantCulture);

        switch (valor)
        {
            case null:
                return null;
            case string texto:
                return new Cell { CellReference = referencia, DataType = CellValues.SharedString, CellValue = new CellValue(guardar(new SharedStringItem(new Text(texto) { Space = SpaceProcessingModeValues.Preserve })).ToString()) };
            case TextoRico rico:
                var partes = rico.Partes.Select(p => (OpenXmlElement)new Run(new Text(p) { Space = SpaceProcessingModeValues.Preserve })).ToArray();
                return new Cell { CellReference = referencia, DataType = CellValues.SharedString, CellValue = new CellValue(guardar(new SharedStringItem(partes)).ToString()) };
            case int or long or double:
                return new Cell { CellReference = referencia, DataType = CellValues.Number, CellValue = new CellValue(Numero(Convert.ToDouble(valor, CultureInfo.InvariantCulture))) };
            case DateOnly data:
                return new Cell { CellReference = referencia, StyleIndex = 1, CellValue = new CellValue(Numero(Serial(data, date1904))) };
            case DataComFormatoEmbutido embutida:
                return new Cell { CellReference = referencia, StyleIndex = 2, CellValue = new CellValue(Numero(Serial(embutida.Data, date1904))) };
            case NumeroComEstilo comEstilo:
                return new Cell { CellReference = referencia, StyleIndex = (uint)comEstilo.Estilo, CellValue = new CellValue(Numero(comEstilo.Valor)) };
            case bool booleano:
                return new Cell { CellReference = referencia, DataType = CellValues.Boolean, CellValue = new CellValue(booleano ? "1" : "0") };
            case FormulaGuardada formula:
                return new Cell { CellReference = referencia, DataType = CellValues.String, CellFormula = new CellFormula(formula.Formula), CellValue = new CellValue(formula.Resultado) };
            default:
                throw new ArgumentException($"Tipo de célula não suportado: {valor.GetType().Name}.", nameof(valor));
        }
    }

    private static double Serial(DateOnly data, bool date1904) =>
        (data.ToDateTime(TimeOnly.MinValue) - (date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 30))).TotalDays;

    // ---------------------------------------------------------------- à mão (arquivos que só um atacante ou outra ferramenta fariam)

    /// <summary>O XML de uma aba com células de texto em linha (<c>inlineStr</c>); cada item de <paramref name="linhas"/> é uma linha, na ordem das colunas A, B, C...</summary>
    public static string XmlDaAba(params string?[][] linhas)
    {
        var xml = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{EspacoDeNomes}\"><sheetData>");
        for (var numero = 0; numero < linhas.Length; numero++)
        {
            xml.Append($"<row r=\"{numero + 1}\">");
            for (var coluna = 0; coluna < linhas[numero].Length; coluna++)
            {
                if (linhas[numero][coluna] is { } texto)
                {
                    xml.Append($"<c r=\"{NomeDaColuna(coluna)}{numero + 1}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(texto)}</t></is></c>");
                }
            }

            xml.Append("</row>");
        }

        return xml.Append("</sheetData></worksheet>").ToString();
    }

    /// <summary>O XML da aba com o cabeçalho do modelo e as linhas dadas, em texto em linha.</summary>
    public static string XmlDaAbaDoModelo(params string?[][] linhas) => XmlDaAba([.. new[] { TitulosDoModelo.Cast<string?>().ToArray() }, .. linhas]);

    /// <summary>
    /// Um pacote <c>.xlsx</c> mínimo, escrito à mão, com a aba "Colaboradores" dada. <paramref name="xmlDosTextos"/> vira <c>xl/sharedStrings.xml</c>. <paramref name="espacosNoFinalDaAba"/> acrescenta essa quantidade de
    /// espaços depois do fim do XML da aba (espaço depois da raiz é XML válido e comprime a quase nada).
    /// </summary>
    public static byte[] XlsxManual(string xmlDaAba, string? xmlDosTextos = null, long espacosNoFinalDaAba = 0)
    {
        var tipos = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        tipos.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        tipos.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        tipos.Append("<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        var relacoes = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        relacoes.Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>");
        var entradas = new List<(string, byte[])>();
        if (xmlDosTextos is not null)
        {
            tipos.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
            relacoes.Append("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            entradas.Add(("xl/sharedStrings.xml", Encoding.UTF8.GetBytes(xmlDosTextos)));
        }

        tipos.Append("</Types>");
        relacoes.Append("</Relationships>");
        entradas.Add(("[Content_Types].xml", Encoding.UTF8.GetBytes(tipos.ToString())));
        entradas.Add(("_rels/.rels", Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>")));
        entradas.Add(("xl/workbook.xml", Encoding.UTF8.GetBytes($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{EspacoDeNomes}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Colaboradores\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>")));
        entradas.Add(("xl/_rels/workbook.xml.rels", Encoding.UTF8.GetBytes(relacoes.ToString())));
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nome, conteudo) in entradas)
            {
                using var fluxo = zip.CreateEntry(nome, CompressionLevel.SmallestSize).Open();
                fluxo.Write(conteudo);
            }

            using var aba = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize).Open();
            aba.Write(Encoding.UTF8.GetBytes(xmlDaAba));
            var bloco = Enumerable.Repeat((byte)' ', 1024 * 1024).ToArray();
            for (long escritos = 0; escritos < espacosNoFinalDaAba; escritos += bloco.Length)
            {
                aba.Write(bloco, 0, (int)Math.Min(bloco.Length, espacosNoFinalDaAba - escritos));
            }
        }

        return memoria.ToArray();
    }

    public static byte[] Zip(params (string Nome, byte[] Conteudo)[] entradas)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nome, conteudo) in entradas)
            {
                using var fluxo = zip.CreateEntry(nome, CompressionLevel.SmallestSize).Open();
                fluxo.Write(conteudo);
            }
        }

        return memoria.ToArray();
    }

    /// <summary>O mesmo zip com mais uma entrada de zeros (que comprime quase a nada): a bomba de descompressão.</summary>
    public static byte[] ComEntradaDeZeros(byte[] zipOriginal, string nome, long bytes)
    {
        using var memoria = new MemoryStream();
        memoria.Write(zipOriginal);
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Update, leaveOpen: true))
        {
            using var fluxo = zip.CreateEntry(nome, CompressionLevel.SmallestSize).Open();
            var bloco = new byte[1024 * 1024];
            for (long escrito = 0; escrito < bytes; escrito += bloco.Length)
            {
                fluxo.Write(bloco, 0, (int)Math.Min(bloco.Length, bytes - escrito));
            }
        }

        return memoria.ToArray();
    }

    /// <summary>O mesmo zip sem a entrada <paramref name="nome"/>.</summary>
    public static byte[] SemEntrada(byte[] zip, string nome)
    {
        using var origem = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var restantes = origem.Entries
            .Where(e => !e.FullName.Equals(nome, StringComparison.OrdinalIgnoreCase))
            .Select(e =>
            {
                using var fluxo = e.Open();
                using var copia = new MemoryStream();
                fluxo.CopyTo(copia);
                return (e.FullName, copia.ToArray());
            })
            .ToArray();
        return restantes.Length == origem.Entries.Count ? throw new InvalidOperationException($"A entrada {nome} não existe: o teste está errado.") : Zip(restantes);
    }

    public static int QuantasEntradas(byte[] zip)
    {
        using var arquivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return arquivo.Entries.Count;
    }

    public static long SomaDosTamanhosDeclarados(byte[] zip)
    {
        using var arquivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return arquivo.Entries.Sum(e => e.Length);
    }

    /// <summary>O mesmo zip com <paramref name="quantas"/> entradas pequenas a mais.</summary>
    public static byte[] ComEntradasExtras(byte[] zipOriginal, int quantas)
    {
        using var memoria = new MemoryStream();
        memoria.Write(zipOriginal);
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Update, leaveOpen: true))
        {
            for (var i = 0; i < quantas; i++)
            {
                using var fluxo = zip.CreateEntry($"extra/{i}.txt").Open();
                fluxo.WriteByte((byte)'x');
            }
        }

        return memoria.ToArray();
    }

    /// <summary>
    /// Troca, no cabeçalho local e no diretório central do zip, o tamanho descompactado declarado de <paramref name="nomeDaEntrada"/>: o cabeçalho MENTE (diz <paramref name="declarado"/>, os dados têm mais).
    /// </summary>
    public static byte[] ComTamanhoDeclaradoMentindo(byte[] zip, string nomeDaEntrada, uint declarado)
    {
        var copia = (byte[])zip.Clone();
        var nome = Encoding.UTF8.GetBytes(nomeDaEntrada);
        var tocados = 0;
        for (var i = 0; i + 46 + nome.Length <= copia.Length; i++)
        {
            // diretório central: 50 4B 01 02, tamanho descompactado em +24, nome em +46; cabeçalho local: 50 4B 03 04, tamanho em +22, nome em +30
            var central = copia[i] == 0x50 && copia[i + 1] == 0x4B && copia[i + 2] == 0x01 && copia[i + 3] == 0x02 && copia.AsSpan(i + 46, nome.Length).SequenceEqual(nome);
            var local = copia[i] == 0x50 && copia[i + 1] == 0x4B && copia[i + 2] == 0x03 && copia[i + 3] == 0x04 && copia.AsSpan(i + 30, nome.Length).SequenceEqual(nome);
            if (central)
            {
                BitConverter.GetBytes(declarado).CopyTo(copia, i + 24);
                tocados++;
            }
            else if (local)
            {
                BitConverter.GetBytes(declarado).CopyTo(copia, i + 22);
                tocados++;
            }
        }

        return tocados > 0 ? copia : throw new InvalidOperationException("A entrada não foi achada no zip: o teste está errado.");
    }
}
