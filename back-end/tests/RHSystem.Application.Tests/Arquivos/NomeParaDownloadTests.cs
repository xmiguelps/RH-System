using RHSystem.Application.Arquivos;

namespace RHSystem.Application.Tests.Arquivos;

public class NomeParaDownloadTests
{
    [Fact]
    public void NomeParaDownload_Limpar_tira_diretorio_aspas_barras_controle_e_quebra_de_linha_corta_em_100_e_cai_em_documento_com_extensao_se_ficar_vazio()
    {
        // Diretório (de qualquer sistema) sai: fica só o último pedaço.
        Assert.Equal("passwd", NomeParaDownload.Limpar("../../etc/passwd", ".pdf"));
        Assert.Equal("contrato.pdf", NomeParaDownload.Limpar(@"C:\Users\ana\Meus Documentos\contrato.pdf", ".pdf"));
        Assert.Equal("contrato.pdf", NomeParaDownload.Limpar("pasta/sub\\contrato.pdf", ".pdf"));

        // Aspas, controle e quebra de linha saem (o nome vai para um cabeçalho): nenhum sobra.
        Assert.Equal("ab.pdf", NomeParaDownload.Limpar("a\"b.pdf", ".pdf"));
        Assert.Equal("aXInjetado1.pdf", NomeParaDownload.Limpar("a\r\nX\u0000Injetado\t1.pdf", ".pdf"));
        Assert.DoesNotContain('\r', NomeParaDownload.Limpar("a\r\nb.pdf", ".pdf"));
        Assert.DoesNotContain('\n', NomeParaDownload.Limpar("a\r\nb.pdf", ".pdf"));
        Assert.DoesNotContain('"', NomeParaDownload.Limpar("\"\"\"x\"\"\".pdf", ".pdf"));
        Assert.Equal("x.pdf", NomeParaDownload.Limpar("\"\"\"x\"\"\".pdf", ".pdf"));

        // Acentos e espaços ficam (o cabeçalho leva a versão em UTF-8): é o nome que a pessoa reconhece.
        Assert.Equal("Relatório de férias 2026.pdf", NomeParaDownload.Limpar("  Relatório de férias 2026.pdf  ", ".pdf"));

        // Até 100 caracteres.
        var longo = new string('a', 150) + ".pdf";
        Assert.Equal(100, NomeParaDownload.Limpar(longo, ".pdf").Length);
        Assert.Equal(new string('b', 100), NomeParaDownload.Limpar(new string('b', 100), ".pdf")); // 100 é o limite
        Assert.Equal(100, NomeParaDownload.Limpar(new string('c', 101), ".pdf").Length);

        // Vazio (ou só lixo) vira "documento" + a extensão.
        Assert.Equal("documento.pdf", NomeParaDownload.Limpar(null, ".pdf"));
        Assert.Equal("documento.jpg", NomeParaDownload.Limpar("", ".jpg"));
        Assert.Equal("documento.pdf", NomeParaDownload.Limpar("   ", ".pdf"));
        Assert.Equal("documento.pdf", NomeParaDownload.Limpar("\"\"\r\n", ".pdf"));
        Assert.Equal("documento.png", NomeParaDownload.Limpar("pasta/", ".png")); // só diretório
        Assert.Equal("documento.pdf", NomeParaDownload.Limpar("..", ".pdf"));
    }

    [Fact]
    public void Limpar_e_idempotente_o_nome_ja_limpo_nao_muda()
    {
        foreach (var nome in new[] { "contrato.pdf", "Relatório de férias.pdf", new string('x', 100), "documento.pdf" })
        {
            Assert.Equal(nome, NomeParaDownload.Limpar(NomeParaDownload.Limpar(nome, ".pdf"), ".pdf"));
        }
    }
}
