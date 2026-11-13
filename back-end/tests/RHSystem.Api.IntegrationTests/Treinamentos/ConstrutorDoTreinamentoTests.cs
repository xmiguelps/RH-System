using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// Criar, obter, atualizar e contar o público de um treinamento (<c>criarTreinamento</c>, <c>obterTreinamento</c>, <c>atualizarTreinamento</c>, <c>contarPublicoDeTreinamento</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. O treinamento nasce rascunho só com o título; as duas caixas que o banco liga por padrão são gravadas como vieram; <c>PUT</c> substitui
/// TODOS os campos (omitido volta ao padrão) e o público trocado apaga as linhas antigas; depois de publicado só muda título, descrição, a caixa de admitidos e a data enquanto agendado; encerrado não muda nada; a contagem só olha pessoas ativas com a admissão já ocorrida.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConstrutorDoTreinamentoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/treinamentos";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ConstrutorDoTreinamentoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> CriarAsync(ApiDeAcesso api, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync(Rota, corpo);

    private static async Task<HttpResponseMessage> AtualizarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}", corpo);

    private static async Task<JsonElement> ErrosAsync(HttpResponseMessage resposta) => (await CorpoAsync(resposta)).GetProperty("errors");

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int? setor = null, bool ativa = true, DateOnly? admissao = null) =>
        ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, setor, ativa, admissao ?? new DateOnly(2026, 1, 2));

    // ---------------------------------------------------------------- criar

    [Fact]
    public async Task Criar_so_com_o_titulo_grava_o_rascunho_com_as_duas_caixas_ligadas_e_o_resto_vazio()
    {
        var api = await ApiAsync();
        var titulo = Titulo("ZTre1");

        var resposta = await CriarAsync(api, new { titulo });
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("criarTreinamento");
        Assert.Equal((titulo, "RASCUNHO", false, true, true, false), (corpo.GetProperty("titulo").GetString(), corpo.GetProperty("situacao").GetString(), corpo.GetProperty("publicado").GetBoolean(), corpo.GetProperty("emiteCertificado").GetBoolean(), corpo.GetProperty("incluiAdmitidos").GetBoolean(), corpo.GetProperty("onboarding").GetBoolean()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, 0, 0, 0), (corpo.GetProperty("publico").ValueKind, corpo.GetProperty("comecaEm").ValueKind, corpo.GetProperty("descricao").ValueKind, corpo.GetProperty("encerradoEmUtc").ValueKind, corpo.GetProperty("setores").GetArrayLength(), corpo.GetProperty("pessoas").GetArrayLength(), corpo.GetProperty("modulos").GetArrayLength()));
        Assert.Equal("0,1,1,0,0,0", await TextoAsync(api, $"SELECT CONCAT(PUBLICADO, ',', EMITE_CERTIFICADO, ',', INCLUI_ADMITIDOS, ',', OBRIGATORIO, ',', AVISA_POR_EMAIL, ',', LEMBRA_ANTES_DO_FIM) FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"));
        Assert.Null(await TextoAsync(api, $"SELECT DATA_FIM FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Criou), (evento.Area, evento.Acao));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Criar_grava_o_que_veio_inclusive_as_caixas_falsas_o_onboarding_e_a_data()
    {
        var api = await ApiAsync();

        var resposta = await CriarAsync(api, new { titulo = Titulo("ZTre2"), descricao = "  Conceitos.  ", comecaEm = "2026-11-16", obrigatorio = true, onboarding = true, emiteCertificado = false, publico = "TODOS", incluiAdmitidos = false });
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(("Conceitos.", "2026-11-16", true, true, false, false, "TODOS"), (corpo.GetProperty("descricao").GetString(), corpo.GetProperty("comecaEm").GetString(), corpo.GetProperty("obrigatorio").GetBoolean(), corpo.GetProperty("onboarding").GetBoolean(), corpo.GetProperty("emiteCertificado").GetBoolean(), corpo.GetProperty("incluiAdmitidos").GetBoolean(), corpo.GetProperty("publico").GetString()));
        Assert.Equal("1,0,0,ONBOARDING,TODOS,2026-11-16", await TextoAsync(api, $"SELECT CONCAT(OBRIGATORIO, ',', EMITE_CERTIFICADO, ',', INCLUI_ADMITIDOS, ',', TIPO_TREINAMENTO, ',', PUBLICO, ',', DATA_INICIO) FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}")); // as duas caixas FALSE chegam ao banco (o padrão dele é TRUE)
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {id}")); // TODOS não guarda linha
    }

    [Fact]
    public async Task Criar_por_setores_e_por_pessoas_guarda_uma_linha_por_destino_e_devolve_os_nomes()
    {
        var api = await ApiAsync();
        var setorA = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZTre3 setor A"));
        var setorB = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZTre3 setor B"));
        var ana = await PessoaAsync(api, "ZTre3 Ana", setorA);
        var bia = await PessoaAsync(api, "ZTre3 Bia", setorB);

        var porSetores = await CorpoAsync(await CriarAsync(api, new { titulo = Titulo("ZTre3 por setores"), publico = "SETORES", setorIds = new[] { setorB, setorA } }));
        var porPessoas = await CorpoAsync(await CriarAsync(api, new { titulo = Titulo("ZTre3 por pessoas"), publico = "PESSOAS", funcionarioIds = new[] { bia, ana } }));

        Assert.Equal([setorA, setorB], porSetores.GetProperty("setores").EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).Order());
        Assert.Equal(2, porSetores.GetProperty("setores")[0].EnumerateObject().Count()); // só id e nome
        Assert.Equal($"{ana},{bia}", string.Join(',', porPessoas.GetProperty("pessoas").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).Order()));
        Assert.Equal(("SETORES", "PESSOAS"), (porSetores.GetProperty("publico").GetString(), porPessoas.GetProperty("publico").GetString()));
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {porSetores.GetProperty("id").GetInt32()} AND ID_SETOR IS NOT NULL AND ID_FUNCIONARIO IS NULL"));
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {porPessoas.GetProperty("id").GetInt32()} AND ID_FUNCIONARIO IS NOT NULL AND ID_SETOR IS NULL"));
    }

    [Fact]
    public async Task Criar_com_erros_devolve_todos_juntos_e_nao_grava_nada()
    {
        var api = await ApiAsync();
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM TREINAMENTOS");
        var rh = await api.ClienteDoRhAgoraAsync();

        var tudoErrado = await CriarAsync(api, new { titulo = "ab", descricao = new string('d', 2001), publico = "TODOS", setorIds = new[] { 1 }, funcionarioIds = new[] { 2 } });
        var inexistentes = await CriarAsync(api, new { titulo = Titulo("ZTre4"), publico = "SETORES", setorIds = new[] { 99999999 } });
        var pessoaInexistente = await CriarAsync(api, new { titulo = Titulo("ZTre4"), publico = "PESSOAS", funcionarioIds = new[] { 99999999 } });
        var semCorpo = await rh.PostAsync(Rota, content: null);
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).PostAsJsonAsync(Rota, new { titulo = "Gestor" });
        var semLogin = await api.Api.ClienteAnonimo().PostAsJsonAsync(Rota, new { titulo = "Anônimo" });

        Assert.Equal([400, 400, 400, 400, 403, 401], new[] { tudoErrado, inexistentes, pessoaInexistente, semCorpo, gestor, semLogin }.Select(r => (int)r.StatusCode));
        Assert.Equal(["descricao", "funcionarioIds", "setorIds", "titulo"], (await ErrosAsync(tudoErrado)).EnumerateObject().Select(p => p.Name).Order());
        Assert.True((await ErrosAsync(inexistentes)).TryGetProperty("setorIds", out _));
        Assert.True((await ErrosAsync(pessoaInexistente)).TryGetProperty("funcionarioIds", out _));
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM TREINAMENTOS"));
    }

    // ---------------------------------------------------------------- obter

    [Fact]
    public async Task Obter_devolve_os_modulos_em_ordem_com_o_conteudo_e_o_pdf_so_como_nome()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZTre5"), "RASCUNHO", descricao: "Descrição.");
        await ModuloAsync(api, id, 3, "PDF", "Canais", 5, "treinamentos/segredo-interno.pdf", "canais-de-denuncia.pdf");
        await ModuloAsync(api, id, 1, "VIDEO", "Abertura", 15);
        await ModuloAsync(api, id, 2, "TEXTO", "Leitura", null);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}");
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterTreinamento");
        var modulos = corpo.GetProperty("modulos").EnumerateArray().ToList();
        Assert.Equal([1, 2, 3], modulos.Select(m => m.GetProperty("ordem").GetInt32()));
        Assert.Equal(("VIDEO", "https://videos.exemplo.com.br/aula", JsonValueKind.Null), (modulos[0].GetProperty("tipo").GetString(), modulos[0].GetProperty("urlExterna").GetString(), modulos[0].GetProperty("conteudoTexto").ValueKind));
        Assert.Equal(("Texto do módulo.", JsonValueKind.Null, JsonValueKind.Null), (modulos[1].GetProperty("conteudoTexto").GetString(), modulos[1].GetProperty("duracaoMinutos").ValueKind, modulos[1].GetProperty("nomeArquivo").ValueKind));
        Assert.Equal(("canais-de-denuncia.pdf", 5), (modulos[2].GetProperty("nomeArquivo").GetString(), modulos[2].GetProperty("duracaoMinutos").GetInt32()));
        Assert.DoesNotContain("segredo-interno", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal); // o caminho de armazenamento nunca sai
        Assert.Equal(8, modulos[2].EnumerateObject().Count()); // id, ordem, titulo, tipo, duracaoMinutos, urlExterna, conteudoTexto, nomeArquivo
    }

    [Fact]
    public async Task Obter_mostra_a_situacao_calculada_pelas_datas_e_so_o_RH_entra()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("ZTre6 rascunho"), "RASCUNHO");
        var agendado = await SemearAsync(api, Titulo("ZTre6 agendado"), "AGENDADO");
        var andamento = await SemearAsync(api, Titulo("ZTre6 andamento"), "EM_ANDAMENTO");
        var encerrado = await SemearAsync(api, Titulo("ZTre6 encerrado"), "ENCERRADO");
        var rh = await api.ClienteDoRhAgoraAsync();

        var situacoes = new List<(string, bool, string?)>();
        foreach (var id in new[] { rascunho, agendado, andamento, encerrado })
        {
            var corpo = await CorpoAsync(await rh.GetAsync($"{Rota}/{id}"));
            situacoes.Add((corpo.GetProperty("situacao").GetString()!, corpo.GetProperty("publicado").GetBoolean(), corpo.GetProperty("encerradoEmUtc").GetString()));
        }

        Assert.Equal([("RASCUNHO", false, (string?)null), ("AGENDADO", true, null), ("EM_ANDAMENTO", true, null), ("ENCERRADO", true, "2026-10-01T03:00:00Z")], situacoes);
        Assert.Equal([404, 403, 403, 401], new[] { await rh.GetAsync($"{Rota}/99999999"), await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync($"{Rota}/{rascunho}"), await api.Api.ClienteComo(api.Elenco.MembroA).GetAsync($"{Rota}/{rascunho}"), await api.Api.ClienteAnonimo().GetAsync($"{Rota}/{rascunho}") }.Select(r => (int)r.StatusCode));
    }

    // ---------------------------------------------------------------- atualizar

    [Fact]
    public async Task O_PUT_do_rascunho_substitui_tudo_o_omitido_volta_ao_padrao_e_o_publico_trocado_apaga_as_linhas_antigas()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZTre7 setor"));
        var ana = await PessoaAsync(api, "ZTre7 Ana", setor);
        var criado = await CorpoAsync(await CriarAsync(api, new { titulo = Titulo("ZTre7"), descricao = "Antes.", comecaEm = "2026-11-01", obrigatorio = true, onboarding = true, emiteCertificado = false, publico = "SETORES", setorIds = new[] { setor }, incluiAdmitidos = false }));
        var id = criado.GetProperty("id").GetInt32();

        var troca = await AtualizarAsync(api, id, new { titulo = "Título novo", publico = "PESSOAS", funcionarioIds = new[] { ana } });
        var corpo = await CorpoAsync(troca);

        Assert.Equal(200, (int)troca.StatusCode);
        await troca.AfirmarConformeAoContratoAsync("atualizarTreinamento");
        Assert.Equal(("Título novo", JsonValueKind.Null, JsonValueKind.Null, false, false, true, true, "PESSOAS"), (corpo.GetProperty("titulo").GetString(), corpo.GetProperty("descricao").ValueKind, corpo.GetProperty("comecaEm").ValueKind, corpo.GetProperty("obrigatorio").GetBoolean(), corpo.GetProperty("onboarding").GetBoolean(), corpo.GetProperty("emiteCertificado").GetBoolean(), corpo.GetProperty("incluiAdmitidos").GetBoolean(), corpo.GetProperty("publico").GetString())); // omitido volta ao padrão: caixas ligadas
        Assert.Equal((0, 1), (corpo.GetProperty("setores").GetArrayLength(), corpo.GetProperty("pessoas").GetArrayLength()));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {id}"));
        Assert.Null(await TextoAsync(api, $"SELECT TIPO_TREINAMENTO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"));
        Assert.Equal(2, (await EventosAsync(api, id)).Count); // criou e alterou
        Assert.Contains((AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou), (await EventosAsync(api, id)).Select(e => (e.Area, e.Acao)));
    }

    [Fact]
    public async Task O_PUT_com_a_lista_do_outro_publico_ou_dado_invalido_e_400_com_tudo_junto_e_nada_muda()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZTre8"), "RASCUNHO");

        var resposta = await AtualizarAsync(api, id, new { titulo = "x", publico = "TODOS", setorIds = new[] { 3 } });

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Equal(["setorIds", "titulo"], (await ErrosAsync(resposta)).EnumerateObject().Select(p => p.Name).Order());
        Assert.StartsWith("ZTre8", await TextoAsync(api, $"SELECT TITULO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}") ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(404, (int)(await AtualizarAsync(api, 99999999, new { titulo = "Inexistente" })).StatusCode);
    }

    [Fact]
    public async Task Depois_de_publicado_so_muda_titulo_descricao_a_caixa_de_admitidos_e_a_data_enquanto_agendado()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZTre9 setor"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZTre9 outro"));
        var agendado = await SemearAsync(api, Titulo("ZTre9 agendado"), "AGENDADO", obrigatorio: true, publico: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({agendado}, {setor}), ({agendado}, {outroSetor})");
        var andamento = await SemearAsync(api, Titulo("ZTre9 andamento"), "EM_ANDAMENTO", obrigatorio: true, publico: "TODOS");

        var livre = await AtualizarAsync(api, agendado, new { titulo = "Título corrigido", descricao = "Nova.", comecaEm = "2026-10-25", obrigatorio = true, publico = "SETORES", setorIds = new[] { outroSetor, setor }, incluiAdmitidos = false });
        var obrigatorioMudou = await AtualizarAsync(api, agendado, new { titulo = "x ok", comecaEm = "2026-10-25", obrigatorio = false, publico = "SETORES", setorIds = new[] { setor, outroSetor } });
        var publicoMudou = await AtualizarAsync(api, agendado, new { titulo = "x ok", comecaEm = "2026-10-25", obrigatorio = true, publico = "SETORES", setorIds = new[] { setor } });
        var certificadoMudou = await AtualizarAsync(api, agendado, new { titulo = "x ok", comecaEm = "2026-10-25", obrigatorio = true, emiteCertificado = false, publico = "SETORES", setorIds = new[] { setor, outroSetor } });
        var semData = await AtualizarAsync(api, agendado, new { titulo = "x ok", comecaEm = (string?)null, obrigatorio = true, publico = "SETORES", setorIds = new[] { setor, outroSetor } });
        var andamentoComData = await AtualizarAsync(api, andamento, new { titulo = "Título em andamento", comecaEm = "2026-10-02", obrigatorio = true, publico = "TODOS" });
        var andamentoLivre = await AtualizarAsync(api, andamento, new { titulo = "Título em andamento", descricao = "Nova.", comecaEm = "2026-10-01", obrigatorio = true, publico = "TODOS", incluiAdmitidos = false });

        Assert.Equal([200, 422, 422, 422, 400, 422, 200], new[] { livre, obrigatorioMudou, publicoMudou, certificadoMudou, semData, andamentoComData, andamentoLivre }.Select(r => (int)r.StatusCode));
        await livre.AfirmarConformeAoContratoAsync("atualizarTreinamento");
        Assert.Equal(("Título corrigido", "2026-10-25", false), ((await CorpoAsync(livre)).GetProperty("titulo").GetString(), (await CorpoAsync(livre)).GetProperty("comecaEm").GetString(), (await CorpoAsync(livre)).GetProperty("incluiAdmitidos").GetBoolean()));
        var travado = await CorpoAsync(obrigatorioMudou);
        Assert.Equal(("TREINAMENTO_CAMPO_TRAVADO", "Depois de publicado, só dá para mudar o título, a descrição e a opção de incluir quem for admitido.", "Operação recusada."), (Codigo(travado), travado.GetProperty("detail").GetString(), travado.GetProperty("title").GetString()));
        Assert.Equal("TREINAMENTO_CAMPO_TRAVADO", Codigo(await CorpoAsync(andamentoComData))); // em andamento a data também trava
        Assert.True((await ErrosAsync(semData)).TryGetProperty("comecaEm", out _));
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {agendado}")); // o público não mexeu
    }

    [Fact]
    public async Task Encerrado_nao_muda_nada_nem_com_o_corpo_igual_ao_atual()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZTre10"), "ENCERRADO");
        var atual = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}"));

        var resposta = await AtualizarAsync(api, id, new { titulo = atual.GetProperty("titulo").GetString(), comecaEm = "2026-09-01", obrigatorio = false, publico = "TODOS" });

        Assert.Equal(422, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("TREINAMENTO_ENCERRADO", "Treinamento encerrado.", "Este treinamento foi encerrado e não pode mais ser alterado."), (Codigo(corpo), corpo.GetProperty("title").GetString(), corpo.GetProperty("detail").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("atualizarTreinamento");
    }

    // ---------------------------------------------------------------- contar o público

    [Fact]
    public async Task A_contagem_do_publico_so_olha_pessoas_ativas_com_a_admissao_ja_ocorrida()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZTre11 setor"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZTre11 outro"));
        var a = await PessoaAsync(api, "ZTre11 A", setor);
        var b = await PessoaAsync(api, "ZTre11 B", setor);
        var c = await PessoaAsync(api, "ZTre11 C", outroSetor);
        var inativa = await PessoaAsync(api, "ZTre11 Inativa", setor, ativa: false);
        var emAdmissao = await PessoaAsync(api, "ZTre11 Nova", setor, admissao: new DateOnly(2026, 10, 20));
        var admitidaHoje = await PessoaAsync(api, "ZTre11 Hoje", setor, admissao: new DateOnly(2026, 10, 7));
        var rh = await api.ClienteDoRhAgoraAsync();
        async Task<JsonElement> ContarAsync(string consulta) => await CorpoAsync(await rh.GetAsync($"{Rota}/publico/contagem?{consulta}"));

        var todos = await ContarAsync("publico=TODOS&setorIds=999&funcionarioIds=999");
        var porSetor = await ContarAsync($"publico=SETORES&setorIds={setor}");
        var porDoisSetores = await ContarAsync($"publico=SETORES&setorIds={setor}&setorIds={outroSetor}&funcionarioIds={c}");
        var porPessoas = await ContarAsync($"publico=PESSOAS&funcionarioIds={a}&funcionarioIds={inativa}&funcionarioIds={emAdmissao}&funcionarioIds={c}&funcionarioIds=99999999&setorIds={setor}");
        var setorVazio = await ContarAsync("publico=SETORES");
        var resposta = await rh.GetAsync($"{Rota}/publico/contagem?publico=SETORES&setorIds={setor}");

        Assert.Equal(await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = TRUE AND DATA_ADMISSAO <= '2026-10-07'"), todos.GetProperty("total").GetInt64()); // TODOS ignora as listas
        Assert.Equal((3, 4, 2, 0), (porSetor.GetProperty("total").GetInt32(), porDoisSetores.GetProperty("total").GetInt32(), porPessoas.GetProperty("total").GetInt32(), setorVazio.GetProperty("total").GetInt32())); // A, B e a admitida hoje; mais C; A e C; nenhum setor
        await resposta.AfirmarConformeAoContratoAsync("contarPublicoDeTreinamento");
        Assert.True(b > 0 && admitidaHoje > 0);
    }

    [Fact]
    public async Task A_contagem_recusa_o_que_nao_e_publico_e_listas_demais_e_so_o_RH_conta()
    {
        var api = await ApiAsync();
        var rh = await api.ClienteDoRhAgoraAsync();
        string Ids(string nome, int quantos) => string.Join('&', Enumerable.Range(1, quantos).Select(i => $"{nome}={i}"));

        var semPublico = await rh.GetAsync($"{Rota}/publico/contagem");
        var invalido = await rh.GetAsync($"{Rota}/publico/contagem?publico=CARGO");
        var setoresDemais = await rh.GetAsync($"{Rota}/publico/contagem?publico=SETORES&{Ids("setorIds", 101)}");
        var pessoasDemais = await rh.GetAsync($"{Rota}/publico/contagem?publico=PESSOAS&{Ids("funcionarioIds", 501)}");
        var noLimite = await rh.GetAsync($"{Rota}/publico/contagem?publico=PESSOAS&{Ids("funcionarioIds", 500)}");
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync($"{Rota}/publico/contagem?publico=TODOS");
        var semLogin = await api.Api.ClienteAnonimo().GetAsync($"{Rota}/publico/contagem?publico=TODOS");

        Assert.Equal([400, 400, 400, 400, 200, 403, 401], new[] { semPublico, invalido, setoresDemais, pessoasDemais, noLimite, gestor, semLogin }.Select(r => (int)r.StatusCode));
        Assert.True((await ErrosAsync(semPublico)).TryGetProperty("publico", out _));
        Assert.True((await ErrosAsync(setoresDemais)).TryGetProperty("setorIds", out _));
        Assert.True((await ErrosAsync(pessoasDemais)).TryGetProperty("funcionarioIds", out _));
    }
}
