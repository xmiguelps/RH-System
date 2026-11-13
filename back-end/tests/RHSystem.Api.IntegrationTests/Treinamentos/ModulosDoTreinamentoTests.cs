using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// Os módulos do treinamento (<c>adicionarModuloDeTreinamento</c>, <c>reordenarModulosDeTreinamento</c>, <c>atualizarModuloDeTreinamento</c>, <c>excluirModuloDeTreinamento</c>), no banco REAL. Cada tipo exige o seu campo e recusa os dos outros (os erros vêm juntos); o PDF (até 10 MiB, pelo CONTEÚDO) vai para o armazenamento fora de transação e o caminho nunca
/// sai; só o RASCUNHO muda a estrutura (acrescentar, reordenar, excluir, trocar o tipo): depois de publicado, e também no encerrado, só se corrige o conteúdo; reordenar e renumerar usam duas fases (a ordem é única); o arquivo que deixa de valer é apagado DEPOIS do commit.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ModulosDoTreinamentoTests(MySqlFixture mysql)
{
    private static string Rota(int treinamento) => $"/api/rh/treinamentos/{treinamento}/modulos";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ModulosDoTreinamentoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static MultipartFormDataContent Modulo(string? titulo, string? tipo, string? duracao = null, string? url = null, string? texto = null, (string Nome, byte[] Bytes)? arquivo = null, (string Nome, byte[] Bytes)? segundo = null)
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
        Campo("duracaoMinutos", duracao);
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

    private static async Task<HttpResponseMessage> ReordenarAsync(ApiDeAcesso api, int treinamento, params int[] ids) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota(treinamento)}/ordem", new { moduloIds = ids });

    private static async Task<HttpResponseMessage> ExcluirAsync(ApiDeAcesso api, int treinamento, int modulo) => await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota(treinamento)}/{modulo}");

    private static async Task<JsonElement> ErrosAsync(HttpResponseMessage resposta) => (await CorpoAsync(resposta)).GetProperty("errors");

    private static Task<string?> OrdensAsync(ApiDeAcesso api, int treinamento) => TextoAsync(api, $"SELECT GROUP_CONCAT(ID_MODULO ORDER BY ORDEM SEPARATOR ',') FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {treinamento}");

    // ---------------------------------------------------------------- acrescentar

    [Fact]
    public async Task Cada_tipo_entra_no_fim_da_lista_e_o_pdf_vai_para_o_armazenamento_sem_o_caminho_na_resposta()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMod1"), "RASCUNHO");
        var pdf = ArquivosDeTeste.Pdf(2048);
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;

        var video = await AdicionarAsync(api, id, Modulo("  Abertura  ", "VIDEO", "15", url: "https://videos.exemplo.com.br/abertura"));
        var texto = await AdicionarAsync(api, id, Modulo("Leitura", "TEXTO", texto: "Texto do módulo."));
        var link = await AdicionarAsync(api, id, Modulo("Artigo", "LINK", url: "http://artigo.exemplo.com.br"));
        var arquivo = await AdicionarAsync(api, id, Modulo("Apostila", "PDF", "30", arquivo: ("../../Canais de denúncia.pdf", pdf)));

        Assert.Equal([201, 201, 201, 201], new[] { video, texto, link, arquivo }.Select(r => (int)r.StatusCode));
        await video.AfirmarConformeAoContratoAsync("adicionarModuloDeTreinamento");
        var corpoPdf = await CorpoAsync(arquivo);
        var ordens = new List<int>();
        foreach (var r in new[] { video, texto, link, arquivo })
        {
            ordens.Add((await CorpoAsync(r)).GetProperty("ordem").GetInt32());
        }

        Assert.Equal([1, 2, 3, 4], ordens);
        Assert.Equal(("Apostila", "PDF", 30, "Canais de denúncia.pdf", JsonValueKind.Null, JsonValueKind.Null), (corpoPdf.GetProperty("titulo").GetString(), corpoPdf.GetProperty("tipo").GetString(), corpoPdf.GetProperty("duracaoMinutos").GetInt32(), corpoPdf.GetProperty("nomeArquivo").GetString(), corpoPdf.GetProperty("urlExterna").ValueKind, corpoPdf.GetProperty("conteudoTexto").ValueKind));
        Assert.Equal("Abertura", (await CorpoAsync(video)).GetProperty("titulo").GetString()); // aparado
        var caminho = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id} AND TIPO_MODULO = 'PDF'"))!;
        Assert.StartsWith("treinamentos/", caminho, StringComparison.Ordinal);
        Assert.Equal(pdf, api.Api.Armazenamento.Conteudo(caminho));
        Assert.Equal(gravadosAntes + 1, api.Api.Armazenamento.Gravados.Count);
        Assert.DoesNotContain("treinamentos/", await arquivo.Content.ReadAsStringAsync(), StringComparison.Ordinal); // o caminho nunca sai
        Assert.Equal("Canais de denúncia.pdf", await TextoAsync(api, $"SELECT NOME_ARQUIVO_ORIGINAL FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id} AND TIPO_MODULO = 'PDF'")); // sem o diretório do nome
        var evento = (await EventosAsync(api, id)).Count;
        Assert.Equal(4, evento);
    }

    [Fact]
    public async Task Os_erros_do_modulo_vem_juntos_e_nada_e_gravado_nem_guardado()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMod2"), "RASCUNHO");
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        var pdf = ArquivosDeTeste.Pdf();
        (string Nome, MultipartFormDataContent Corpo, string[] Chaves)[] casos =
        [
            ("tudo errado", Modulo("  ", "VIDEO", "0", url: "javascript:alert(1)", texto: "sobra", arquivo: ("a.pdf", pdf)), ["titulo", "duracaoMinutos", "urlExterna", "conteudoTexto", "arquivo"]),
            ("sem tipo", Modulo("Sem tipo", null), ["tipo"]),
            ("tipo desconhecido", Modulo("Tipo", "QUIZ"), ["tipo"]),
            ("vídeo sem endereço", Modulo("Vídeo", "VIDEO"), ["urlExterna"]),
            ("texto em branco", Modulo("Texto", "TEXTO", texto: "   "), ["conteudoTexto"]),
            ("link com arquivo", Modulo("Link", "LINK", url: "https://a.com", arquivo: ("a.pdf", pdf)), ["arquivo"]),
            ("vídeo com arquivo", Modulo("Vídeo", "VIDEO", url: "https://a.com", arquivo: ("a.pdf", pdf)), ["arquivo"]),
            ("PDF sem arquivo", Modulo("PDF", "PDF"), ["arquivo"]),
            ("PDF com endereço", Modulo("PDF", "PDF", url: "https://a.com", arquivo: ("a.pdf", pdf)), ["urlExterna"]),
            ("exe renomeado para pdf", Modulo("PDF", "PDF", arquivo: ("a.pdf", ArquivosDeTeste.Exe())), ["arquivo"]),
            ("png", Modulo("PDF", "PDF", arquivo: ("a.pdf", ArquivosDeTeste.Png())), ["arquivo"]),
            ("arquivo vazio", Modulo("PDF", "PDF", arquivo: ("a.pdf", [])), ["arquivo"]),
            ("PDF de 11 MB", Modulo("PDF", "PDF", arquivo: ("a.pdf", ArquivosDeTeste.Pdf((10 * 1024 * 1024) + 1))), ["arquivo"]),
            ("dois arquivos", Modulo("PDF", "PDF", arquivo: ("a.pdf", pdf), segundo: ("b.pdf", pdf)), ["arquivo"]),
            ("título de 201 caracteres", Modulo(new string('t', 201), "TEXTO", texto: "a"), ["titulo"]),
            ("duração de 32768", Modulo("Duração", "TEXTO", "32768", texto: "a"), ["duracaoMinutos"]),
            ("duração que não é número", Modulo("Duração", "TEXTO", "dez", texto: "a"), ["duracaoMinutos"]),
            ("texto de 50001 caracteres", Modulo("Texto", "TEXTO", texto: new string('c', 50001)), ["conteudoTexto"]),
        ];

        var falhas = new List<string>();
        foreach (var (nome, corpo, chaves) in casos)
        {
            var resposta = await AdicionarAsync(api, id, corpo);
            var obtidas = (int)resposta.StatusCode == 400 ? (await ErrosAsync(resposta)).EnumerateObject().Select(p => p.Name).Order().ToArray() : [];
            if ((int)resposta.StatusCode != 400 || !chaves.Order().SequenceEqual(obtidas))
            {
                falhas.Add($"{nome}: {(int)resposta.StatusCode} [{string.Join(',', obtidas)}]");
            }
        }

        var json = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync(Rota(id), new { titulo = "Json" });
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).PostAsync(Rota(id), Modulo("Gestor", "TEXTO", texto: "a"));
        var semLogin = await api.Api.ClienteAnonimo().PostAsync(Rota(id), Modulo("Anônimo", "TEXTO", texto: "a"));
        var inexistente = await AdicionarAsync(api, 99999999, Modulo("Inexistente", "TEXTO", texto: "a"));

        Assert.Empty(falhas);
        Assert.Equal([415, 403, 401, 404], new[] { json, gestor, semLogin, inexistente }.Select(r => (int)r.StatusCode));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id}"));
        Assert.Equal(gravadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    [Fact]
    public async Task Duas_abas_acrescentando_modulo_ao_mesmo_tempo_ficam_em_posicoes_diferentes()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMod3"), "RASCUNHO");
        var rh = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(1, 4).Select(i => rh.PostAsync(Rota(id), Modulo($"Módulo {i}", "TEXTO", texto: "a"))));

        Assert.All(respostas, r => Assert.Equal(201, (int)r.StatusCode));
        Assert.Equal("1,2,3,4", await TextoAsync(api, $"SELECT GROUP_CONCAT(ORDEM ORDER BY ORDEM) FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id}"));
    }

    // ---------------------------------------------------------------- depois de publicado

    [Fact]
    public async Task Publicado_e_encerrado_travam_a_estrutura_mas_aceitam_corrigir_o_conteudo()
    {
        var api = await ApiAsync();
        var publicado = await SemearAsync(api, Titulo("ZMod4 publicado"), "EM_ANDAMENTO");
        var m1 = await ModuloAsync(api, publicado, 1, "TEXTO");
        var m2 = await ModuloAsync(api, publicado, 2, "TEXTO");
        var encerrado = await SemearAsync(api, Titulo("ZMod4 encerrado"), "ENCERRADO");
        var e1 = await ModuloAsync(api, encerrado, 1, "TEXTO");

        var respostas = new List<HttpResponseMessage>
        {
            await AdicionarAsync(api, publicado, Modulo("Novo", "TEXTO", texto: "a")),
            await ReordenarAsync(api, publicado, m2, m1),
            await ExcluirAsync(api, publicado, m1),
            await AdicionarAsync(api, encerrado, Modulo("Novo", "TEXTO", texto: "a")),
            await ExcluirAsync(api, encerrado, e1),
        };
        var corrige = await AtualizarAsync(api, publicado, m1, Modulo("Título corrigido", "TEXTO", "7", texto: "Texto corrigido."));
        var corrigeEncerrado = await AtualizarAsync(api, encerrado, e1, Modulo("Corrigido depois de encerrado", "TEXTO", texto: "Texto corrigido."));
        var trocaTipo = await AtualizarAsync(api, publicado, m2, Modulo("Agora é link", "LINK", url: "https://a.com"));

        Assert.All(respostas, r => Assert.Equal(422, (int)r.StatusCode));
        foreach (var r in respostas)
        {
            var corpo = await CorpoAsync(r);
            Assert.Equal(("TREINAMENTO_MODULOS_TRAVADOS", "Este treinamento já foi publicado. Não dá mais para acrescentar, reordenar nem excluir módulos; só corrigir o conteúdo deles."), (Codigo(corpo), corpo.GetProperty("detail").GetString()));
        }

        Assert.Equal([200, 200, 422], new[] { corrige, corrigeEncerrado, trocaTipo }.Select(r => (int)r.StatusCode));
        Assert.Equal("TREINAMENTO_CAMPO_TRAVADO", Codigo(await CorpoAsync(trocaTipo)));
        Assert.Equal("Depois de publicado, o tipo do módulo não muda. Dá para corrigir o título, a duração e o conteúdo.", (await CorpoAsync(trocaTipo)).GetProperty("detail").GetString());
        Assert.Equal(("Título corrigido", "Texto corrigido.", "1,2"), ((await CorpoAsync(corrige)).GetProperty("titulo").GetString(), (await CorpoAsync(corrige)).GetProperty("conteudoTexto").GetString(), (await TextoAsync(api, $"SELECT GROUP_CONCAT(ORDEM ORDER BY ORDEM) FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {publicado}"))!)); // a ordem não mudou
    }

    // ---------------------------------------------------------------- reordenar

    [Fact]
    public async Task Reordenar_move_o_terceiro_para_o_primeiro_sem_1062_e_recusa_ids_que_nao_batem()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMod5"), "RASCUNHO");
        var outro = await SemearAsync(api, Titulo("ZMod5 outro"), "RASCUNHO");
        var m1 = await ModuloAsync(api, id, 1);
        var m2 = await ModuloAsync(api, id, 2);
        var m3 = await ModuloAsync(api, id, 3);
        var deOutro = await ModuloAsync(api, outro, 1);

        var move = await ReordenarAsync(api, id, m3, m1, m2);
        var faltando = await ReordenarAsync(api, id, m3, m1);
        var repetido = await ReordenarAsync(api, id, m3, m3, m1, m2);
        var estranho = await ReordenarAsync(api, id, m3, m1, deOutro);
        var vazio = await ReordenarAsync(api, id);
        var inexistente = await ReordenarAsync(api, 99999999, m1);

        Assert.Equal(200, (int)move.StatusCode);
        await move.AfirmarConformeAoContratoAsync("reordenarModulosDeTreinamento");
        var itens = (await CorpoAsync(move)).GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal([m3, m1, m2], itens.Select(i => i.GetProperty("id").GetInt32()));
        Assert.Equal([1, 2, 3], itens.Select(i => i.GetProperty("ordem").GetInt32()));
        Assert.Equal($"{m3},{m1},{m2}", await OrdensAsync(api, id));
        Assert.Equal([400, 400, 400, 400, 404], new[] { faltando, repetido, estranho, vazio, inexistente }.Select(r => (int)r.StatusCode));
        Assert.True((await ErrosAsync(faltando)).TryGetProperty("moduloIds", out _));
        Assert.Equal($"{m3},{m1},{m2}", await OrdensAsync(api, id)); // nenhum erro mexeu na ordem
    }

    // ---------------------------------------------------------------- atualizar

    [Fact]
    public async Task Atualizar_o_modulo_no_rascunho_troca_o_tipo_e_so_apaga_o_arquivo_que_deixou_de_valer_depois_do_commit()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMod6"), "RASCUNHO");
        var pdf1 = ArquivosDeTeste.Pdf(1500);
        var pdf2 = ArquivosDeTeste.Pdf(2500);
        var criado = await CorpoAsync(await AdicionarAsync(api, id, Modulo("Apostila", "PDF", arquivo: ("primeira.pdf", pdf1))));
        var modulo = criado.GetProperty("id").GetInt32();
        var primeiro = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"))!;

        var mantem = await AtualizarAsync(api, id, modulo, Modulo("Apostila revisada", "PDF", "20")); // sem arquivo novo: o atual fica
        var mantido = await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}");
        var substitui = await AtualizarAsync(api, id, modulo, Modulo("Apostila nova", "PDF", arquivo: ("segunda.pdf", pdf2)));
        var segundo = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}"))!;
        var conteudoDoSegundo = api.Api.Armazenamento.Conteudo(segundo);
        var viraTexto = await AtualizarAsync(api, id, modulo, Modulo("Agora é texto", "TEXTO", texto: "Texto."));
        var viraPdf = await AtualizarAsync(api, id, modulo, Modulo("Volta a ser PDF", "PDF")); // trocar para PDF exige o arquivo

        Assert.Equal([200, 200, 200, 400], new[] { mantem, substitui, viraTexto, viraPdf }.Select(r => (int)r.StatusCode));
        await mantem.AfirmarConformeAoContratoAsync("atualizarModuloDeTreinamento");
        Assert.Equal((primeiro, "primeira.pdf"), (mantido!, (await CorpoAsync(mantem)).GetProperty("nomeArquivo").GetString())); // manteve o caminho e o nome
        Assert.NotEqual(primeiro, segundo);
        Assert.Equal("segunda.pdf", (await CorpoAsync(substitui)).GetProperty("nomeArquivo").GetString());
        Assert.Equal(pdf2, conteudoDoSegundo);
        Assert.False(api.Api.Armazenamento.Existe(primeiro)); // o antigo saiu depois do commit da troca
        Assert.False(api.Api.Armazenamento.Existe(segundo)); // e o da troca por texto também
        Assert.Equal((JsonValueKind.Null, "Texto."), ((await CorpoAsync(viraTexto)).GetProperty("nomeArquivo").ValueKind, (await CorpoAsync(viraTexto)).GetProperty("conteudoTexto").GetString()));
        Assert.Equal("1,Agora é texto,TEXTO,,,Texto.", await TextoAsync(api, $"SELECT CONCAT(ORDEM, ',', TITULO, ',', TIPO_MODULO, ',', IFNULL(CAMINHO_ARQUIVO, ''), ',', IFNULL(NOME_ARQUIVO_ORIGINAL, ''), ',', IFNULL(CONTEUDO_TEXTO, '')) FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {modulo}")); // o caminho e o nome saem juntos
    }

    [Fact]
    public async Task Modulo_de_outro_treinamento_e_inexistente_dao_404_e_so_o_RH_mexe()
    {
        var api = await ApiAsync();
        var a = await SemearAsync(api, Titulo("ZMod7 a"), "RASCUNHO");
        var b = await SemearAsync(api, Titulo("ZMod7 b"), "RASCUNHO");
        var deB = await ModuloAsync(api, b, 1);

        var atualiza = await AtualizarAsync(api, a, deB, Modulo("Roubado", "TEXTO", texto: "a"));
        var exclui = await ExcluirAsync(api, a, deB);
        var inexistente = await AtualizarAsync(api, a, 99999999, Modulo("Inexistente", "TEXTO", texto: "a"));
        var treinamentoInexistente = await ExcluirAsync(api, 99999999, deB);
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).DeleteAsync($"{Rota(b)}/{deB}");
        var semLogin = await api.Api.ClienteAnonimo().DeleteAsync($"{Rota(b)}/{deB}");

        Assert.Equal([404, 404, 404, 404, 403, 401], new[] { atualiza, exclui, inexistente, treinamentoInexistente, gestor, semLogin }.Select(r => (int)r.StatusCode));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {deB} AND TITULO = 'Módulo 1'"));
    }

    // ---------------------------------------------------------------- excluir

    [Fact]
    public async Task Excluir_o_modulo_renumera_os_que_sobram_sem_buraco_e_apaga_o_arquivo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMod8"), "RASCUNHO");
        var m1 = await ModuloAsync(api, id, 1);
        var pdf = await CorpoAsync(await AdicionarAsync(api, id, Modulo("Apostila", "PDF", arquivo: ("a.pdf", ArquivosDeTeste.Pdf()))));
        var m2 = pdf.GetProperty("id").GetInt32();
        var m3 = await ModuloAsync(api, id, 3);
        var m4 = await ModuloAsync(api, id, 4);
        var caminho = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {m2}"))!;
        Assert.Equal($"{m1},{m2},{m3},{m4}", await OrdensAsync(api, id));

        var exclui = await ExcluirAsync(api, id, m2);

        Assert.Equal(204, (int)exclui.StatusCode);
        Assert.Equal($"{m1},{m3},{m4}", await OrdensAsync(api, id));
        Assert.Equal("1,2,3", await TextoAsync(api, $"SELECT GROUP_CONCAT(ORDEM ORDER BY ORDEM) FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id}")); // sem buraco
        Assert.False(api.Api.Armazenamento.Existe(caminho));
        Assert.Contains((AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou), (await EventosAsync(api, id)).Select(e => (e.Area, e.Acao)));
    }

    // ---------------------------------------------------------------- limpeza de órfãos

    [Fact]
    public async Task A_limpeza_de_orfaos_conhece_o_arquivo_do_modulo_e_nao_o_apaga_mas_apaga_a_sobra()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMod9"), "RASCUNHO");
        await AdicionarAsync(api, id, Modulo("Apostila", "PDF", arquivo: ("a.pdf", ArquivosDeTeste.Pdf())));
        var referenciado = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id}"))!;
        var sobra = CaminhosDeArquivo.Novo("treinamentos", ".pdf");
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var fonte = escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>().Single(f => f.Prefixo == "treinamentos/");

        var referenciados = await fonte.ReferenciadosAsync([referenciado, sobra], CancellationToken.None);

        Assert.Equal([referenciado], referenciados);
    }
}
