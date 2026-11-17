using System.Text;
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
    [InlineData("Ana (a Grande) \\ Silva")]
    [InlineData("João d'Ávila Çelik")]
    [InlineData("Ünïcode ☃ fora do alfabeto")]
    public void Nome_com_parentese_barra_ou_caractere_fora_do_alfabeto_nao_quebra_o_PDF(string nome)
    {
        var bytes = PdfsDeExemplo.Holerite(nome, "Cargo", "Setor", 1, 2026);

        using var documento = PdfDocument.Open(bytes);
        Assert.Equal(1, documento.NumberOfPages);
        Assert.Contains("Cargo", string.Join(' ', documento.GetPages().Select(p => p.Text)));
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

    [Fact]
    public void O_PDF_tem_a_estrutura_que_os_leitores_exigem_cabecalho_xref_e_fim()
    {
        var texto = Encoding.Latin1.GetString(PdfsDeExemplo.Holerite("Lucas Pereira", "Desenvolvedor", "Tecnologia", 9, 2026));

        Assert.StartsWith("%PDF-1.4", texto, StringComparison.Ordinal);
        Assert.Contains("\nxref\n", texto, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", texto, StringComparison.Ordinal);
    }
}
