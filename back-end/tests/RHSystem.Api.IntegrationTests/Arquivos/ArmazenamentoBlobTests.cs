using System.Net;
using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Infrastructure.Arquivos;

namespace RHSystem.Api.IntegrationTests.Arquivos;

/// <summary>
/// O <see cref="ArmazenamentoBlob"/> contra um Azurite de verdade (contêiner, imagem com versão fixada): gravar sem sobrescrever, link temporário por SAS (só leitura, vencimento pelo relógio, baixa como anexo com o nome
/// dado, sem a chave da conta), apagar, listar. O relógio é o do sistema (o servidor mede o vencimento do SAS pelo relógio dele): os testes o movem com <see cref="RelogioDeTeste.Avancar"/>, nunca o fixam no passado remoto.
/// </summary>
public class ArmazenamentoBlobTests(AzuriteFixture azurite) : IClassFixture<AzuriteFixture>
{
    // A chave pública e conhecida do emulador (docs da Microsoft): serve só para provar que ela não vaza no link.
    private const string ChaveDaConta = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private static readonly HttpClient Http = new();

    private static CancellationToken Ct => CancellationToken.None;

    private static string NovoContainer() => $"teste-{Guid.NewGuid():N}"[..30];

    private ArmazenamentoBlob Novo(RelogioDeTeste? relogio = null, Action<OpcoesDeArmazenamento>? ajustar = null, string? container = null)
    {
        var opcoes = new OpcoesDeArmazenamento { ConexaoBlob = azurite.Conexao, Container = container ?? NovoContainer(), SomenteHttps = false };
        ajustar?.Invoke(opcoes);
        return new ArmazenamentoBlob(opcoes, relogio ?? new RelogioDeTeste(), NullLogger<ArmazenamentoBlob>.Instance);
    }

    private static Task GravarAsync(ArmazenamentoBlob armazenamento, string caminho, byte[] bytes, string tipo = "application/pdf") =>
        armazenamento.GravarAsync(caminho, new MemoryStream(bytes), tipo, Ct);

    [Fact]
    public async Task Gravar_e_baixar_pelo_link_devolve_os_mesmos_bytes_como_anexo_com_o_nome_dado()
    {
        var armazenamento = Novo();
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");
        var bytes = ArquivosDeTeste.Pdf(4096);
        await GravarAsync(armazenamento, caminho, bytes);

        var link = await armazenamento.CriarLinkTemporarioAsync(caminho, "Relatório de férias.pdf", Ct);
        using var resposta = await Http.GetAsync(link.Url);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(bytes, await resposta.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", resposta.Content.Headers.ContentType?.MediaType);
        var disposicao = Assert.Single(resposta.Content.Headers.GetValues("Content-Disposition"));
        Assert.StartsWith("attachment;", disposicao); // sempre como anexo, nunca inline
        Assert.Contains("filename=\"Relat_rio de f_rias.pdf\"", disposicao); // a versão ASCII, para quem não lê o filename*
        Assert.Contains("filename*=UTF-8''Relat%C3%B3rio%20de%20f%C3%A9rias.pdf", disposicao);
    }

    [Fact]
    public async Task Gravar_define_o_Content_Type_que_o_chamador_deu()
    {
        var armazenamento = Novo();
        var caminho = CaminhosDeArquivo.Novo("documentos", ".png");
        await GravarAsync(armazenamento, caminho, ArquivosDeTeste.Png(), "image/png");

        using var resposta = await Http.GetAsync((await armazenamento.CriarLinkTemporarioAsync(caminho, "foto.png", Ct)).Url);

        Assert.Equal("image/png", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Link_expira_no_instante_do_relogio_mais_a_validade_e_um_link_vencido_e_recusado_pelo_servico()
    {
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");
        var container = NovoContainer();
        var antes = DateTime.UtcNow;
        var armazenamento = Novo(ajustar: o => o.LinkValidoMinutos = 7, container: container);
        await GravarAsync(armazenamento, caminho, ArquivosDeTeste.Pdf());

        var link = await armazenamento.CriarLinkTemporarioAsync(caminho, "a.pdf", Ct);

        Assert.InRange(link.ExpiraEmUtc, antes.AddMinutes(7), DateTime.UtcNow.AddMinutes(7)); // o relógio mais a validade configurada
        Assert.Equal(DateTimeKind.Utc, link.ExpiraEmUtc.Kind);
        using (var valido = await Http.GetAsync(link.Url))
        {
            Assert.Equal(HttpStatusCode.OK, valido.StatusCode);
        }

        // O mesmo arquivo, com o relógio 10 minutos no passado: o link nasce com vencimento 5 minutos atrás e o servidor o recusa.
        var atrasado = new RelogioDeTeste();
        atrasado.Avancar(TimeSpan.FromMinutes(-10));
        var vencido = await Novo(atrasado, container: container).CriarLinkTemporarioAsync(caminho, "a.pdf", Ct);
        Assert.InRange(vencido.ExpiraEmUtc, DateTime.UtcNow.AddMinutes(-6), DateTime.UtcNow.AddMinutes(-4));
        using var recusado = await Http.GetAsync(vencido.Url);
        Assert.Equal(HttpStatusCode.Forbidden, recusado.StatusCode);
    }

    [Fact]
    public async Task Link_nao_contem_a_chave_da_conta_nem_a_connection_string_e_so_da_leitura()
    {
        var armazenamento = Novo();
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");
        await GravarAsync(armazenamento, caminho, ArquivosDeTeste.Pdf());

        var link = await armazenamento.CriarLinkTemporarioAsync(caminho, "contrato.pdf", Ct);

        Assert.DoesNotContain(ChaveDaConta, link.Url);
        Assert.DoesNotContain(Uri.EscapeDataString(ChaveDaConta), link.Url);
        Assert.DoesNotContain("AccountKey", link.Url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(azurite.Conexao, link.Url);
        var consulta = HttpUtility.ParseQueryString(new Uri(link.Url).Query);
        Assert.Equal("r", consulta["sp"]); // permissão só de leitura
        Assert.False(string.IsNullOrEmpty(consulta["sig"]));
        Assert.Equal("b", consulta["sr"]); // vale para este blob, não para o contêiner

        // Só leitura: o mesmo link não deixa gravar nem apagar.
        using var gravar = new HttpRequestMessage(HttpMethod.Put, link.Url) { Content = new ByteArrayContent([1, 2, 3]) };
        gravar.Headers.Add("x-ms-blob-type", "BlockBlob");
        using var respostaDaGravacao = await Http.SendAsync(gravar);
        Assert.Equal(HttpStatusCode.Forbidden, respostaDaGravacao.StatusCode);
        using var respostaDoApagar = await Http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, link.Url));
        Assert.Equal(HttpStatusCode.Forbidden, respostaDoApagar.StatusCode);
    }

    [Fact]
    public async Task Gravar_em_caminho_ja_existente_falha_sem_sobrescrever_e_caminho_invalido_lanca_ArgumentException_antes_de_chamar_o_servico()
    {
        var armazenamento = Novo();
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");
        var primeiro = ArquivosDeTeste.Pdf(500);
        await GravarAsync(armazenamento, caminho, primeiro);

        await Assert.ThrowsAnyAsync<Exception>(() => GravarAsync(armazenamento, caminho, ArquivosDeTeste.Pdf(900)));

        using var resposta = await Http.GetAsync((await armazenamento.CriarLinkTemporarioAsync(caminho, "a.pdf", Ct)).Url);
        Assert.Equal(primeiro, await resposta.Content.ReadAsByteArrayAsync()); // o primeiro continua lá, intacto

        // Um serviço que não existe: se o caminho inválido chegasse ao servidor, o erro seria de conexão, não ArgumentException.
        var semServico = new ArmazenamentoBlob(
            new OpcoesDeArmazenamento { ConexaoBlob = "DefaultEndpointsProtocol=http;AccountName=conta;AccountKey=AAAA;BlobEndpoint=http://127.0.0.1:1/conta;", Container = "arquivos", SomenteHttps = false },
            new RelogioDeTeste(),
            NullLogger<ArmazenamentoBlob>.Instance);
        foreach (var invalido in new[] { "../documentos/0123456789abcdef0123456789abcdef.pdf", "/documentos/0123456789abcdef0123456789abcdef.pdf", "Documentos/0123456789abcdef0123456789abcdef.pdf", "documentos/contrato.pdf", string.Empty })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => semServico.GravarAsync(invalido, new MemoryStream([1]), "application/pdf", Ct));
            await Assert.ThrowsAsync<ArgumentException>(() => semServico.CriarLinkTemporarioAsync(invalido, "a.pdf", Ct));
            await Assert.ThrowsAsync<ArgumentException>(() => semServico.ApagarAsync(invalido, Ct));
        }
    }

    [Fact]
    public async Task Apagar_e_idempotente()
    {
        var armazenamento = Novo();
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");
        await GravarAsync(armazenamento, caminho, ArquivosDeTeste.Pdf());
        var link = await armazenamento.CriarLinkTemporarioAsync(caminho, "a.pdf", Ct);

        await armazenamento.ApagarAsync(caminho, Ct);
        await armazenamento.ApagarAsync(caminho, Ct); // já apagado: não lança
        await armazenamento.ApagarAsync(CaminhosDeArquivo.Novo("documentos", ".pdf"), Ct); // nunca existiu: não lança

        using var resposta = await Http.GetAsync(link.Url);
        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Empty(await ListarAsync(armazenamento, "documentos/"));
    }

    [Fact]
    public async Task Nome_com_aspas_e_quebra_de_linha_nao_injeta_cabecalho_nem_quebra_o_link()
    {
        var armazenamento = Novo();
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");
        await GravarAsync(armazenamento, caminho, ArquivosDeTeste.Pdf());

        var link = await armazenamento.CriarLinkTemporarioAsync(caminho, "a\"b\r\nX-Injetado: 1\r\nSet-Cookie: x=1\r\n.pdf", Ct);

        Assert.True(Uri.TryCreate(link.Url, UriKind.Absolute, out var uri), "o link precisa continuar sendo uma URL");
        var rscd = HttpUtility.ParseQueryString(uri.Query)["rscd"] ?? string.Empty;
        Assert.DoesNotContain('\r', rscd);
        Assert.DoesNotContain('\n', rscd);
        using var resposta = await Http.GetAsync(link.Url);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False(resposta.Headers.Contains("X-Injetado")); // nenhum cabeçalho novo
        Assert.False(resposta.Headers.Contains("Set-Cookie"));
        Assert.False(resposta.Content.Headers.Contains("X-Injetado"));
        var disposicao = Assert.Single(resposta.Content.Headers.GetValues("Content-Disposition"));
        Assert.DoesNotContain('\r', disposicao);
        Assert.DoesNotContain('\n', disposicao);
        Assert.Contains("filename=\"abX-Injetado: 1Set-Cookie: x=1.pdf\"", disposicao); // as aspas e as quebras saíram; o resto é só texto dentro das aspas
        Assert.Equal(1, disposicao.Count(c => c == '"') / 2); // um só par de aspas: as do filename
    }

    [Fact]
    public async Task ListarAsync_devolve_caminho_tamanho_e_data_de_criacao_dos_blobs_do_prefixo()
    {
        var armazenamento = Novo();
        var pequeno = CaminhosDeArquivo.Novo("documentos", ".pdf");
        var grande = CaminhosDeArquivo.Novo("documentos", ".png");
        var deOutraPasta = CaminhosDeArquivo.Novo("holerites", ".pdf");
        var antes = DateTime.UtcNow.AddMinutes(-2);
        await GravarAsync(armazenamento, pequeno, ArquivosDeTeste.Pdf(100));
        await GravarAsync(armazenamento, grande, ArquivosDeTeste.Png(2000), "image/png");
        await GravarAsync(armazenamento, deOutraPasta, ArquivosDeTeste.Pdf(300));

        var lista = (await ListarAsync(armazenamento, "documentos/")).OrderBy(a => a.Tamanho).ToList();

        Assert.Equal([(pequeno, 100L), (grande, 2000L)], lista.Select(a => (a.Caminho, a.Tamanho)));
        Assert.All(lista, a =>
        {
            Assert.Equal(DateTimeKind.Utc, a.CriadoEmUtc.Kind);
            Assert.InRange(a.CriadoEmUtc, antes, DateTime.UtcNow.AddMinutes(2));
        });
        Assert.Equal([deOutraPasta], (await ListarAsync(armazenamento, "holerites/")).Select(a => a.Caminho));
        Assert.Empty(await ListarAsync(armazenamento, "atestados/"));
    }

    [Fact]
    public async Task Sem_ConexaoBlob_o_servico_existe_e_a_primeira_operacao_falha_com_erro_claro_sem_a_connection_string()
    {
        var armazenamento = new ArmazenamentoBlob(new OpcoesDeArmazenamento { ConexaoBlob = string.Empty }, new RelogioDeTeste(), NullLogger<ArmazenamentoBlob>.Instance);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => GravarAsync(armazenamento, CaminhosDeArquivo.Novo("documentos", ".pdf"), ArquivosDeTeste.Pdf()));

        Assert.Contains("Armazenamento:ConexaoBlob", erro.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => armazenamento.CriarLinkTemporarioAsync(CaminhosDeArquivo.Novo("documentos", ".pdf"), "a.pdf", Ct));
    }

    [Fact]
    public void Opcoes_com_valor_invalido_falham_na_validacao_com_mensagem_clara()
    {
        Assert.Throws<InvalidOperationException>(() => new OpcoesDeArmazenamento { LinkValidoMinutos = 0 }.Validar());
        Assert.Throws<InvalidOperationException>(() => new OpcoesDeArmazenamento { SobrasMinimoHoras = 0 }.Validar());
        Assert.Throws<InvalidOperationException>(() => new OpcoesDeArmazenamento { Container = "Nome Invalido" }.Validar());
        new OpcoesDeArmazenamento().Validar(); // os padrões valem (e a connection string vazia não é erro: o app sobe)
        Assert.Equal(("arquivos", 5, true, 24), (new OpcoesDeArmazenamento().Container, new OpcoesDeArmazenamento().LinkValidoMinutos, new OpcoesDeArmazenamento().SomenteHttps, new OpcoesDeArmazenamento().SobrasMinimoHoras));
    }

    private static async Task<List<ArquivoGuardado>> ListarAsync(IListagemDeArquivos listagem, string prefixo)
    {
        var lista = new List<ArquivoGuardado>();
        await foreach (var arquivo in listagem.ListarAsync(prefixo, Ct))
        {
            lista.Add(arquivo);
        }

        return lista;
    }
}
