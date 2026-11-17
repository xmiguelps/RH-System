using RHSystem.Application.Arquivos;
using RHSystem.Application.Comum;
using RHSystem.Application.Documentos;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Tests.Treinamentos;

/// <summary>O vídeo de um módulo de treinamento (decisão do dono, 2026-11-17): o tipo pelo conteúdo (MP4 e WebM), o limite de 100 MiB e a regra do módulo de vídeo (um endereço OU um arquivo).</summary>
public class VideoDoModuloTests
{
    private static byte[] Mp4(string marca = "isom") => [0x00, 0x00, 0x00, 0x18, .. "ftyp"u8.ToArray(), .. System.Text.Encoding.ASCII.GetBytes(marca), 0x00, 0x00, 0x02, 0x00];

    private static byte[] Ebml(string docType)
    {
        var tipo = System.Text.Encoding.ASCII.GetBytes(docType);
        return [0x1A, 0x45, 0xDF, 0xA3, 0xA3, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81, 0x01, 0x42, 0xF2, 0x81, 0x04, 0x42, 0xF3, 0x81, 0x08, 0x42, 0x82, (byte)(0x80 | tipo.Length), .. tipo, 0x42, 0x87, 0x81, 0x04];
    }

    // ---------------------------------------------------------------- o tipo pelo conteúdo

    [Theory]
    [InlineData("isom")]
    [InlineData("mp42")]
    [InlineData("M4V ")]
    [InlineData("qt  ")]
    public void Ftyp_nos_bytes_4_a_7_e_MP4_de_qualquer_marca(string marca)
    {
        var tipo = IdentificadorDeVideo.Identificar(Mp4(marca));

        Assert.Equal(new TipoDeArquivo("video/mp4", ".mp4"), tipo);
    }

    [Fact]
    public void WebM_e_a_assinatura_EBML_com_o_DocType_webm()
    {
        Assert.Equal(new TipoDeArquivo("video/webm", ".webm"), IdentificadorDeVideo.Identificar(Ebml("webm")));
    }

    [Fact]
    public void Matroska_tem_a_mesma_assinatura_EBML_mas_o_DocType_matroska_e_fica_de_fora()
    {
        Assert.Null(IdentificadorDeVideo.Identificar(Ebml("matroska")));
    }

    [Fact]
    public void A_assinatura_EBML_sem_DocType_nos_primeiros_64_bytes_nao_vale()
    {
        byte[] semDocType = [0x1A, 0x45, 0xDF, 0xA3, .. new byte[100]];

        Assert.Null(IdentificadorDeVideo.Identificar(semDocType));
    }

    [Fact]
    public void O_DocType_webm_depois_do_byte_64_nao_vale()
    {
        byte[] tarde = [0x1A, 0x45, 0xDF, 0xA3, .. new byte[70], .. "webm"u8.ToArray()];

        Assert.Null(IdentificadorDeVideo.Identificar(tarde));
    }

    [Fact]
    public void O_DocType_webm_que_termina_exatamente_no_byte_64_vale()
    {
        var corpo = new byte[64];
        corpo[0] = 0x1A;
        corpo[1] = 0x45;
        corpo[2] = 0xDF;
        corpo[3] = 0xA3;
        "webm"u8.CopyTo(corpo.AsSpan(60));

        Assert.Equal(new TipoDeArquivo("video/webm", ".webm"), IdentificadorDeVideo.Identificar(corpo));
    }

    [Fact]
    public void Ftyp_fora_dos_bytes_4_a_7_nao_vale()
    {
        Assert.Null(IdentificadorDeVideo.Identificar([.. "ftypisom"u8.ToArray(), 0, 0, 0, 0]));
        Assert.Null(IdentificadorDeVideo.Identificar([0, 0, 0, 0, 0, .. "ftyp"u8.ToArray(), 0, 0, 0]));
    }

    [Fact]
    public void Pdf_jpeg_exe_texto_e_vazio_nao_sao_video_e_poucos_bytes_nao_estouram()
    {
        Assert.Null(IdentificadorDeVideo.Identificar("%PDF-1.7\n"u8));
        Assert.Null(IdentificadorDeVideo.Identificar([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46]));
        Assert.Null(IdentificadorDeVideo.Identificar("MZ\0\0\0\0\0\0\0\0\0\0"u8));
        Assert.Null(IdentificadorDeVideo.Identificar("texto simples sem assinatura"u8));
        Assert.Null(IdentificadorDeVideo.Identificar([]));
        Assert.Null(IdentificadorDeVideo.Identificar([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79])); // ftyp cortado
        Assert.Null(IdentificadorDeVideo.Identificar([0x1A, 0x45, 0xDF]));
    }

    // ---------------------------------------------------------------- o arquivo recebido

    private static ArquivoRecebido Recebido(byte[] bytes, long? tamanho = null, Action? aoAbrir = null) =>
        new("aula.mp4", tamanho ?? bytes.Length, () =>
        {
            aoAbrir?.Invoke();
            return new MemoryStream(bytes);
        });

    private static async Task<(TipoDeArquivo? Tipo, Dictionary<string, string[]> Erros)> ValidarAsync(ArquivoRecebido arquivo, int recebidos = 1)
    {
        var erros = new ColetorDeErros();
        var tipo = await ValidacaoDoVideoDoModulo.ValidarAsync(erros, arquivo, recebidos, CancellationToken.None);
        try
        {
            erros.LancarSeHouver();
            return (tipo, []);
        }
        catch (ErroDeValidacaoException excecao)
        {
            return (tipo, excecao.Erros.ToDictionary(p => p.Key, p => p.Value));
        }
    }

    [Fact]
    public async Task Um_mp4_e_um_webm_validos_devolvem_o_tipo_do_conteudo_e_nenhum_erro()
    {
        var (mp4, errosDoMp4) = await ValidarAsync(Recebido(Mp4()));
        var (webm, errosDoWebm) = await ValidarAsync(Recebido(Ebml("webm")));

        Assert.Equal(("video/mp4", ".mp4", "video/webm", ".webm"), (mp4!.TipoDeConteudo, mp4.Extensao, webm!.TipoDeConteudo, webm.Extensao));
        Assert.Empty(errosDoMp4);
        Assert.Empty(errosDoWebm);
    }

    [Fact]
    public async Task O_limite_e_100_MiB_exatos_vale_e_um_byte_a_mais_e_recusado_sem_abrir_o_arquivo()
    {
        var abertos = 0;
        var noLimite = await ValidarAsync(Recebido(Mp4(), tamanho: 100L * 1024 * 1024, aoAbrir: () => abertos++));
        var passou = await ValidarAsync(Recebido(Mp4(), tamanho: (100L * 1024 * 1024) + 1, aoAbrir: () => abertos++));

        Assert.NotNull(noLimite.Tipo);
        Assert.Empty(noLimite.Erros);
        Assert.Null(passou.Tipo);
        Assert.Equal(["O vídeo pode ter até 100 MB."], passou.Erros["arquivo"]);
        Assert.Equal(1, abertos); // só o que está dentro do limite foi aberto
        Assert.Equal(104857600L, ValidacaoDoVideoDoModulo.TamanhoMaximoBytes);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task Arquivo_vazio_e_recusado_sem_abrir(long tamanho)
    {
        var abertos = 0;

        var (tipo, erros) = await ValidarAsync(Recebido([], tamanho, () => abertos++));

        Assert.Null(tipo);
        Assert.Equal(["O arquivo está vazio."], erros["arquivo"]);
        Assert.Equal(0, abertos);
    }

    [Fact]
    public async Task Mais_de_um_arquivo_no_formulario_e_recusado()
    {
        var (tipo, erros) = await ValidarAsync(Recebido(Mp4()), recebidos: 2);

        Assert.Null(tipo);
        Assert.Equal(["Envie um arquivo só."], erros["arquivo"]);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("exe")]
    [InlineData("texto")]
    [InlineData("matroska")]
    public async Task O_que_nao_e_mp4_nem_webm_diz_para_enviar_um_video_MP4_ou_WebM(string o_que)
    {
        var bytes = o_que switch
        {
            "pdf" => "%PDF-1.7\nconteúdo"u8.ToArray(),
            "exe" => "MZ\0\0\0\0\0\0\0\0\0\0\0\0"u8.ToArray(),
            "texto" => "texto simples sem assinatura"u8.ToArray(),
            _ => Ebml("matroska"),
        };

        var (tipo, erros) = await ValidarAsync(Recebido(bytes));

        Assert.Null(tipo);
        Assert.Equal(["Envie um vídeo MP4 ou WebM."], erros["arquivo"]);
    }

    // ---------------------------------------------------------------- a regra do módulo de vídeo

    private static Dictionary<string, string[]> ErrosDoModulo(string? url, bool temArquivoNovo, bool jaTemArquivo, TipoDeModulo tipo = TipoDeModulo.Video)
    {
        var erros = new ColetorDeErros();
        _ = ValidacaoDoModulo.Validar(erros, "Aula", tipo, 10, url, null, temArquivoNovo, jaTemArquivo);
        try
        {
            erros.LancarSeHouver();
            return [];
        }
        catch (ErroDeValidacaoException excecao)
        {
            return excecao.Erros.ToDictionary(p => p.Key, p => p.Value);
        }
    }

    [Fact]
    public void Video_aceita_um_endereco_ou_um_arquivo_e_nunca_os_dois_nem_nenhum()
    {
        Assert.Empty(ErrosDoModulo("https://videos.exemplo.com.br/aula", temArquivoNovo: false, jaTemArquivo: false));
        Assert.Empty(ErrosDoModulo(null, temArquivoNovo: true, jaTemArquivo: false));
        Assert.Equal(["Escolha um só: o link ou o arquivo de vídeo."], ErrosDoModulo("https://videos.exemplo.com.br/aula", temArquivoNovo: true, jaTemArquivo: false)["arquivo"]);
        Assert.Equal(["Informe o endereço do vídeo ou envie o arquivo."], ErrosDoModulo(null, temArquivoNovo: false, jaTemArquivo: false)["urlExterna"]);
    }

    [Fact]
    public void Na_edicao_o_video_que_ja_tem_arquivo_fica_sem_nada_novo_e_vira_link_com_um_endereco()
    {
        Assert.Empty(ErrosDoModulo(null, temArquivoNovo: false, jaTemArquivo: true)); // o arquivo atual fica
        Assert.Empty(ErrosDoModulo("https://videos.exemplo.com.br/aula", temArquivoNovo: false, jaTemArquivo: true)); // virou link: o arquivo velho sai
        Assert.Empty(ErrosDoModulo(null, temArquivoNovo: true, jaTemArquivo: true)); // troca o arquivo
        Assert.Contains("arquivo", ErrosDoModulo("https://videos.exemplo.com.br/aula", temArquivoNovo: true, jaTemArquivo: true).Keys); // endereço e arquivo novo juntos
    }

    [Theory]
    [InlineData(TipoDeModulo.Texto)]
    [InlineData(TipoDeModulo.Link)]
    public void Texto_e_link_continuam_sem_arquivo(TipoDeModulo tipo)
    {
        var erros = new ColetorDeErros();
        _ = ValidacaoDoModulo.Validar(erros, "Aula", tipo, null, tipo == TipoDeModulo.Link ? "https://a.com" : null, tipo == TipoDeModulo.Texto ? "Texto." : null, true, false);

        var problema = Assert.Throws<ErroDeValidacaoException>(erros.LancarSeHouver);
        Assert.Equal(["Este tipo de módulo não tem arquivo."], problema.Erros["arquivo"]);
    }
}
