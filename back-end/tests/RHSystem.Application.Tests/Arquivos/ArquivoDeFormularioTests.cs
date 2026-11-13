using System.IO.Compression;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Comum;
using RHSystem.Application.Documentos;

namespace RHSystem.Application.Tests.Arquivos;

/// <summary>
/// O arquivo de uma resposta de formulário, sem banco nem armazenamento: PDF ou Word (<c>.doc</c> pela assinatura OLE2, <c>.docx</c> pelo ZIP com <c>word/document.xml</c>) de até 5 MiB, o tipo pelo CONTEÚDO (o nome não conta); PNG, JPEG, executável, texto, ZIP sem o documento, ZIP quebrado e arquivo vazio dão 400 em
/// <c>arquivos</c>; o limite é de 5 MiB exatos.
/// </summary>
public class ArquivoDeFormularioTests
{
    private static byte[] Docx(string entrada = "word/document.xml")
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var escritor = new StreamWriter(zip.CreateEntry(entrada).Open());
            escritor.Write("<w:document/>");
        }

        return memoria.ToArray();
    }

    private static byte[] Preenchido(byte[] inicio, int tamanho)
    {
        var bytes = new byte[tamanho];
        inicio.CopyTo(bytes, 0);
        return bytes;
    }

    private static async Task<(TipoDeArquivo? Tipo, string[] Erros)> ValidarAsync(byte[] bytes, long? tamanhoDeclarado = null, string nome = "arquivo.bin")
    {
        var erros = new ColetorDeErros();
        var tipo = await ValidacaoDeArquivoDeFormulario.ValidarAsync(erros, "arquivos", new ArquivoRecebido(nome, tamanhoDeclarado ?? bytes.Length, () => new MemoryStream(bytes)), CancellationToken.None);
        try
        {
            erros.LancarSeHouver();
            return (tipo, []);
        }
        catch (ErroDeValidacaoException erro)
        {
            Assert.Equal(["arquivos"], erro.Erros.Keys);
            return (tipo, erro.Erros["arquivos"]);
        }
    }

    [Fact]
    public async Task PDF_docx_e_doc_sao_aceitos_pelo_conteudo_com_o_tipo_e_a_extensao_certos()
    {
        var pdf = await ValidarAsync("%PDF-1.7 corpo"u8.ToArray());
        var docx = await ValidarAsync(Docx(), nome: "chamado.pdf");
        var doc = await ValidarAsync(Preenchido([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], 600), nome: "chamado.docx");

        Assert.Equal((new TipoDeArquivo("application/pdf", ".pdf"), 0), (pdf.Tipo, pdf.Erros.Length));
        Assert.Equal((new TipoDeArquivo("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx"), 0), (docx.Tipo, docx.Erros.Length));
        Assert.Equal((new TipoDeArquivo("application/msword", ".doc"), 0), (doc.Tipo, doc.Erros.Length));
    }

    [Fact]
    public async Task Imagem_executavel_texto_zip_sem_o_documento_e_zip_quebrado_sao_recusados_com_a_mensagem_do_tipo()
    {
        var recusados = new Dictionary<string, byte[]>
        {
            ["png"] = Preenchido([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 100),
            ["jpeg"] = Preenchido([0xFF, 0xD8, 0xFF, 0xE0], 100),
            ["exe"] = Preenchido("MZ"u8.ToArray(), 100),
            ["texto"] = "texto simples sem assinatura"u8.ToArray(),
            ["zip sem word/document.xml"] = Docx("outro/arquivo.txt"),
            ["zip quebrado"] = Preenchido([0x50, 0x4B, 0x03, 0x04], 100),
            ["ole2 incompleto"] = [0xD0, 0xCF, 0x11, 0xE0],
        };

        foreach (var (rotulo, bytes) in recusados)
        {
            var (tipo, erros) = await ValidarAsync(bytes);

            Assert.True(tipo is null && erros.SequenceEqual([ValidacaoDeArquivoDeFormulario.MensagemDoTipo]), rotulo);
        }
    }

    [Fact]
    public async Task O_limite_e_de_5_MiB_exatos_e_arquivo_vazio_e_recusado()
    {
        var noLimite = await ValidarAsync(Preenchido("%PDF-"u8.ToArray(), 5 * 1024 * 1024));
        var acima = await ValidarAsync(Preenchido("%PDF-"u8.ToArray(), (5 * 1024 * 1024) + 1));
        var vazio = await ValidarAsync([]);

        Assert.NotNull(noLimite.Tipo);
        Assert.Equal([ValidacaoDeArquivoDeFormulario.MensagemDoTamanho], acima.Erros);
        Assert.Equal([ValidacaoDeArquivoDeFormulario.MensagemVazio], vazio.Erros);
        Assert.Equal((5L * 1024 * 1024, 5L * 1024 * 1024 * 10 + (1024 * 1024)), (ValidacaoDeArquivoDeFormulario.TamanhoMaximoBytes, ValidacaoDeArquivoDeFormulario.TetoDaRequisicaoBytes));
    }

    [Fact]
    public async Task O_tamanho_vem_do_declarado_e_o_arquivo_pequeno_de_menos_de_8_bytes_nao_quebra()
    {
        var declaradoGrande = await ValidarAsync("%PDF-"u8.ToArray(), tamanhoDeclarado: (6 * 1024 * 1024));
        var curto = await ValidarAsync([0x25]);

        Assert.Equal([ValidacaoDeArquivoDeFormulario.MensagemDoTamanho], declaradoGrande.Erros);
        Assert.Equal([ValidacaoDeArquivoDeFormulario.MensagemDoTipo], curto.Erros);
    }

    [Fact]
    public void O_Word_nao_entra_no_identificador_geral_de_arquivos()
    {
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])); // documentos, holerites e atestados continuam sem aceitar Word
        Assert.Null(IdentificadorDeTipoDeArquivo.Identificar([0x50, 0x4B, 0x03, 0x04]));
    }
}
