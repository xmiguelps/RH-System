using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RHSystem.Application.Arquivos;
using RHSystem.Demo.Exemplos;
using UglyToad.PdfPig;

namespace RHSystem.Api.IntegrationTests.Demo;

/// <summary>Os PDFs que a ferramenta da demo manda: têm de ser aceitos pela API (assinatura e tamanho), abrir num leitor de verdade e dizer o que o nome do arquivo promete, mesmo com acento, parêntese e barra invertida no nome.</summary>
public class PdfsDeExemploTests
{
    private static string Compacto(string texto) => new([.. texto.Where(c => !char.IsWhiteSpace(c))]);

    private static string TextoDoPdf(byte[] bytes)
    {
        using var documento = PdfDocument.Open(bytes);
        return Compacto(string.Join(' ', documento.GetPages().Select(p => p.Text)));
    }

    [Fact]
    public void O_holerite_e_aceito_como_PDF_pela_API_e_cabe_no_limite_de_5_MB()
    {
        var bytes = PdfsDeExemplo.Holerite("Lucas Pereira", "Desenvolvedor", "Tecnologia", 9, 2026);

        Assert.Equal("application/pdf", IdentificadorDeTipoDeArquivo.Identificar(bytes)?.TipoDeConteudo);
        Assert.InRange(bytes.Length, 500, 5 * 1024 * 1024);
    }

    [Fact]
    public void O_holerite_traz_nome_cargo_setor_competencia_e_o_aviso_de_que_e_ficticio()
    {
        var texto = TextoDoPdf(PdfsDeExemplo.Holerite("Patrícia Gomes", "Assistente Comercial", "Comercial", 8, 2026));

        Assert.Contains("PatríciaGomes", texto);
        Assert.Contains("AssistenteComercial", texto);
        Assert.Contains("Comercial", texto);
        Assert.Contains("Agosto/2026", texto);
        Assert.Contains("fictício", texto);
    }

    [Fact]
    public void O_holerite_tem_proventos_descontos_e_o_liquido_que_bate_com_a_conta()
    {
        var texto = TextoDoPdf(PdfsDeExemplo.Holerite("Lucas Pereira", "Desenvolvedor", "Tecnologia", 9, 2026));

        Assert.Contains("Saláriobase", texto); // o texto compacto não tem espaços
        var (bruto, inss, irrf, liquido) = PdfsDeExemplo.ValoresDoHolerite("Lucas Pereira");
        Assert.Equal(bruto - inss - irrf, liquido);
        Assert.InRange(bruto, 3000m, 9000m);
        Assert.Contains(PdfsDeExemplo.Reais(liquido).Replace(" ", string.Empty), texto);
    }

    [Fact]
    public void O_mesmo_pedido_gera_exatamente_os_mesmos_bytes_e_pessoas_diferentes_geram_valores_diferentes()
    {
        var um = PdfsDeExemplo.Holerite("Lucas Pereira", "Desenvolvedor", "Tecnologia", 9, 2026);
        var dois = PdfsDeExemplo.Holerite("Lucas Pereira", "Desenvolvedor", "Tecnologia", 9, 2026);

        Assert.Equal(um, dois);
        Assert.NotEqual(PdfsDeExemplo.ValoresDoHolerite("Lucas Pereira").Bruto, PdfsDeExemplo.ValoresDoHolerite("Camila Duarte").Bruto);
    }

    [Theory]
    [InlineData(1000, "R$ 1.000,00")]
    [InlineData(4500.5, "R$ 4.500,50")]
    [InlineData(999.99, "R$ 999,99")]
    [InlineData(12345.6, "R$ 12.345,60")]
    public void Os_valores_saem_no_formato_brasileiro_qualquer_que_seja_a_cultura_da_maquina(double valor, string esperado) =>
        Assert.Equal(esperado, PdfsDeExemplo.Reais((decimal)valor));

    [Theory]
    [InlineData("Ana ) Silva (", "Ana)Silva(")] // parênteses desbalanceados: sem o escape o PDF perde o resto da linha
    [InlineData("Bia \\ Costa", "Bia\\Costa")]
    [InlineData("João d’Ávila Çelik", "Joãod'ÁvilaÇelik")] // o apóstrofo tipográfico (do teclado do celular) vira o reto
    [InlineData("Ana “Ani” – Lima", "Ana\"Ani\"-Lima")] // aspas e travessão tipográficos
    [InlineData("Ünïcode ☃ fora", "Ünïcode?fora")] // o que o Latin-1 não tem vira ?
    public void O_nome_sai_no_PDF_como_foi_escrito_mesmo_com_parentese_barra_aspas_ou_caractere_fora_do_alfabeto(string nome, string esperadoCompacto)
    {
        var texto = TextoDoPdf(PdfsDeExemplo.Holerite(nome, "Cargo", "Setor", 1, 2026));

        Assert.Contains($"Funcionário:{esperadoCompacto}Cargo:Cargo", texto); // a linha seguinte (o cargo) ainda aparece: o nome não engoliu o resto
    }

    [Fact]
    public void O_atestado_diz_quem_o_periodo_e_o_numero_de_dias_e_nao_traz_diagnostico()
    {
        var bytes = PdfsDeExemplo.Atestado("Thiago Mello", new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 30));

        var texto = TextoDoPdf(bytes);
        Assert.Equal("application/pdf", IdentificadorDeTipoDeArquivo.Identificar(bytes)?.TipoDeConteudo);
        Assert.Contains("ThiagoMello", texto);
        Assert.Contains("29/09/2026a30/09/2026", texto);
        Assert.Contains("(2dias)", texto);
        Assert.Contains("fictício", texto);
        Assert.Contains("CIDnãoinformado", texto);
    }

    [Fact]
    public void O_atestado_de_um_dia_diz_um_dia_no_singular()
    {
        var texto = TextoDoPdf(PdfsDeExemplo.Atestado("Thiago Mello", new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30)));

        Assert.Contains("(1dia)", texto);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_tabela_xref_o_startxref_e_o_Length_apontam_para_o_lugar_certo_como_exigem_os_leitores_estritos(bool holerite)
    {
        var bytes = holerite
            ? PdfsDeExemplo.Holerite("Lucas Pereira", "Desenvolvedor", "Tecnologia", 9, 2026)
            : PdfsDeExemplo.Atestado("Thiago Mello", new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30));
        var texto = Encoding.Latin1.GetString(bytes); // Latin-1 mantém um caractere por byte: os índices do texto são os deslocamentos do arquivo

        Assert.StartsWith("%PDF-1.4", texto, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", texto, StringComparison.Ordinal);
        var inicioDaTabela = int.Parse(Regex.Match(texto, @"startxref\n(\d+)\n%%EOF").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.Equal("xref\n", texto.Substring(inicioDaTabela, 5)); // o startxref aponta para a palavra xref
        var entradas = Regex.Matches(texto[inicioDaTabela..], @"^(\d{10}) 00000 n $", RegexOptions.Multiline);
        Assert.Equal(6, entradas.Count);
        for (var i = 0; i < entradas.Count; i++)
        {
            var posicao = int.Parse(entradas[i].Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.StartsWith($"{i + 1} 0 obj\n", texto[posicao..], StringComparison.Ordinal); // cada entrada aponta para o "N 0 obj" do objeto
        }

        var fluxo = Regex.Match(texto, @"/Length (\d+) >>\nstream\n");
        var comprimento = int.Parse(fluxo.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.StartsWith("\nendstream", texto[(fluxo.Index + fluxo.Length + comprimento)..], StringComparison.Ordinal); // o /Length é o tamanho exato do fluxo
    }
}
