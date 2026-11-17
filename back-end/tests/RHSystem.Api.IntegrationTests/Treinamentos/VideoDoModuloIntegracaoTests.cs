using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// O vídeo por arquivo nos módulos de treinamento (decisão do dono, 2026-11-17), no banco REAL: o módulo <c>VIDEO</c> aceita um endereço OU um arquivo MP4/WebM (pelo CONTEÚDO, até 100 MiB), o blob sai com o tipo de vídeo e o caminho nunca aparece; na edição o arquivo fica, é trocado ou vira link, e
/// o que deixa de valer é apagado DEPOIS do commit; a pessoa abre o módulo (<c>temArquivo</c>) e pega o link temporário em <c>baixarArquivoDeModulo</c>. O teto da requisição das duas rotas de módulo é o do vídeo.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class VideoDoModuloIntegracaoTests(MySqlFixture mysql)
{
    private static string Rota(int treinamento) => $"/api/rh/treinamentos/{treinamento}/modulos";

    private static async Task<(int Id, HttpClient Cliente)> PessoaAsync(ApiDeAcesso api, string nome)
    {
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));
        return (id, await api.ClienteAgoraAsync(await AdvertenciasDeTeste.ComLoginAsync(api, id)));
    }

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO) VALUES ({treinamento}, {pessoa}, '{status}', NULL)");

    private static Task<int> EmAndamentoAsync(ApiDeAcesso api, string titulo) => SemearAsync(api, titulo, "EM_ANDAMENTO", true, "TODOS");

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(VideoDoModuloIntegracaoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static byte[] Mp4(int tamanho = 4096) => Preenchido([0x00, 0x00, 0x00, 0x18, .. "ftypmp42"u8.ToArray()], tamanho);

    private static byte[] Webm(int tamanho = 4096) => Preenchido([0x1A, 0x45, 0xDF, 0xA3, 0xA3, 0x42, 0x86, 0x81, 0x01, 0x42, 0x82, 0x84, .. "webm"u8.ToArray()], tamanho);

    private static byte[] Preenchido(byte[] inicio, int tamanho)
    {
        var bytes = new byte[tamanho];
        inicio.CopyTo(bytes, 0);
        for (var i = inicio.Length; i < tamanho; i++)
        {
            bytes[i] = (byte)(0x20 + (i % 90));
        }

        return bytes;
    }

    private static MultipartFormDataContent Modulo(string? titulo, string tipo = "VIDEO", string? url = null, (string Nome, byte[] Bytes)? arquivo = null, (string Nome, byte[] Bytes)? segundo = null, string? texto = null)
    {
        var corpo = new MultipartFormDataContent();
        void Campo(string nome, string? valor)
        {
            if (valor is not null)
            {
                corpo.Add(new StringContent(valor, Encoding.UTF8), nome);
            }
        }

        Campo("titulo", titulo);
        Campo("tipo", tipo);
        Campo("urlExterna", url);
        Campo("conteudoTexto", texto);
        if (arquivo is { } a)
        {
            corpo.Add(new ByteArrayContent(a.Bytes), "arquivo", a.Nome);
        }

        if (segundo is { } s)
        {
            corpo.Add(new ByteArrayContent(s.Bytes), "arquivo", s.Nome);
        }

        return corpo;
    }

    private static async Task<HttpResponseMessage> AdicionarAsync(ApiDeAcesso api, int treinamento, HttpContent corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsync(Rota(treinamento), corpo);

    private static async Task<HttpResponseMessage> AtualizarAsync(ApiDeAcesso api, int treinamento, int modulo, HttpContent corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsync($"{Rota(treinamento)}/{modulo}", corpo);

    private static async Task<JsonElement> ErrosAsync(HttpResponseMessage resposta) => (await CorpoAsync(resposta)).GetProperty("errors");

    private static async Task<(int Id, string Caminho)> VideoDeArquivoAsync(ApiDeAcesso api, int treinamento, byte[]? conteudo = null, string nome = "Aula 1.mp4")
    {
        var resposta = await AdicionarAsync(api, treinamento, Modulo("Aula", arquivo: (nome, conteudo ?? Mp4())));
        Assert.Equal(201, (int)resposta.StatusCode);
        var id = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        return (id, (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {id}"))!);
    }

    // ---------------------------------------------------------------- acrescentar

    [Fact]
    public async Task Video_por_arquivo_mp4_e_webm_grava_o_blob_com_o_tipo_de_video_e_devolve_so_o_nome()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid1"), "RASCUNHO");
        var mp4 = Mp4(8192);
        var webm = Webm(8192);

        var a = await AdicionarAsync(api, id, Modulo("Abertura", arquivo: ("../../Minha aula.mp4", mp4)));
        var b = await AdicionarAsync(api, id, Modulo("Segunda aula", arquivo: ("aula-2.webm", webm)));

        Assert.Equal([201, 201], new[] { a, b }.Select(r => (int)r.StatusCode));
        await a.AfirmarConformeAoContratoAsync("adicionarModuloDeTreinamento");
        var corpo = await CorpoAsync(a);
        Assert.Equal(("VIDEO", "Minha aula.mp4", JsonValueKind.Null, JsonValueKind.Null), (corpo.GetProperty("tipo").GetString(), corpo.GetProperty("nomeArquivo").GetString(), corpo.GetProperty("urlExterna").ValueKind, corpo.GetProperty("conteudoTexto").ValueKind));
        var caminhoMp4 = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id} AND ORDEM = 1"))!;
        var caminhoWebm = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id} AND ORDEM = 2"))!;
        Assert.StartsWith("treinamentos/", caminhoMp4, StringComparison.Ordinal);
        Assert.EndsWith(".mp4", caminhoMp4, StringComparison.Ordinal);
        Assert.EndsWith(".webm", caminhoWebm, StringComparison.Ordinal);
        Assert.Equal(mp4, api.Api.Armazenamento.Conteudo(caminhoMp4));
        Assert.Equal(webm, api.Api.Armazenamento.Conteudo(caminhoWebm));
        Assert.Equal(("video/mp4", "video/webm"), (api.Api.Armazenamento.TipoDeConteudo(caminhoMp4), api.Api.Armazenamento.TipoDeConteudo(caminhoWebm)));
        Assert.Equal((null, "Minha aula.mp4"), (await TextoAsync(api, $"SELECT URL_EXTERNA FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id} AND ORDEM = 1"), await TextoAsync(api, $"SELECT NOME_ARQUIVO_ORIGINAL FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id} AND ORDEM = 1")));
        Assert.DoesNotContain("treinamentos/", await a.Content.ReadAsStringAsync(), StringComparison.Ordinal); // o caminho nunca sai
    }

    [Fact]
    public async Task Nome_sem_extensao_e_o_nome_limpo_e_guardado_como_veio_e_a_extensao_do_blob_e_a_do_conteudo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid2"), "RASCUNHO");

        var resposta = await AdicionarAsync(api, id, Modulo("Sem extensão", arquivo: ("gravacao-da-reuniao", Webm())));

        Assert.Equal(201, (int)resposta.StatusCode);
        var caminho = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id}"))!;
        Assert.EndsWith(".webm", caminho, StringComparison.Ordinal);
        Assert.Equal("gravacao-da-reuniao", (await CorpoAsync(resposta)).GetProperty("nomeArquivo").GetString());
    }

    [Fact]
    public async Task O_link_externo_do_video_continua_igual_e_nao_guarda_arquivo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid3"), "RASCUNHO");
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;

        var resposta = await AdicionarAsync(api, id, Modulo("Por link", url: "https://videos.exemplo.com.br/aula"));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("https://videos.exemplo.com.br/aula", JsonValueKind.Null), (corpo.GetProperty("urlExterna").GetString(), corpo.GetProperty("nomeArquivo").ValueKind));
        Assert.Equal(gravadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    [Fact]
    public async Task Os_erros_do_video_vem_no_campo_certo_e_nada_e_gravado_nem_guardado()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid4"), "RASCUNHO");
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        (string Nome, MultipartFormDataContent Corpo, string Campo, string Mensagem)[] casos =
        [
            ("endereço e arquivo juntos", Modulo("V", url: "https://a.com", arquivo: ("a.mp4", Mp4())), "arquivo", "Escolha um só: o link ou o arquivo de vídeo."),
            ("nem endereço nem arquivo", Modulo("V"), "urlExterna", "Informe o endereço do vídeo ou envie o arquivo."),
            ("pdf com nome de mp4", Modulo("V", arquivo: ("a.mp4", ArquivosDeTeste.Pdf())), "arquivo", "Envie um vídeo MP4 ou WebM."),
            ("exe com nome de mp4", Modulo("V", arquivo: ("a.mp4", ArquivosDeTeste.Exe())), "arquivo", "Envie um vídeo MP4 ou WebM."),
            ("matroska", Modulo("V", arquivo: ("a.webm", [0x1A, 0x45, 0xDF, 0xA3, 0xA3, 0x42, 0x82, 0x88, .. "matroska"u8.ToArray(), .. new byte[32]])), "arquivo", "Envie um vídeo MP4 ou WebM."),
            ("arquivo vazio", Modulo("V", arquivo: ("a.mp4", [])), "arquivo", "O arquivo está vazio."),
            ("dois arquivos", Modulo("V", arquivo: ("a.mp4", Mp4()), segundo: ("b.mp4", Mp4())), "arquivo", "Envie um arquivo só."),
            ("texto com vídeo", Modulo("V", "TEXTO", texto: "a", arquivo: ("a.mp4", Mp4())), "arquivo", "Este tipo de módulo não tem arquivo."),
            ("link com vídeo", Modulo("V", "LINK", url: "https://a.com", arquivo: ("a.mp4", Mp4())), "arquivo", "Este tipo de módulo não tem arquivo."),
            ("pdf com vídeo", Modulo("V", "PDF", arquivo: ("a.pdf", Mp4())), "arquivo", "Envie um arquivo PDF."),
        ];

        var falhas = new List<string>();
        foreach (var (nome, corpo, campo, mensagem) in casos)
        {
            var resposta = await AdicionarAsync(api, id, corpo);
            var texto = (int)resposta.StatusCode == 400 && (await ErrosAsync(resposta)).TryGetProperty(campo, out var lista) ? lista[0].GetString() : $"HTTP {(int)resposta.StatusCode}";
            if (texto != mensagem)
            {
                falhas.Add($"{nome}: {texto}");
            }
        }

        Assert.Empty(falhas);
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id}"));
        Assert.Equal(gravadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    [Fact]
    public async Task Publicado_trava_a_estrutura_e_nao_deixa_acrescentar_video_por_arquivo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid5"), "EM_ANDAMENTO");
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;

        var resposta = await AdicionarAsync(api, id, Modulo("Tarde demais", arquivo: ("a.mp4", Mp4())));

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("TREINAMENTO_MODULOS_TRAVADOS", Codigo(await CorpoAsync(resposta)));
        Assert.Equal(gravadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    // ---------------------------------------------------------------- atualizar

    [Fact]
    public async Task Trocar_o_arquivo_do_video_grava_o_novo_e_so_apaga_o_antigo_depois_do_commit()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid7"), "RASCUNHO");
        var (modulo, antigo) = await VideoDeArquivoAsync(api, id);
        var novo = Webm(9000);

        var resposta = await AtualizarAsync(api, id, modulo, Modulo("Aula nova", arquivo: ("nova.webm", novo)));

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("atualizarModuloDeTreinamento");
        var caminho = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"))!;
        Assert.NotEqual(antigo, caminho);
        Assert.EndsWith(".webm", caminho, StringComparison.Ordinal);
        Assert.Equal(novo, api.Api.Armazenamento.Conteudo(caminho));
        Assert.Equal(("video/webm", false, "nova.webm"), (api.Api.Armazenamento.TipoDeConteudo(caminho), api.Api.Armazenamento.Existe(antigo), (await CorpoAsync(resposta)).GetProperty("nomeArquivo").GetString()));
        Assert.Contains(antigo, api.Api.Armazenamento.Apagados);
    }

    [Fact]
    public async Task Mudar_so_o_titulo_mantem_o_arquivo_do_video_e_nao_apaga_nada()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid8"), "RASCUNHO");
        var (modulo, caminho) = await VideoDeArquivoAsync(api, id, nome: "Aula original.mp4");
        var apagadosAntes = api.Api.Armazenamento.Apagados.Count;

        var resposta = await AtualizarAsync(api, id, modulo, Modulo("Título corrigido"));

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("Título corrigido", "Aula original.mp4"), (corpo.GetProperty("titulo").GetString(), corpo.GetProperty("nomeArquivo").GetString()));
        Assert.Equal((caminho, true, apagadosAntes), (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"), api.Api.Armazenamento.Existe(caminho), api.Api.Armazenamento.Apagados.Count));
    }

    [Fact]
    public async Task Video_de_arquivo_que_vira_link_guarda_o_endereco_e_apaga_o_arquivo_depois_do_commit()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid9"), "RASCUNHO");
        var (modulo, caminho) = await VideoDeArquivoAsync(api, id);

        var resposta = await AtualizarAsync(api, id, modulo, Modulo("Agora por link", url: "https://videos.exemplo.com.br/aula"));

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("https://videos.exemplo.com.br/aula", JsonValueKind.Null), (corpo.GetProperty("urlExterna").GetString(), corpo.GetProperty("nomeArquivo").ValueKind));
        Assert.Equal((null, null, false), (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"), await TextoAsync(api, $"SELECT NOME_ARQUIVO_ORIGINAL FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"), api.Api.Armazenamento.Existe(caminho)));
    }

    [Fact]
    public async Task Video_de_link_que_vira_arquivo_guarda_o_blob_e_apaga_o_endereco()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid10"), "RASCUNHO");
        var modulo = await ModuloAsync(api, id, 1, "VIDEO");

        var resposta = await AtualizarAsync(api, id, modulo, Modulo("Agora por arquivo", arquivo: ("aula.mp4", Mp4())));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((null, "aula.mp4"), (await TextoAsync(api, $"SELECT URL_EXTERNA FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"), await TextoAsync(api, $"SELECT NOME_ARQUIVO_ORIGINAL FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}")));
        Assert.True(api.Api.Armazenamento.Existe((await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"))!));
    }

    [Fact]
    public async Task Pdf_nao_vira_video_sem_conteudo_novo_e_video_de_arquivo_nao_vira_pdf_sem_arquivo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid11"), "RASCUNHO");
        var pdf = await ModuloAsync(api, id, 1, "PDF");
        var (video, _) = await VideoDeArquivoAsync(api, id);

        var pdfParaVideo = await AtualizarAsync(api, id, pdf, Modulo("Virou vídeo", "VIDEO"));
        var videoParaPdf = await AtualizarAsync(api, id, video, Modulo("Virou PDF", "PDF"));

        Assert.Equal((400, "Informe o endereço do vídeo ou envie o arquivo."), ((int)pdfParaVideo.StatusCode, (await ErrosAsync(pdfParaVideo)).GetProperty("urlExterna")[0].GetString()));
        Assert.Equal((400, "Envie o arquivo PDF do módulo."), ((int)videoParaPdf.StatusCode, (await ErrosAsync(videoParaPdf)).GetProperty("arquivo")[0].GetString()));
        Assert.Equal(("PDF", "VIDEO"), (await TextoAsync(api, $"SELECT TIPO_MODULO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {pdf}"), await TextoAsync(api, $"SELECT TIPO_MODULO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {video}")));
    }

    [Fact]
    public async Task Video_de_arquivo_que_vira_texto_apaga_o_arquivo_depois_do_commit()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid12"), "RASCUNHO");
        var (modulo, caminho) = await VideoDeArquivoAsync(api, id);

        var resposta = await AtualizarAsync(api, id, modulo, Modulo("Agora é texto", "TEXTO", texto: "Resumo da aula."));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((null, false), (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"), api.Api.Armazenamento.Existe(caminho)));
    }

    [Fact]
    public async Task No_treinamento_publicado_o_arquivo_do_video_se_corrige_mas_o_tipo_nao_muda()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid13"), "EM_ANDAMENTO");
        var caminhoSemeado = $"treinamentos/{Guid.NewGuid():N}.mp4";
        api.Api.Armazenamento.Semear(caminhoSemeado, Mp4(), ElencoDeTeste.Hoje, "video/mp4");
        var modulo = await ModuloAsync(api, id, 1, "VIDEO", caminho: caminhoSemeado, nomeArquivo: "Aula velha.mp4");
        var novo = Mp4(7000);

        var corrigiu = await AtualizarAsync(api, id, modulo, Modulo("Aula corrigida", arquivo: ("aula-nova.mp4", novo)));
        var mudouOTipo = await AtualizarAsync(api, id, modulo, Modulo("Virou texto", "TEXTO", texto: "a"));

        Assert.Equal(200, (int)corrigiu.StatusCode);
        Assert.Equal(422, (int)mudouOTipo.StatusCode);
        Assert.Equal("TREINAMENTO_CAMPO_TRAVADO", Codigo(await CorpoAsync(mudouOTipo)));
        Assert.False(api.Api.Armazenamento.Existe(caminhoSemeado));
    }

    // ---------------------------------------------------------------- excluir e limpar

    [Fact]
    public async Task Excluir_o_modulo_de_video_apaga_o_arquivo_depois_do_commit()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid14"), "RASCUNHO");
        var (modulo, caminho) = await VideoDeArquivoAsync(api, id);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota(id)}/{modulo}");

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.False(api.Api.Armazenamento.Existe(caminho));
    }

    [Fact]
    public async Task A_limpeza_de_orfaos_conhece_o_arquivo_do_video_e_nao_o_apaga()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZVid15"), "RASCUNHO");
        var (_, caminho) = await VideoDeArquivoAsync(api, id);
        var sobra = $"treinamentos/{Guid.NewGuid():N}.mp4";
        api.Api.Armazenamento.Semear(sobra, Mp4(), ElencoDeTeste.Hoje.AddDays(-3), "video/mp4");

        await using var escopo = api.Api.Services.CreateAsyncScope();
        var fonte = escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>().Single(f => f.Prefixo == "treinamentos/");

        var referenciados = await fonte.ReferenciadosAsync([caminho, sobra], CancellationToken.None);

        Assert.Equal([caminho], referenciados); // o do vídeo tem linha; a sobra não
    }

    // ---------------------------------------------------------------- a pessoa

    [Fact]
    public async Task A_pessoa_ve_temArquivo_no_modulo_de_video_e_pega_o_link_temporario_com_o_nome_do_arquivo()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZVid16 Ana");
        var t = await EmAndamentoAsync(api, Titulo("ZVid16"));
        var caminho = $"treinamentos/{Guid.NewGuid():N}.mp4";
        api.Api.Armazenamento.Semear(caminho, Mp4(), ElencoDeTeste.Hoje, "video/mp4");
        var video = await ModuloAsync(api, t, 1, "VIDEO", caminho: caminho, nomeArquivo: "aula-de-integracao.mp4");
        var porLink = await ModuloAsync(api, t, 2, "VIDEO");
        await LinhaAsync(api, t, eu, "INSCRITO");

        var conteudo = await cliente.GetAsync($"/api/eu/treinamentos/{t}/modulos/{video}");
        var conteudoDoLink = await cliente.GetAsync($"/api/eu/treinamentos/{t}/modulos/{porLink}");
        var download = await cliente.GetAsync($"/api/eu/treinamentos/{t}/modulos/{video}/download");
        var downloadSemArquivo = await cliente.GetAsync($"/api/eu/treinamentos/{t}/modulos/{porLink}/download");

        Assert.Equal((200, 200, 200, 404), ((int)conteudo.StatusCode, (int)conteudoDoLink.StatusCode, (int)download.StatusCode, (int)downloadSemArquivo.StatusCode));
        await conteudo.AfirmarConformeAoContratoAsync("obterConteudoDeModulo");
        await download.AfirmarConformeAoContratoAsync("baixarArquivoDeModulo");
        var corpo = await CorpoAsync(conteudo);
        Assert.Equal(("VIDEO", true, JsonValueKind.Null), (corpo.GetProperty("tipo").GetString(), corpo.GetProperty("temArquivo").GetBoolean(), corpo.GetProperty("urlExterna").ValueKind));
        var link = await CorpoAsync(download);
        Assert.Equal(["expiraEmUtc", "url"], link.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)); // nenhum campo com o caminho
        Assert.Contains("aula-de-integracao.mp4", link.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.Equal(("https://videos.exemplo.com.br/aula", false), ((await CorpoAsync(conteudoDoLink)).GetProperty("urlExterna").GetString(), (await CorpoAsync(conteudoDoLink)).GetProperty("temArquivo").GetBoolean()));
    }

    // ---------------------------------------------------------------- o teto da requisição

    [Fact]
    public async Task As_duas_rotas_de_modulo_declaram_o_teto_do_video_mais_uma_folga()
    {
        var api = await ApiAsync();
        var fontes = api.Api.Services.GetServices<EndpointDataSource>().SelectMany(f => f.Endpoints).OfType<RouteEndpoint>().ToList();

        var adicionar = fontes.Single(e => e.RoutePattern.RawText == "/api/rh/treinamentos/{treinamentoId:int}/modulos" && e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("POST"));
        var atualizar = fontes.Single(e => e.RoutePattern.RawText == "/api/rh/treinamentos/{treinamentoId:int}/modulos/{moduloId:int}" && e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("PUT"));

        var esperado = (100L * 1024 * 1024) + (1024 * 1024);
        Assert.Equal(105906176L, esperado); // 100 MiB do vídeo mais 1 MiB de folga para os outros campos
        Assert.Equal(105906176L, LimitesDoModulo.TetoDaRequisicaoBytes);
        foreach (var rota in new[] { adicionar, atualizar })
        {
            Assert.Equal(esperado, rota.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()!.MaxRequestBodySize);
            Assert.Equal(esperado, rota.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.RequestFormLimitsAttribute>()!.MultipartBodyLengthLimit);
        }
    }
}
