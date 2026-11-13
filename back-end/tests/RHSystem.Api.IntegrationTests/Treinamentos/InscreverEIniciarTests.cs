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
/// <c>inscreverMeEmTreinamento</c> e <c>iniciarMeuTreinamento</c>, no banco REAL com o relógio em 2026-10-07T12:42Z. Inscrever só vale para treinamento OPCIONAL do público da pessoa, publicado e liberado (obrigatório = 422, fora do público = 404, encerrado = 422); repetir devolve o treinamento sem mudar nada e duas telas ao mesmo tempo deixam uma linha só. Iniciar passa <c>INSCRITO</c> a
/// <c>EM_ANDAMENTO</c> com a data do RELÓGIO da aplicação por <c>UPDATE</c> condicional; repetir não toca no banco e a linha <c>CONCLUIDO</c> nunca é mexida.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class InscreverEIniciarTests(MySqlFixture mysql)
{
    private static string Rota(int treinamento, string acao) => $"/api/eu/treinamentos/{treinamento}/{acao}";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(InscreverEIniciarTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(int Id, HttpClient Cliente)> PessoaAsync(ApiDeAcesso api, string nome, int? setor = null, Perfil perfil = Perfil.Funcionario)
    {
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), perfil, setor, true, new DateOnly(2026, 1, 2));
        return (id, await api.ClienteAgoraAsync(await AdvertenciasDeTeste.ComLoginAsync(api, id)));
    }

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")})");

    private static Task<string?> DadosDaLinhaAsync(ApiDeAcesso api, int treinamento, int pessoa) =>
        TextoAsync(api, $"SELECT CONCAT(STATUS_TREINAMENTO, '|', IFNULL(DATE_FORMAT(DATA_INICIO, '%Y-%m-%d'), '-'), '|', IFNULL(DATE_FORMAT(DATA_CONCLUSAO, '%Y-%m-%d'), '-')) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static Task<long> LinhasAsync(ApiDeAcesso api, int treinamento, int pessoa) => ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static Task<int> OpcionalAsync(ApiDeAcesso api, string titulo, string situacao = "EM_ANDAMENTO", string publico = "TODOS", bool obrigatorio = false) => SemearAsync(api, titulo, situacao, obrigatorio, publico);

    private static async Task PublicoDoSetorAsync(ApiDeAcesso api, int treinamento, int setor) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({treinamento}, {setor})");

    private static async Task PublicoDaPessoaAsync(ApiDeAcesso api, int treinamento, int pessoa) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO) VALUES ({treinamento}, {pessoa})");

    // ---------------------------------------------------------------- inscrever

    [Fact]
    public async Task Inscrever_em_opcional_de_todos_do_setor_ou_da_pessoa_cria_a_linha_INSCRITO_sem_datas_e_devolve_o_detalhe()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZIns1 setor"));
        var (eu, cliente) = await PessoaAsync(api, "ZIns1 Ana", setor);
        var deTodos = await OpcionalAsync(api, Titulo("ZIns1 todos"));
        var doSetor = await OpcionalAsync(api, Titulo("ZIns1 setor"), publico: "SETORES");
        await PublicoDoSetorAsync(api, doSetor, setor);
        var daPessoa = await OpcionalAsync(api, Titulo("ZIns1 pessoa"), publico: "PESSOAS");
        await PublicoDaPessoaAsync(api, daPessoa, eu);
        _ = await ModuloAsync(api, deTodos, 1, "VIDEO", duracao: 40);
        _ = await ModuloAsync(api, deTodos, 2, "TEXTO", duracao: 20);

        var respostas = new[] { await cliente.PostAsync(Rota(deTodos, "inscricao"), null), await cliente.PostAsync(Rota(doSetor, "inscricao"), null), await cliente.PostAsync(Rota(daPessoa, "inscricao"), null) };

        Assert.Equal([200, 200, 200], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("inscreverMeEmTreinamento");
        var corpo = await CorpoAsync(respostas[0]);
        Assert.Equal(("NAO_INICIADO", 2, 60, 0, 1, JsonValueKind.Null), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("totalModulos").GetInt32(), corpo.GetProperty("duracaoTotalMinutos").GetInt32(), corpo.GetProperty("modulosConcluidos").GetInt32(), corpo.GetProperty("moduloAtual").GetInt32(), corpo.GetProperty("iniciadoEm").ValueKind));
        foreach (var id in new[] { deTodos, doSetor, daPessoa })
        {
            Assert.Equal("INSCRITO|-|-", await DadosDaLinhaAsync(api, id, eu));
        }

        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE NOTA IS NOT NULL")); // a API nunca grava nota
    }

    [Fact]
    public async Task Inscrever_de_novo_devolve_o_treinamento_sem_mudar_nada_e_nunca_toca_na_linha_em_andamento_ou_concluida()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZIns2 Ana");
        var inscrito = await OpcionalAsync(api, Titulo("ZIns2 inscrito"));
        var andamento = await OpcionalAsync(api, Titulo("ZIns2 andamento"));
        var concluido = await OpcionalAsync(api, Titulo("ZIns2 concluido"));
        await LinhaAsync(api, inscrito, eu, "INSCRITO");
        await LinhaAsync(api, andamento, eu, "EM_ANDAMENTO", "2026-10-02");
        await LinhaAsync(api, concluido, eu, "CONCLUIDO");
        var antes = new[] { await DadosDaLinhaAsync(api, inscrito, eu), await DadosDaLinhaAsync(api, andamento, eu), await DadosDaLinhaAsync(api, concluido, eu) };

        var respostas = new[] { await cliente.PostAsync(Rota(inscrito, "inscricao"), null), await cliente.PostAsync(Rota(andamento, "inscricao"), null), await cliente.PostAsync(Rota(concluido, "inscricao"), null) };

        Assert.Equal([200, 200, 200], respostas.Select(r => (int)r.StatusCode));
        var situacoes = new List<string?>();
        foreach (var r in respostas)
        {
            situacoes.Add((await CorpoAsync(r)).GetProperty("situacao").GetString());
        }

        var depois = new List<string?> { await DadosDaLinhaAsync(api, inscrito, eu), await DadosDaLinhaAsync(api, andamento, eu), await DadosDaLinhaAsync(api, concluido, eu) };
        Assert.Equal(["NAO_INICIADO", "EM_ANDAMENTO", "CONCLUIDO"], situacoes);
        Assert.Equal(antes, depois);
        Assert.Equal(1L, await LinhasAsync(api, inscrito, eu));
    }

    [Fact]
    public async Task Inscrever_obrigatorio_e_422_e_fora_do_publico_ou_nao_visivel_e_404()
    {
        var api = await ApiAsync();
        var meuSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZIns3 meu"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZIns3 outro"));
        var (eu, cliente) = await PessoaAsync(api, "ZIns3 Ana", meuSetor);
        var obrigatorioComLinha = await OpcionalAsync(api, Titulo("ZIns3 obrigatorio com linha"), obrigatorio: true);
        await LinhaAsync(api, obrigatorioComLinha, eu, "INSCRITO");
        var obrigatorioDoMeuPublico = await OpcionalAsync(api, Titulo("ZIns3 obrigatorio publico"), obrigatorio: true);
        var obrigatorioDeOutroSetor = await OpcionalAsync(api, Titulo("ZIns3 obrigatorio outro"), publico: "SETORES", obrigatorio: true);
        await PublicoDoSetorAsync(api, obrigatorioDeOutroSetor, outroSetor);
        var opcionalDeOutroSetor = await OpcionalAsync(api, Titulo("ZIns3 opcional outro"), publico: "SETORES");
        await PublicoDoSetorAsync(api, opcionalDeOutroSetor, outroSetor);
        var rascunho = await SemearAsync(api, Titulo("ZIns3 rascunho"));
        var agendado = await OpcionalAsync(api, Titulo("ZIns3 agendado"), situacao: "AGENDADO");
        var reprovado = await OpcionalAsync(api, Titulo("ZIns3 reprovado"));
        await LinhaAsync(api, reprovado, eu, "REPROVADO");

        var respostas = new Dictionary<int, HttpResponseMessage>();
        foreach (var id in new[] { obrigatorioComLinha, obrigatorioDoMeuPublico, obrigatorioDeOutroSetor, opcionalDeOutroSetor, rascunho, agendado, reprovado, 987654 })
        {
            respostas[id] = await cliente.PostAsync(Rota(id, "inscricao"), null);
        }

        Assert.Equal([422, 422, 404, 404, 404, 404, 404, 404], new[] { obrigatorioComLinha, obrigatorioDoMeuPublico, obrigatorioDeOutroSetor, opcionalDeOutroSetor, rascunho, agendado, reprovado, 987654 }.Select(i => (int)respostas[i].StatusCode));
        await respostas[obrigatorioComLinha].AfirmarConformeAoContratoAsync("inscreverMeEmTreinamento");
        var corpo = await CorpoAsync(respostas[obrigatorioDoMeuPublico]);
        Assert.Equal(("TREINAMENTO_NAO_E_OPCIONAL", "Este treinamento é obrigatório e não precisa de inscrição."), (Codigo(corpo), corpo.GetProperty("detail").GetString()));
        Assert.Equal(0L, await LinhasAsync(api, obrigatorioDoMeuPublico, eu) + await LinhasAsync(api, opcionalDeOutroSetor, eu) + await LinhasAsync(api, rascunho, eu) + await LinhasAsync(api, agendado, eu));
    }

    [Fact]
    public async Task Inscrever_em_treinamento_encerrado_e_422_e_so_quem_ja_comecou_ou_concluiu_recebe_o_detalhe()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZIns4 Ana");
        var semLinha = await OpcionalAsync(api, Titulo("ZIns4 sem linha"), situacao: "ENCERRADO");
        var inscrito = await OpcionalAsync(api, Titulo("ZIns4 inscrito"), situacao: "ENCERRADO");
        await LinhaAsync(api, inscrito, eu, "INSCRITO");
        var andamento = await OpcionalAsync(api, Titulo("ZIns4 andamento"), situacao: "ENCERRADO");
        await LinhaAsync(api, andamento, eu, "EM_ANDAMENTO", "2026-09-10");

        var respostas = new[] { await cliente.PostAsync(Rota(semLinha, "inscricao"), null), await cliente.PostAsync(Rota(inscrito, "inscricao"), null), await cliente.PostAsync(Rota(andamento, "inscricao"), null) };

        Assert.Equal([422, 422, 200], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("inscreverMeEmTreinamento");
        var corpo = await CorpoAsync(respostas[0]);
        Assert.Equal(("TREINAMENTO_ENCERRADO", "Treinamento encerrado.", "O RH encerrou este treinamento. Fale com o RH se precisar dele."), (Codigo(corpo), corpo.GetProperty("title").GetString(), corpo.GetProperty("detail").GetString()));
        Assert.Equal(0L, await LinhasAsync(api, semLinha, eu));
        Assert.Equal(("EM_ANDAMENTO", true), ((await CorpoAsync(respostas[2])).GetProperty("situacao").GetString(), (await CorpoAsync(respostas[2])).GetProperty("encerrado").GetBoolean()));
    }

    [Fact]
    public async Task Duas_telas_inscrevendo_ao_mesmo_tempo_deixam_uma_linha_e_dois_200()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZIns5 Ana");
        var id = await OpcionalAsync(api, Titulo("ZIns5 paralelo"));

        var respostas = await Task.WhenAll(cliente.PostAsync(Rota(id, "inscricao"), null), cliente.PostAsync(Rota(id, "inscricao"), null), cliente.PostAsync(Rota(id, "inscricao"), null));

        Assert.Equal([200, 200, 200], respostas.Select(r => (int)r.StatusCode));
        Assert.Equal(1L, await LinhasAsync(api, id, eu));
    }

    // ---------------------------------------------------------------- iniciar

    [Fact]
    public async Task Iniciar_passa_INSCRITO_a_EM_ANDAMENTO_com_a_data_do_relogio_e_repetir_nao_toca_no_banco()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZIni1 Ana");
        var id = await SemearAsync(api, Titulo("ZIni1"), "EM_ANDAMENTO", obrigatorio: true);
        _ = await ModuloAsync(api, id, 1);
        await LinhaAsync(api, id, eu, "INSCRITO");

        var primeira = await cliente.PostAsync(Rota(id, "iniciar"), null);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(3)); // outro dia: se a repetição gravasse de novo a data mudaria
        var repetida = await (await api.ClienteAgoraAsync(await LoginAsync(api, eu))).PostAsync(Rota(id, "iniciar"), null);

        Assert.Equal([200, 200], new[] { primeira, repetida }.Select(r => (int)r.StatusCode));
        await primeira.AfirmarConformeAoContratoAsync("iniciarMeuTreinamento");
        var corpo = await CorpoAsync(primeira);
        Assert.Equal(("EM_ANDAMENTO", "2026-10-07", 0, 1), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("iniciadoEm").GetString(), corpo.GetProperty("modulosConcluidos").GetInt32(), corpo.GetProperty("moduloAtual").GetInt32()));
        Assert.Equal("2026-10-07", (await CorpoAsync(repetida)).GetProperty("iniciadoEm").GetString());
        Assert.Equal("EM_ANDAMENTO|2026-10-07|-", await DadosDaLinhaAsync(api, id, eu)); // a data é a do relógio, não a do banco, e não mudou
    }

    private static async Task<int> LoginAsync(ApiDeAcesso api, int funcionario) => (int)await ContarAsync(api, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionario}");

    [Fact]
    public async Task Iniciar_opcional_sem_inscricao_e_422_obrigatorio_sem_linha_e_fora_do_publico_sao_404()
    {
        var api = await ApiAsync();
        var meuSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZIni2 meu"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZIni2 outro"));
        var (eu, cliente) = await PessoaAsync(api, "ZIni2 Ana", meuSetor);
        var opcional = await OpcionalAsync(api, Titulo("ZIni2 opcional"));
        var obrigatorioSemLinha = await OpcionalAsync(api, Titulo("ZIni2 obrigatorio"), obrigatorio: true);
        var deOutroSetor = await OpcionalAsync(api, Titulo("ZIni2 outro setor"), publico: "SETORES");
        await PublicoDoSetorAsync(api, deOutroSetor, outroSetor);
        var rascunho = await SemearAsync(api, Titulo("ZIni2 rascunho"));
        var agendado = await OpcionalAsync(api, Titulo("ZIni2 agendado"), situacao: "AGENDADO");
        var reprovado = await OpcionalAsync(api, Titulo("ZIni2 reprovado"));
        await LinhaAsync(api, reprovado, eu, "REPROVADO");

        var ids = new[] { opcional, obrigatorioSemLinha, deOutroSetor, rascunho, agendado, reprovado, 987654 };
        var respostas = new List<HttpResponseMessage>();
        foreach (var id in ids)
        {
            respostas.Add(await cliente.PostAsync(Rota(id, "iniciar"), null));
        }

        Assert.Equal([422, 404, 404, 404, 404, 404, 404], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("iniciarMeuTreinamento");
        var corpo = await CorpoAsync(respostas[0]);
        Assert.Equal(("INSCRICAO_PENDENTE", "Inscreva-se no treinamento antes de começar."), (Codigo(corpo), corpo.GetProperty("detail").GetString()));
        Assert.Equal(0L, await LinhasAsync(api, opcional, eu) + await LinhasAsync(api, obrigatorioSemLinha, eu)); // iniciar nunca cria linha
    }

    [Fact]
    public async Task Iniciar_no_encerrado_e_422_para_quem_nao_tinha_comecado_e_quem_ja_estava_em_andamento_ou_concluiu_so_recebe_o_estado()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZIni3 Ana");
        var inscrito = await SemearAsync(api, Titulo("ZIni3 inscrito"), "ENCERRADO", obrigatorio: true);
        var semLinha = await OpcionalAsync(api, Titulo("ZIni3 sem linha"), situacao: "ENCERRADO");
        var andamento = await SemearAsync(api, Titulo("ZIni3 andamento"), "ENCERRADO", obrigatorio: true);
        var concluido = await SemearAsync(api, Titulo("ZIni3 concluido"), "ENCERRADO", obrigatorio: true);
        await LinhaAsync(api, inscrito, eu, "INSCRITO");
        await LinhaAsync(api, andamento, eu, "EM_ANDAMENTO", "2026-09-10");
        await LinhaAsync(api, concluido, eu, "CONCLUIDO", "2026-09-11");
        var antes = new[] { await DadosDaLinhaAsync(api, inscrito, eu), await DadosDaLinhaAsync(api, andamento, eu), await DadosDaLinhaAsync(api, concluido, eu) };

        var respostas = new[] { await cliente.PostAsync(Rota(inscrito, "iniciar"), null), await cliente.PostAsync(Rota(semLinha, "iniciar"), null), await cliente.PostAsync(Rota(andamento, "iniciar"), null), await cliente.PostAsync(Rota(concluido, "iniciar"), null) };

        Assert.Equal([422, 422, 200, 200], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("iniciarMeuTreinamento");
        Assert.Equal("TREINAMENTO_ENCERRADO", Codigo(await CorpoAsync(respostas[0])));
        var situacoes = new List<string?> { (await CorpoAsync(respostas[2])).GetProperty("situacao").GetString(), (await CorpoAsync(respostas[3])).GetProperty("situacao").GetString() };
        var depois = new List<string?> { await DadosDaLinhaAsync(api, inscrito, eu), await DadosDaLinhaAsync(api, andamento, eu), await DadosDaLinhaAsync(api, concluido, eu) };
        Assert.Equal(["EM_ANDAMENTO", "CONCLUIDO"], situacoes);
        Assert.Equal(antes, depois); // nada mudou; o concluído nunca é tocado
    }

    [Fact]
    public async Task Iniciar_duas_vezes_ao_mesmo_tempo_da_dois_200_e_uma_linha_em_andamento()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZIni4 Ana");
        var id = await SemearAsync(api, Titulo("ZIni4"), "EM_ANDAMENTO", obrigatorio: true);
        await LinhaAsync(api, id, eu, "INSCRITO");

        var respostas = await Task.WhenAll(cliente.PostAsync(Rota(id, "iniciar"), null), cliente.PostAsync(Rota(id, "iniciar"), null), cliente.PostAsync(Rota(id, "iniciar"), null));

        Assert.Equal([200, 200, 200], respostas.Select(r => (int)r.StatusCode));
        Assert.Equal("EM_ANDAMENTO|2026-10-07|-", await DadosDaLinhaAsync(api, id, eu));
        Assert.Equal(1L, await LinhasAsync(api, id, eu));
    }

    [Fact]
    public async Task O_gestor_e_o_rh_usam_as_mesmas_rotas_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var (gestor, clienteDoGestor) = await PessoaAsync(api, "ZIni5 Gestor", perfil: Perfil.Gestor);
        var id = await OpcionalAsync(api, Titulo("ZIni5"));

        var inscrito = await clienteDoGestor.PostAsync(Rota(id, "inscricao"), null);
        var iniciado = await clienteDoGestor.PostAsync(Rota(id, "iniciar"), null);
        var semLogin = new[] { await api.Api.ClienteAnonimo().PostAsync(Rota(id, "inscricao"), null), await api.Api.ClienteAnonimo().PostAsync(Rota(id, "iniciar"), null) };

        Assert.Equal([200, 200], new[] { inscrito, iniciado }.Select(r => (int)r.StatusCode));
        Assert.Equal([401, 401], semLogin.Select(r => (int)r.StatusCode));
        Assert.Equal("EM_ANDAMENTO|2026-10-07|-", await DadosDaLinhaAsync(api, id, gestor));
    }
}
