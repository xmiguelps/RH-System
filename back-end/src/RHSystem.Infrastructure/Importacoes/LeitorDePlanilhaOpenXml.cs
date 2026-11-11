using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using RHSystem.Application.Comum;
using RHSystem.Application.Importacoes;

namespace RHSystem.Infrastructure.Importacoes;

/// <summary>
/// Lê a aba "Colaboradores" de um <c>.xlsx</c> com <see cref="XmlReader"/> direto sobre o zip (o formato Open XML, ECMA-376): é o que dá controle sobre os limites e sobre o XML hostil. <b>O XML nunca aceita DTD</b>
/// (<see cref="DtdProcessing.Prohibit"/>, sem resolvedor): entidade externa e "billion laughs" morrem no <c>&lt;!DOCTYPE</c>. O tamanho que o cabeçalho do zip diz nunca é confiado: a soma declarada é conferida de
/// saída e tudo o que se lê passa por um fluxo que conta os bytes REAIS. Qualquer problema do CONTEÚDO vira 422 (<see cref="ErrosDePlanilha"/>), sem repetir o que estava nas células.
/// </summary>
public sealed class LeitorDePlanilhaOpenXml : ILeitorDePlanilha
{
    private const string NomeDaAba = "Colaboradores";
    private const string EspacoDeNomesDasRelacoes = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const long TamanhoMaximoDasPartesPequenas = 5L * 1024 * 1024;
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public PlanilhaLida Ler(Stream xlsx)
    {
        try
        {
            var bytes = LerComLimite(xlsx);
            using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
            return new Leitura(zip).Executar();
        }
        catch (ProblemaDeApiException)
        {
            throw;
        }
        catch (LimiteExcedidoException)
        {
            throw ErrosDePlanilha.MuitoGrande();
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception)
        {
            // zip quebrado, XML malformado, DTD, índice fora do intervalo...: o conteúdo do arquivo nunca derruba a API e nunca vai para a mensagem.
            throw ErrosDePlanilha.NaoEExcel();
        }
    }

    private static byte[] LerComLimite(Stream origem)
    {
        using var memoria = new MemoryStream();
        var bloco = new byte[81920];
        int lidos;
        while ((lidos = origem.Read(bloco, 0, bloco.Length)) > 0)
        {
            memoria.Write(bloco, 0, lidos);
            if (memoria.Length > LimitesDeImportacao.TamanhoMaximoBytes)
            {
                throw new LimiteExcedidoException();
            }
        }

        return memoria.ToArray();
    }

    /// <summary>Estourou um limite de tamanho, de entradas, de planilhas ou de células.</summary>
    private sealed class LimiteExcedidoException : Exception;

    /// <summary>Conta os bytes que a descompressão realmente entrega, entre todas as partes lidas.</summary>
    private sealed class FluxoContado(Stream origem, Leitura dona) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var lidos = origem.Read(buffer, offset, count);
            dona.Contar(lidos);
            return lidos;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin referencia) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                origem.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class Leitura
    {
        private readonly Dictionary<string, ZipArchiveEntry> _entradas;
        private long _lidos;

        public Leitura(ZipArchive zip)
        {
            if (zip.Entries.Count > LimitesDeImportacao.EntradasMaximas)
            {
                throw new LimiteExcedidoException();
            }

            long declarado = 0;
            foreach (var entrada in zip.Entries)
            {
                declarado += entrada.Length;
                if (declarado > LimitesDeImportacao.DescompactadoMaximoBytes)
                {
                    throw new LimiteExcedidoException();
                }
            }

            _entradas = zip.Entries.ToDictionary(e => e.FullName.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase);
        }

        public void Contar(int bytes)
        {
            _lidos += bytes;
            if (_lidos > LimitesDeImportacao.DescompactadoMaximoBytes)
            {
                throw new LimiteExcedidoException();
            }
        }

        public PlanilhaLida Executar()
        {
            if (!_entradas.ContainsKey("[Content_Types].xml") || !_entradas.ContainsKey("xl/workbook.xml"))
            {
                throw ErrosDePlanilha.NaoEExcel();
            }

            var relacoes = LerRelacoes();
            var (abas, data1904) = LerPasta(); // o limite de planilhas é conferido enquanto a pasta é lida
            var indiceDaAba = abas.FindIndex(a => string.Equals(a.Nome.Trim(), NomeDaAba, StringComparison.OrdinalIgnoreCase));
            if (indiceDaAba < 0)
            {
                throw ErrosDePlanilha.ModeloAlterado();
            }

            if (!relacoes.TryGetValue(abas[indiceDaAba].IdDaRelacao, out var alvoDaAba))
            {
                throw ErrosDePlanilha.NaoEExcel();
            }

            var textos = LerTextosCompartilhados(AlvoDoTipo(relacoes, "/sharedStrings") ?? "xl/sharedStrings.xml");
            var estilosDeData = LerEstilosDeData(AlvoDoTipo(relacoes, "/styles") ?? "xl/styles.xml");
            return LerAba(Caminho(alvoDaAba.Alvo), textos, estilosDeData, data1904);
        }

        private static string? AlvoDoTipo(Dictionary<string, (string Tipo, string Alvo)> relacoes, string sufixoDoTipo)
        {
            foreach (var (tipo, alvo) in relacoes.Values)
            {
                if (tipo.EndsWith(sufixoDoTipo, StringComparison.OrdinalIgnoreCase))
                {
                    return Caminho(alvo);
                }
            }

            return null;
        }

        /// <summary>O alvo de uma relação de <c>xl/_rels/workbook.xml.rels</c>, como caminho dentro do zip.</summary>
        private static string Caminho(string alvo)
        {
            var segmentos = new List<string>();
            foreach (var parte in (alvo.StartsWith('/') ? alvo[1..] : "xl/" + alvo).Split('/'))
            {
                if (parte == "..")
                {
                    if (segmentos.Count > 0)
                    {
                        segmentos.RemoveAt(segmentos.Count - 1);
                    }
                }
                else if (parte is not ("" or "."))
                {
                    segmentos.Add(parte);
                }
            }

            return string.Join('/', segmentos);
        }

        private XmlReader Abrir(string caminho, long maximoDeclarado = long.MaxValue)
        {
            if (!_entradas.TryGetValue(caminho, out var entrada))
            {
                throw ErrosDePlanilha.NaoEExcel();
            }

            if (entrada.Length > maximoDeclarado)
            {
                throw new LimiteExcedidoException();
            }

            return XmlReader.Create(
                new FluxoContado(entrada.Open(), this),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = true, MaxCharactersInDocument = 0 });
        }

        private Dictionary<string, (string Tipo, string Alvo)> LerRelacoes()
        {
            var relacoes = new Dictionary<string, (string Tipo, string Alvo)>(StringComparer.Ordinal);
            if (!_entradas.ContainsKey("xl/_rels/workbook.xml.rels"))
            {
                return relacoes;
            }

            using var leitor = Abrir("xl/_rels/workbook.xml.rels", TamanhoMaximoDasPartesPequenas);
            while (leitor.Read())
            {
                if (leitor.NodeType == XmlNodeType.Element && leitor.LocalName == "Relationship"
                    && leitor.GetAttribute("Id") is { } id && leitor.GetAttribute("Target") is { } alvo)
                {
                    relacoes[id] = (leitor.GetAttribute("Type") ?? string.Empty, alvo);
                }
            }

            return relacoes;
        }

        private (List<(string Nome, string IdDaRelacao)> Abas, bool Data1904) LerPasta()
        {
            var abas = new List<(string, string)>();
            var data1904 = false;
            using var leitor = Abrir("xl/workbook.xml", TamanhoMaximoDasPartesPequenas);
            while (leitor.Read())
            {
                if (leitor.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (leitor.LocalName == "sheet")
                {
                    abas.Add((leitor.GetAttribute("name") ?? string.Empty, leitor.GetAttribute("id", EspacoDeNomesDasRelacoes) ?? string.Empty));
                    if (abas.Count > LimitesDeImportacao.PlanilhasMaximas)
                    {
                        throw new LimiteExcedidoException();
                    }
                }
                else if (leitor.LocalName == "workbookPr")
                {
                    data1904 = leitor.GetAttribute("date1904") is "1" or "true";
                }
            }

            return (abas, data1904);
        }

        private List<string> LerTextosCompartilhados(string caminho)
        {
            var textos = new List<string>();
            if (!_entradas.ContainsKey(caminho))
            {
                return textos;
            }

            var texto = new StringBuilder();
            var dentroDeItem = false;
            using var leitor = Abrir(caminho);
            var ler = true;
            while (true)
            {
                if (ler && !leitor.Read())
                {
                    break;
                }

                ler = true;
                if (leitor.NodeType == XmlNodeType.Element)
                {
                    switch (leitor.LocalName)
                    {
                        case "si":
                            if (textos.Count >= LimitesDeImportacao.CelulasMaximas)
                            {
                                throw new LimiteExcedidoException();
                            }

                            texto.Clear();
                            if (leitor.IsEmptyElement)
                            {
                                textos.Add(string.Empty);
                            }
                            else
                            {
                                dentroDeItem = true;
                            }

                            break;
                        case "rPh" when dentroDeItem: // a leitura fonética (japonês) não é parte do texto
                            leitor.Skip();
                            ler = false;
                            break;
                        case "t" when dentroDeItem:
                            texto.Append(leitor.ReadElementContentAsString());
                            ler = false;
                            break;
                    }
                }
                else if (leitor.NodeType == XmlNodeType.EndElement && leitor.LocalName == "si" && dentroDeItem)
                {
                    textos.Add(texto.ToString());
                    dentroDeItem = false;
                }
            }

            return textos;
        }

        private HashSet<int> LerEstilosDeData(string caminho)
        {
            var estilosDeData = new HashSet<int>();
            if (!_entradas.ContainsKey(caminho))
            {
                return estilosDeData;
            }

            var formatos = new Dictionary<int, string>();
            var idsDosEstilos = new List<int>();
            var emCellXfs = false;
            using (var leitor = Abrir(caminho, TamanhoMaximoDasPartesPequenas))
            {
                while (leitor.Read())
                {
                    if (leitor.NodeType == XmlNodeType.Element)
                    {
                        switch (leitor.LocalName)
                        {
                            case "numFmt" when int.TryParse(leitor.GetAttribute("numFmtId"), NumberStyles.None, Invariante, out var id):
                                formatos[id] = leitor.GetAttribute("formatCode") ?? string.Empty;
                                break;
                            case "cellXfs":
                                emCellXfs = true;
                                break;
                            case "xf" when emCellXfs:
                                idsDosEstilos.Add(int.TryParse(leitor.GetAttribute("numFmtId"), NumberStyles.None, Invariante, out var formato) ? formato : 0);
                                break;
                        }
                    }
                    else if (leitor.NodeType == XmlNodeType.EndElement && leitor.LocalName == "cellXfs")
                    {
                        emCellXfs = false;
                    }
                }
            }

            for (var indice = 0; indice < idsDosEstilos.Count; indice++)
            {
                if (EhFormatoDeData(idsDosEstilos[indice], formatos))
                {
                    estilosDeData.Add(indice);
                }
            }

            return estilosDeData;
        }

        private static bool EhFormatoDeData(int id, Dictionary<int, string> formatos) =>
            id is (>= 14 and <= 22) or (>= 27 and <= 36) or (>= 45 and <= 47) or (>= 50 and <= 58)
            || (formatos.TryGetValue(id, out var codigo) && CodigoEDeData(codigo));

        /// <summary>Um código de formato é de data quando, fora de aspas, de colchetes e de caracteres escapados, tem <c>d</c>, <c>m</c>, <c>y</c>, <c>h</c> ou <c>s</c>. Só a primeira seção (antes do <c>;</c>) conta.</summary>
        private static bool CodigoEDeData(string codigo)
        {
            var emAspas = false;
            var emColchetes = false;
            for (var i = 0; i < codigo.Length; i++)
            {
                var letra = codigo[i];
                if (letra == '"')
                {
                    emAspas = !emAspas;
                }
                else if (emAspas)
                {
                    continue;
                }
                else if (letra == '[')
                {
                    emColchetes = true;
                }
                else if (letra == ']')
                {
                    emColchetes = false;
                }
                else if (emColchetes)
                {
                    continue;
                }
                else if (letra == '\\' || letra == '_' || letra == '*')
                {
                    i++; // o próximo caractere é literal
                }
                else if (letra == ';')
                {
                    return false;
                }
                else if (char.ToLowerInvariant(letra) is 'd' or 'm' or 'y' or 'h' or 's')
                {
                    return true;
                }
            }

            return false;
        }

        private PlanilhaLida LerAba(string caminho, List<string> textos, HashSet<int> estilosDeData, bool data1904)
        {
            var colunas = ColunasDaImportacao.Todas.Count;
            var guardadas = new List<LinhaLida>();
            var celulasLidas = 0;
            var temCabecalho = false;

            var linhaAtual = 0;
            string?[]? celulas = null;
            var colunaForaDoModelo = false;
            var colunaAtual = -1;
            string? tipo = null;
            var estilo = 0;
            string? valor = null;
            var emCelula = false;
            var dentroDeTextoEmLinha = false;
            var texto = new StringBuilder();

            void FecharCelula()
            {
                emCelula = false;
                var conteudo = ValorDaCelula(tipo, estilo, valor, texto, colunaAtual, textos, estilosDeData, data1904);
                if (conteudo is not null)
                {
                    if (colunaAtual is >= 0 && colunaAtual < colunas && celulas is not null)
                    {
                        celulas[colunaAtual] = conteudo;
                    }
                    else
                    {
                        colunaForaDoModelo = true;
                    }
                }
            }

            void FecharLinha()
            {
                if (celulas is not null)
                {
                    if (linhaAtual == 1)
                    {
                        temCabecalho = true;
                        if (colunaForaDoModelo || !CabecalhoConfere(celulas))
                        {
                            throw ErrosDePlanilha.ModeloAlterado();
                        }
                    }
                    else if (linhaAtual > 1 && celulas.Any(c => c is not null))
                    {
                        guardadas.Add(new LinhaLida(linhaAtual, celulas)); // limitado pelo teto de células, não de linhas: o total real entra na mensagem do 422
                    }
                }

                celulas = null;
            }

            using var leitor = Abrir(caminho);
            var ler = true;
            while (true)
            {
                if (ler && !leitor.Read())
                {
                    break;
                }

                ler = true;
                if (leitor.NodeType == XmlNodeType.Element)
                {
                    switch (leitor.LocalName)
                    {
                        case "row":
                            linhaAtual = int.TryParse(leitor.GetAttribute("r"), NumberStyles.None, Invariante, out var numero) ? numero : linhaAtual + 1;
                            celulas = new string?[colunas];
                            colunaForaDoModelo = false;
                            colunaAtual = -1;
                            if (leitor.IsEmptyElement)
                            {
                                FecharLinha();
                            }

                            break;
                        case "c":
                            if (++celulasLidas > LimitesDeImportacao.CelulasMaximas)
                            {
                                throw new LimiteExcedidoException();
                            }

                            colunaAtual = ColunaDaReferencia(leitor.GetAttribute("r"), colunaAtual + 1);
                            tipo = leitor.GetAttribute("t");
                            estilo = int.TryParse(leitor.GetAttribute("s"), NumberStyles.None, Invariante, out var s) ? s : 0;
                            valor = null;
                            texto.Clear();
                            emCelula = true;
                            dentroDeTextoEmLinha = false;
                            if (leitor.IsEmptyElement)
                            {
                                FecharCelula();
                            }

                            break;
                        case "v" when emCelula:
                            valor = leitor.ReadElementContentAsString();
                            ler = false;
                            break;
                        case "is" when emCelula:
                            dentroDeTextoEmLinha = !leitor.IsEmptyElement;
                            break;
                        case "rPh" when dentroDeTextoEmLinha:
                            leitor.Skip();
                            ler = false;
                            break;
                        case "t" when dentroDeTextoEmLinha:
                            texto.Append(leitor.ReadElementContentAsString());
                            ler = false;
                            break;
                    }
                }
                else if (leitor.NodeType == XmlNodeType.EndElement)
                {
                    switch (leitor.LocalName)
                    {
                        case "c" when emCelula:
                            FecharCelula();
                            break;
                        case "is":
                            dentroDeTextoEmLinha = false;
                            break;
                        case "row":
                            FecharLinha();
                            break;
                    }
                }
            }

            if (!temCabecalho)
            {
                throw ErrosDePlanilha.ModeloAlterado();
            }

            if (guardadas.Count == 0)
            {
                throw ErrosDePlanilha.Vazia();
            }

            if (guardadas.Count > LimitesDeImportacao.LinhasMaximas)
            {
                throw ErrosDePlanilha.LimiteDeLinhas(guardadas.Count);
            }

            return new PlanilhaLida(guardadas);
        }

        private static bool CabecalhoConfere(string?[] celulas)
        {
            for (var i = 0; i < ColunasDaImportacao.Todas.Count; i++)
            {
                if (NomesNormalizados.Chave(celulas[i]) != NomesNormalizados.Chave(ColunasDaImportacao.Titulo(ColunasDaImportacao.Todas[i])))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>O índice (de 0) da coluna da referência <c>"B7"</c> ou <c>"AA3"</c>; sem referência, a posição seguinte à da célula anterior.</summary>
        private static int ColunaDaReferencia(string? referencia, int seguinte)
        {
            if (string.IsNullOrEmpty(referencia))
            {
                return seguinte;
            }

            var coluna = 0;
            var letras = 0;
            foreach (var letra in referencia)
            {
                if (letra is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                {
                    coluna = (coluna * 26) + (char.ToUpperInvariant(letra) - 'A' + 1);
                    letras++;
                    if (letras > 3)
                    {
                        return seguinte; // nenhuma planilha do Excel tem mais de 16384 colunas (XFD)
                    }
                }
                else
                {
                    break;
                }
            }

            return letras == 0 ? seguinte : coluna - 1;
        }

        private static string? ValorDaCelula(string? tipo, int estilo, string? valor, StringBuilder textoEmLinha, int coluna, List<string> textos, HashSet<int> estilosDeData, bool data1904)
        {
            string? conteudo = tipo switch
            {
                "inlineStr" => textoEmLinha.ToString(),
                "s" => valor is not null && int.TryParse(valor, NumberStyles.None, Invariante, out var indice) ? textos[indice] : null,
                "b" => valor is null ? null : valor is "1" or "true" ? "VERDADEIRO" : "FALSO",
                "str" or "e" or "d" => valor,
                _ => valor is null ? null : TextoDoNumero(valor, estilosDeData.Contains(estilo), coluna == (int)ColunaDaImportacao.Cpf, data1904),
            };

            conteudo = conteudo?.Trim();
            return string.IsNullOrEmpty(conteudo) ? null : conteudo;
        }

        private static string TextoDoNumero(string valor, bool ehData, bool ehCpf, bool data1904)
        {
            if (!double.TryParse(valor, NumberStyles.Float, Invariante, out var numero) || double.IsNaN(numero) || double.IsInfinity(numero))
            {
                return valor;
            }

            if (ehData)
            {
                var serial = data1904 ? numero + 1462 : numero;
                if (serial is >= 1 and < 2_958_466)
                {
                    return DateTime.FromOADate(serial).ToString("dd/MM/yyyy", Invariante);
                }
            }

            if (numero == Math.Floor(numero) && Math.Abs(numero) < 1e15)
            {
                var inteiro = ((long)numero).ToString(Invariante);
                return ehCpf && numero >= 0 && inteiro.Length < 11 ? inteiro.PadLeft(11, '0') : inteiro; // o Excel tira o zero à esquerda do CPF digitado como número
            }

            return numero.ToString("R", Invariante);
        }
    }
}
