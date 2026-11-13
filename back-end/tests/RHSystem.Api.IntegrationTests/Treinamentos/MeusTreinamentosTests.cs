using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>listarMeusTreinamentos</c> e <c>obterMeuTreinamento</c>, no banco REAL com o relógio em 2026-10-07T12:42Z. A pessoa só vê publicados já liberados em que tem linha (<c>INSCRITO</c>, <c>EM_ANDAMENTO</c>, <c>CONCLUIDO</c>) ou que são opcionais do público dela (<c>DISPONIVEL</c>); o encerrado some para quem não começou e continua para quem começou ou concluiu; linha <c>REPROVADO</c> não aparece.
/// Cada teste cria as SUAS pessoas (com login), então a lista de uma não depende das outras. Gestor e RH usam a mesma rota; a pessoa nunca vê a linha, o progresso nem o certificado de outra.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MeusTreinamentosTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/treinamentos";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MeusTreinamentosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    /// <summary>Uma pessoa nova do setor (ativa, admitida em janeiro) com login, e um cliente HTTP dela.</summary>
    private static async Task<(int Id, HttpClient Cliente)> PessoaAsync(ApiDeAcesso api, string nome, int? setor = null, Perfil perfil = Perfil.Funcionario)
    {
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), perfil, setor, true, new DateOnly(2026, 1, 2));
        var usuario = await AdvertenciasDeTeste.ComLoginAsync(api, id);
        return (id, await api.ClienteAgoraAsync(usuario));
    }

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")})");

    private static async Task ProgressoAsync(ApiDeAcesso api, int modulo, int pessoa, string instanteLocal) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({modulo}, {pessoa}, '{instanteLocal}')");

    private static async Task PublicoDoSetorAsync(ApiDeAcesso api, int treinamento, int setor) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({treinamento}, {setor})");

    private static async Task PublicoDaPessoaAsync(ApiDeAcesso api, int treinamento, int pessoa) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO) VALUES ({treinamento}, {pessoa})");

    private static async Task DataAsync(ApiDeAcesso api, int treinamento, string? dia) => await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = {(dia is null ? "NULL" : $"'{dia}'")} WHERE ID_TREINAMENTO = {treinamento}");

    /// <summary>A lista da pessoa, isolada pelo prefixo do título do teste (a busca): os opcionais "de todos" de outros testes valem para qualquer pessoa.</summary>
    private static async Task<JsonElement> ListarAsync(HttpClient cliente, string prefixo, string consulta = "") => await CorpoAsync(await cliente.GetAsync($"{Rota}?busca={prefixo}{(consulta.Length == 0 ? string.Empty : "&" + consulta)}"));

    private static int[] Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32())];

    private static (int, int, int, int) Contagens(JsonElement lista)
    {
        var c = lista.GetProperty("contagens");
        return (c.GetProperty("todos").GetInt32(), c.GetProperty("disponiveis").GetInt32(), c.GetProperty("emAndamento").GetInt32(), c.GetProperty("concluidos").GetInt32());
    }

    /// <summary>Um treinamento publicado e liberado (começou em 01/10), com a situação e o público pedidos.</summary>
    private static Task<int> EmAndamentoAsync(ApiDeAcesso api, string titulo, bool obrigatorio = false, string? publico = "TODOS", bool emite = true, string? descricao = null) =>
        SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio, publico, emite, descricao: descricao);

    [Fact]
    public async Task O_exemplo_do_contrato_2_disponivel_e_em_andamento_1_1_e_3_concluidos_com_o_cartao_completo()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZMeu1 setor"));
        var (eu, cliente) = await PessoaAsync(api, "ZMeu1 Ana", setor);
        var seguranca = await EmAndamentoAsync(api, Titulo("ZMeu1 Segurança"), obrigatorio: true, descricao: "Senhas, golpes por e-mail e cuidado com dados.");
        var duracoes = new short[] { 20, 20, 20, 20, 20 };
        var modulos = new List<int>();
        for (var i = 0; i < duracoes.Length; i++)
        {
            modulos.Add(await ModuloAsync(api, seguranca, i + 1, duracao: duracoes[i]));
        }

        await LinhaAsync(api, seguranca, eu, "EM_ANDAMENTO", "2026-10-05");
        foreach (var (modulo, instante) in new[] { (modulos[0], "2026-10-05 10:00:00"), (modulos[1], "2026-10-05 11:00:00"), (modulos[2], "2026-10-06 09:30:00") })
        {
            await ProgressoAsync(api, modulo, eu, instante);
        }

        var excel = await EmAndamentoAsync(api, Titulo("ZMeu1 Excel"), obrigatorio: false, publico: "SETORES", descricao: null);
        await PublicoDoSetorAsync(api, excel, setor);
        _ = await ModuloAsync(api, excel, 1, duracao: 30);
        _ = await ModuloAsync(api, excel, 2, duracao: null);
        for (var i = 0; i < 3; i++)
        {
            var feito = await EmAndamentoAsync(api, Titulo("ZMeu1 feito"));
            _ = await ModuloAsync(api, feito, 1);
            await LinhaAsync(api, feito, eu, "CONCLUIDO");
        }

        var resposta = await cliente.GetAsync($"{Rota}?busca=ZMeu1");
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarMeusTreinamentos");
        Assert.Equal((2, 1, 1, 3), Contagens(lista));
        Assert.Equal([seguranca, excel], Ids(lista)); // obrigatório primeiro
        var primeiro = lista.GetProperty("itens")[0];
        Assert.Equal(("EM_ANDAMENTO", 5, 100, 3, 60, 4, "2026-10-05", false, true), (primeiro.GetProperty("situacao").GetString(), primeiro.GetProperty("totalModulos").GetInt32(), primeiro.GetProperty("duracaoTotalMinutos").GetInt32(), primeiro.GetProperty("modulosConcluidos").GetInt32(), primeiro.GetProperty("progressoPercentual").GetInt32(), primeiro.GetProperty("moduloAtual").GetInt32(), primeiro.GetProperty("iniciadoEm").GetString(), primeiro.GetProperty("encerrado").GetBoolean(), primeiro.GetProperty("obrigatorio").GetBoolean()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, "Senhas, golpes por e-mail e cuidado com dados."), (primeiro.GetProperty("concluidoEmUtc").ValueKind, primeiro.GetProperty("certificado").ValueKind, primeiro.GetProperty("descricao").GetString()));
        var segundo = lista.GetProperty("itens")[1];
        Assert.Equal(("DISPONIVEL", 2, 30, 0, 0, 1, JsonValueKind.Null, JsonValueKind.Null), (segundo.GetProperty("situacao").GetString(), segundo.GetProperty("totalModulos").GetInt32(), segundo.GetProperty("duracaoTotalMinutos").GetInt32(), segundo.GetProperty("modulosConcluidos").GetInt32(), segundo.GetProperty("progressoPercentual").GetInt32(), segundo.GetProperty("moduloAtual").GetInt32(), segundo.GetProperty("iniciadoEm").ValueKind, segundo.GetProperty("descricao").ValueKind));
    }

    [Fact]
    public async Task Cada_aba_traz_so_o_que_e_dela_e_a_padrao_nao_inclui_os_concluidos()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZMeu2 Ana");
        var disponivel = await EmAndamentoAsync(api, Titulo("ZMeu2 disponivel"));
        var naoIniciado = await EmAndamentoAsync(api, Titulo("ZMeu2 nao iniciado"), obrigatorio: true);
        var andamento = await EmAndamentoAsync(api, Titulo("ZMeu2 andamento"), obrigatorio: true);
        var concluido = await EmAndamentoAsync(api, Titulo("ZMeu2 concluido"));
        await LinhaAsync(api, naoIniciado, eu, "INSCRITO");
        await LinhaAsync(api, andamento, eu, "EM_ANDAMENTO", "2026-10-02");
        await LinhaAsync(api, concluido, eu, "CONCLUIDO");

        var todos = await ListarAsync(cliente, "ZMeu2");
        var explicito = await ListarAsync(cliente, "ZMeu2", "aba=TODOS");
        var disponiveis = await ListarAsync(cliente, "ZMeu2", "aba=DISPONIVEIS");
        var emAndamento = await ListarAsync(cliente, "ZMeu2", "aba=EM_ANDAMENTO");
        var concluidos = await ListarAsync(cliente, "ZMeu2", "aba=CONCLUIDOS");

        Assert.Equal([andamento, naoIniciado, disponivel], Ids(todos)); // obrigatórios primeiro (mesmo comecaEm: pelo título); o opcional por último
        Assert.Equal(Ids(todos), Ids(explicito));
        Assert.Equal([naoIniciado, disponivel], Ids(disponiveis));
        Assert.Equal([andamento], Ids(emAndamento));
        Assert.Equal([concluido], Ids(concluidos));
        Assert.All(new[] { todos, explicito, disponiveis, emAndamento, concluidos }, lista => Assert.Equal((3, 2, 1, 1), Contagens(lista))); // as contagens ignoram a aba
        Assert.Equal(["NAO_INICIADO", "DISPONIVEL"], disponiveis.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("situacao").GetString()!));
    }

    [Fact]
    public async Task Opcional_do_setor_ou_da_pessoa_ou_de_todos_e_disponivel_e_de_outro_setor_ou_obrigatorio_sem_linha_e_404()
    {
        var api = await ApiAsync();
        var meuSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZMeu3 meu"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZMeu3 outro"));
        var (eu, cliente) = await PessoaAsync(api, "ZMeu3 Ana", meuSetor);
        var (outra, _) = await PessoaAsync(api, "ZMeu3 Bia", meuSetor);
        var doMeuSetor = await EmAndamentoAsync(api, Titulo("ZMeu3 meu setor"), publico: "SETORES");
        await PublicoDoSetorAsync(api, doMeuSetor, meuSetor);
        var deOutroSetor = await EmAndamentoAsync(api, Titulo("ZMeu3 outro setor"), publico: "SETORES");
        await PublicoDoSetorAsync(api, deOutroSetor, outroSetor);
        var deTodos = await EmAndamentoAsync(api, Titulo("ZMeu3 todos"), publico: "TODOS");
        var minhaPessoa = await EmAndamentoAsync(api, Titulo("ZMeu3 pessoas eu"), publico: "PESSOAS");
        await PublicoDaPessoaAsync(api, minhaPessoa, eu);
        var deOutraPessoa = await EmAndamentoAsync(api, Titulo("ZMeu3 pessoas outra"), publico: "PESSOAS");
        await PublicoDaPessoaAsync(api, deOutraPessoa, outra);
        var obrigatorioSemLinha = await EmAndamentoAsync(api, Titulo("ZMeu3 obrigatorio"), obrigatorio: true, publico: "TODOS");

        var lista = await ListarAsync(cliente, "ZMeu3");
        var respostas = new Dictionary<int, int>();
        foreach (var id in new[] { doMeuSetor, deOutroSetor, deTodos, minhaPessoa, deOutraPessoa, obrigatorioSemLinha })
        {
            respostas[id] = (int)(await cliente.GetAsync($"{Rota}/{id}")).StatusCode;
        }

        Assert.Equal([doMeuSetor, deTodos, minhaPessoa], Ids(lista).Order());
        Assert.Equal((200, 404, 200, 200, 404, 404), (respostas[doMeuSetor], respostas[deOutroSetor], respostas[deTodos], respostas[minhaPessoa], respostas[deOutraPessoa], respostas[obrigatorioSemLinha]));
        var detalhe = await cliente.GetAsync($"{Rota}/{doMeuSetor}");
        await detalhe.AfirmarConformeAoContratoAsync("obterMeuTreinamento");
        Assert.Equal("DISPONIVEL", (await CorpoAsync(detalhe)).GetProperty("situacao").GetString());
    }

    [Fact]
    public async Task Rascunho_agendado_encerrado_sem_comecar_e_linhas_reprovadas_ficam_fora_e_o_encerrado_de_quem_comecou_ou_concluiu_continua()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZMeu4 Ana");
        var rascunho = await SemearAsync(api, Titulo("ZMeu4 rascunho"), obrigatorio: true);
        await LinhaAsync(api, rascunho, eu, "INSCRITO");
        var agendado = await SemearAsync(api, Titulo("ZMeu4 agendado"), "AGENDADO", obrigatorio: true);
        await LinhaAsync(api, agendado, eu, "INSCRITO");
        var amanha = await EmAndamentoAsync(api, Titulo("ZMeu4 amanha"), obrigatorio: true);
        await DataAsync(api, amanha, "2026-10-08");
        await LinhaAsync(api, amanha, eu, "INSCRITO");
        var hoje = await EmAndamentoAsync(api, Titulo("ZMeu4 hoje"), obrigatorio: true);
        await DataAsync(api, hoje, "2026-10-07");
        await LinhaAsync(api, hoje, eu, "INSCRITO");
        var semData = await EmAndamentoAsync(api, Titulo("ZMeu4 sem data"), obrigatorio: true);
        await DataAsync(api, semData, null);
        await LinhaAsync(api, semData, eu, "INSCRITO");
        var encerradoSemComecar = await SemearAsync(api, Titulo("ZMeu4 encerrado inscrito"), "ENCERRADO", obrigatorio: true);
        await LinhaAsync(api, encerradoSemComecar, eu, "INSCRITO");
        var encerradoOpcional = await SemearAsync(api, Titulo("ZMeu4 encerrado opcional"), "ENCERRADO", publico: "TODOS");
        var encerradoComecou = await SemearAsync(api, Titulo("ZMeu4 encerrado andamento"), "ENCERRADO", obrigatorio: true);
        await LinhaAsync(api, encerradoComecou, eu, "EM_ANDAMENTO", "2026-09-10");
        var encerradoConcluido = await SemearAsync(api, Titulo("ZMeu4 encerrado concluido"), "ENCERRADO", obrigatorio: true);
        await LinhaAsync(api, encerradoConcluido, eu, "CONCLUIDO");
        var reprovado = await EmAndamentoAsync(api, Titulo("ZMeu4 reprovado"), obrigatorio: true);
        await LinhaAsync(api, reprovado, eu, "REPROVADO");
        var cancelado = await EmAndamentoAsync(api, Titulo("ZMeu4 cancelado"), obrigatorio: true);
        await LinhaAsync(api, cancelado, eu, "CANCELADO");

        var todos = await ListarAsync(cliente, "ZMeu4");
        var concluidos = await ListarAsync(cliente, "ZMeu4", "aba=CONCLUIDOS");
        var codigos = new Dictionary<int, int>();
        foreach (var id in new[] { rascunho, agendado, amanha, encerradoSemComecar, encerradoOpcional, reprovado, cancelado, encerradoComecou, hoje, semData })
        {
            codigos[id] = (int)(await cliente.GetAsync($"{Rota}/{id}")).StatusCode;
        }

        Assert.Equal(new[] { hoje, semData, encerradoComecou }.Order(), Ids(todos).Order()); // só estes três (os de situação "fora" não aparecem)
        Assert.Equal([encerradoConcluido], Ids(concluidos));
        Assert.Equal((3, 2, 1, 1), Contagens(todos)); // reprovado e cancelado não entram em nenhuma contagem
        Assert.Equal([404, 404, 404, 404, 404, 404, 404, 200, 200, 200], new[] { rascunho, agendado, amanha, encerradoSemComecar, encerradoOpcional, reprovado, cancelado, encerradoComecou, hoje, semData }.Select(i => codigos[i]));
        var comecou = (await ListarAsync(cliente, "ZMeu4", "aba=EM_ANDAMENTO")).GetProperty("itens")[0];
        Assert.Equal((encerradoComecou, true, "EM_ANDAMENTO"), (comecou.GetProperty("id").GetInt32(), comecou.GetProperty("encerrado").GetBoolean(), comecou.GetProperty("situacao").GetString()));
        var concluiu = (await CorpoAsync(await cliente.GetAsync($"{Rota}/{encerradoConcluido}")));
        Assert.Equal((true, "CONCLUIDO"), (concluiu.GetProperty("encerrado").GetBoolean(), concluiu.GetProperty("situacao").GetString()));
    }

    [Fact]
    public async Task A_busca_olha_titulo_e_descricao_o_filtro_obrigatorio_separa_e_as_contagens_respeitam_os_dois_mas_nao_a_aba()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZMeu5 Ana");
        var p = Titulo("ZMeu5");
        var pelaDescricao = await EmAndamentoAsync(api, $"{p} alfa", obrigatorio: true, descricao: "fala de ergonomia");
        var pelaDescricaoOpcional = await EmAndamentoAsync(api, $"{p} beta", obrigatorio: false, descricao: "outra ERGONOMIA");
        var pelaDescricaoConcluido = await EmAndamentoAsync(api, $"{p} gama", obrigatorio: true, descricao: "ergonomia de novo");
        var semRelacao = await EmAndamentoAsync(api, $"{p} delta", obrigatorio: true, descricao: "segurança");
        foreach (var id in new[] { pelaDescricao, semRelacao })
        {
            await LinhaAsync(api, id, eu, "INSCRITO");
        }

        await LinhaAsync(api, pelaDescricaoConcluido, eu, "CONCLUIDO");

        var porDescricao = await ListarAsync(cliente, "ergonomia");
        var soObrigatorios = await ListarAsync(cliente, "ergonomia", "obrigatorio=true");
        var soOpcionais = await ListarAsync(cliente, "ergonomia", "obrigatorio=false");
        var porTitulo = await ListarAsync(cliente, $"{p.ToLowerInvariant()}%20DELTA");
        var abaConcluidos = await ListarAsync(cliente, "ergonomia", "aba=CONCLUIDOS");

        Assert.Equal([pelaDescricao, pelaDescricaoOpcional], Ids(porDescricao));
        Assert.Equal((2, 2, 0, 1), Contagens(porDescricao)); // alfa (não iniciado) e beta (disponível) = todos e disponíveis; gama concluído
        Assert.Equal([pelaDescricao], Ids(soObrigatorios));
        Assert.Equal((1, 1, 0, 1), Contagens(soObrigatorios)); // o obrigatório não iniciado e o concluído
        Assert.Equal([pelaDescricaoOpcional], Ids(soOpcionais));
        Assert.Equal([semRelacao], Ids(porTitulo));
        Assert.Equal([pelaDescricaoConcluido], Ids(abaConcluidos));
        Assert.Equal((2, 2, 0, 1), Contagens(abaConcluidos)); // a aba não muda as contagens
    }

    [Fact]
    public async Task A_ordem_e_obrigatorios_primeiro_depois_o_comeca_em_mais_antigo_o_sem_data_por_ultimo_e_o_titulo_e_a_pagina_corta_a_lista()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZMeu6 Ana");
        var opcionalAntigo = await EmAndamentoAsync(api, Titulo("ZMeu6 A opcional antigo"), obrigatorio: false);
        await DataAsync(api, opcionalAntigo, "2026-01-01");
        var obrigNovo = await EmAndamentoAsync(api, Titulo("ZMeu6 B obrigatorio novo"), obrigatorio: true);
        await DataAsync(api, obrigNovo, "2026-09-01");
        var obrigAntigoB = await EmAndamentoAsync(api, Titulo("ZMeu6 C obrigatorio antigo"), obrigatorio: true);
        await DataAsync(api, obrigAntigoB, "2026-08-01");
        var obrigAntigoA = await EmAndamentoAsync(api, Titulo("ZMeu6 AA obrigatorio antigo"), obrigatorio: true);
        await DataAsync(api, obrigAntigoA, "2026-08-01");
        var obrigSemData = await EmAndamentoAsync(api, Titulo("ZMeu6 D obrigatorio sem data"), obrigatorio: true);
        await DataAsync(api, obrigSemData, null);
        foreach (var id in new[] { obrigNovo, obrigAntigoB, obrigAntigoA, obrigSemData })
        {
            await LinhaAsync(api, id, eu, "INSCRITO");
        }

        var tudo = await ListarAsync(cliente, "ZMeu6");
        var pagina2 = await ListarAsync(cliente, "ZMeu6", "tamanhoPagina=2&pagina=2");
        var alem = await ListarAsync(cliente, "ZMeu6", "tamanhoPagina=2&pagina=9");

        Assert.Equal([obrigAntigoA, obrigAntigoB, obrigNovo, obrigSemData, opcionalAntigo], Ids(tudo));
        Assert.Equal([obrigNovo, obrigSemData], Ids(pagina2));
        Assert.Equal((5, 3, 2, 2), (pagina2.GetProperty("total").GetInt32(), pagina2.GetProperty("totalPaginas").GetInt32(), pagina2.GetProperty("pagina").GetInt32(), pagina2.GetProperty("tamanhoPagina").GetInt32()));
        Assert.Equal((0, 5), (alem.GetProperty("itens").GetArrayLength(), alem.GetProperty("total").GetInt32()));
    }

    [Fact]
    public async Task O_detalhe_traz_os_modulos_sem_conteudo_o_progresso_so_dele_e_o_certificado_so_se_emite_e_com_codigo_v4()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZMeu7 Ana");
        var (outra, clienteDaOutra) = await PessoaAsync(api, "ZMeu7 Bia");
        var emite = await SemearAsync(api, Titulo("ZMeu7 emite"), "EM_ANDAMENTO", obrigatorio: true, publico: "TODOS", emite: true, onboarding: true);
        var m1 = await ModuloAsync(api, emite, 1, "VIDEO", duracao: 15);
        var m2 = await ModuloAsync(api, emite, 2, "TEXTO", duracao: 20);
        var m3 = await ModuloAsync(api, emite, 3, "PDF", duracao: null, caminho: "treinamentos/segredo.pdf", nomeArquivo: "apostila.pdf");
        await LinhaAsync(api, emite, eu, "CONCLUIDO", "2026-08-02");
        await LinhaAsync(api, emite, outra, "EM_ANDAMENTO", "2026-08-03");
        await ProgressoAsync(api, m1, eu, "2026-08-02 10:10:00");
        await ProgressoAsync(api, m2, eu, "2026-08-02 10:35:00");
        await ProgressoAsync(api, m3, eu, "2026-09-15 11:05:00");
        await ProgressoAsync(api, m1, outra, "2026-08-03 08:00:00");
        // a outra pessoa também concluiu este treinamento e tem o SEU certificado: o da Ana não pode se confundir com ele
        var terceira = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMeu7 Caio"), Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));
        await LinhaAsync(api, emite, terceira, "CONCLUIDO", "2026-08-03");
        const string codigoV4 = "a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b57";
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET CODIGO_VALIDACAO = '{codigoV4}', DATA_EMISSAO = '2026-09-15' WHERE ID_TREINAMENTO = {emite} AND ID_FUNCIONARIO = {eu}");
        var certificadoId = await ContarAsync(api, $"SELECT ID_CERTIFICADO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {emite} AND ID_FUNCIONARIO = {eu}");

        var resposta = await cliente.GetAsync($"{Rota}/{emite}");
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterMeuTreinamento");
        Assert.Equal(("CONCLUIDO", 3, 35, 3, 100, JsonValueKind.Null, "2026-08-02", "2026-09-15T14:05:00Z"), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("totalModulos").GetInt32(), corpo.GetProperty("duracaoTotalMinutos").GetInt32(), corpo.GetProperty("modulosConcluidos").GetInt32(), corpo.GetProperty("progressoPercentual").GetInt32(), corpo.GetProperty("moduloAtual").ValueKind, corpo.GetProperty("iniciadoEm").GetString(), corpo.GetProperty("concluidoEmUtc").GetString()));
        var certificado = corpo.GetProperty("certificado");
        Assert.Equal(($"CERT-2026-{certificadoId:00000}", codigoV4, "2026-09-15"), (certificado.GetProperty("numero").GetString(), certificado.GetProperty("codigoValidacao").GetString(), certificado.GetProperty("emitidoEm").GetString()));
        var modulos = corpo.GetProperty("modulos").EnumerateArray().ToList();
        Assert.Equal([m1, m2, m3], modulos.Select(m => m.GetProperty("id").GetInt32()));
        Assert.Equal(["2026-08-02T13:10:00Z", "2026-08-02T13:35:00Z", "2026-09-15T14:05:00Z"], modulos.Select(m => m.GetProperty("concluidoEmUtc").GetString()!)); // o banco guarda -03:00
        Assert.Equal([true, true, true], modulos.Select(m => m.GetProperty("concluido").GetBoolean()));
        Assert.True(corpo.GetProperty("onboarding").GetBoolean());
        Assert.All(modulos, m => Assert.Equal(["concluido", "concluidoEmUtc", "duracaoMinutos", "id", "ordem", "tipo", "titulo"], m.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal))); // nada de conteúdo, link nem caminho
        Assert.DoesNotContain("segredo.pdf", corpo.GetRawText(), StringComparison.Ordinal);

        // a outra pessoa vê só o progresso DELA e nenhum certificado
        var dela = await CorpoAsync(await clienteDaOutra.GetAsync($"{Rota}/{emite}"));
        Assert.Equal(("EM_ANDAMENTO", 1, 2, JsonValueKind.Null), (dela.GetProperty("situacao").GetString(), dela.GetProperty("modulosConcluidos").GetInt32(), dela.GetProperty("moduloAtual").GetInt32(), dela.GetProperty("certificado").ValueKind));
        Assert.Equal([true, false, false], dela.GetProperty("modulos").EnumerateArray().Select(m => m.GetProperty("concluido").GetBoolean()));
    }

    [Fact]
    public async Task Certificado_com_codigo_v1_ou_de_treinamento_que_nao_emite_nunca_sai_na_resposta()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZMeu8 Ana");
        var emite = await EmAndamentoAsync(api, Titulo("ZMeu8 emite"), emite: true);
        var naoEmite = await EmAndamentoAsync(api, Titulo("ZMeu8 nao emite"), emite: false);
        foreach (var id in new[] { emite, naoEmite })
        {
            _ = await ModuloAsync(api, id, 1);
            await LinhaAsync(api, id, eu, "CONCLUIDO");
        }

        // a linha do trigger tem código v1 (o do banco); enquanto a aplicação não o trocar, ele NUNCA aparece
        var v1 = await CorpoAsync(await cliente.GetAsync($"{Rota}/{emite}"));
        var semEmitir = await CorpoAsync(await cliente.GetAsync($"{Rota}/{naoEmite}"));
        var lista = await ListarAsync(cliente, "ZMeu8", "aba=CONCLUIDOS");

        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (v1.GetProperty("certificado").ValueKind, semEmitir.GetProperty("certificado").ValueKind));
        Assert.All(lista.GetProperty("itens").EnumerateArray(), item => Assert.Equal(JsonValueKind.Null, item.GetProperty("certificado").ValueKind));
        Assert.Equal(2, lista.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_pessoa_nunca_ve_a_linha_de_outra_gestor_e_rh_usam_a_mesma_rota_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var (outra, _) = await PessoaAsync(api, "ZMeu9 Bia");
        var (_, gestor) = await PessoaAsync(api, "ZMeu9 Gestor", perfil: Perfil.Gestor);
        var deOutra = await EmAndamentoAsync(api, Titulo("ZMeu9 so da outra"), obrigatorio: true);
        await LinhaAsync(api, deOutra, outra, "INSCRITO");

        var comoGestor = await gestor.GetAsync(Rota);
        var comoRh = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota);
        var detalheComoGestor = await gestor.GetAsync($"{Rota}/{deOutra}");
        var semLogin = await api.Api.ClienteAnonimo().GetAsync(Rota);
        var semLoginDetalhe = await api.Api.ClienteAnonimo().GetAsync($"{Rota}/{deOutra}");

        Assert.Equal([200, 200, 404, 401, 401], new[] { comoGestor, comoRh, detalheComoGestor, semLogin, semLoginDetalhe }.Select(r => (int)r.StatusCode));
        Assert.DoesNotContain(deOutra, Ids(await CorpoAsync(comoGestor)));
    }

    [Fact]
    public async Task Parametro_invalido_devolve_400_com_todos_os_campos_juntos_e_treinamento_inexistente_e_404()
    {
        var api = await ApiAsync();
        var (_, cliente) = await PessoaAsync(api, "ZMeu10 Ana");

        var resposta = await cliente.GetAsync($"{Rota}?aba=XPTO&pagina=0&tamanhoPagina=101");
        var erros = (await CorpoAsync(resposta)).GetProperty("errors");
        var inexistente = await cliente.GetAsync($"{Rota}/987654");

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarMeusTreinamentos");
        Assert.Equal(["aba", "pagina", "tamanhoPagina"], erros.EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal));
        Assert.Equal(404, (int)inexistente.StatusCode);
    }
}
