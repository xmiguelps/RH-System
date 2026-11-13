using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>listarTreinamentosDeOnboarding</c>, <c>incluirFuncionarioNoOnboarding</c> e a caixa "Incluir no treinamento" do cadastro (<c>IInclusaoNoOnboarding</c> real), no banco REAL com o relógio em 2026-10-07T12:42Z. Em vigor = publicado, marcado como onboarding e não encerrado (o agendado entra); a pessoa entra em TODOS, qualquer que seja o público; quem já tem linha não muda. Os testes da
/// classe dividem o banco: cada um começa aposentando (encerrando por SQL) os treinamentos de onboarding dos outros e semeia os seus.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class OnboardingTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(OnboardingTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await Sql.ExecutarAsync(api.Conexao, "UPDATE TREINAMENTOS SET ENCERRADO_EM = '2026-10-01 00:00:00' WHERE TIPO_TREINAMENTO = 'ONBOARDING' AND PUBLICADO = TRUE AND ENCERRADO_EM IS NULL");
        return api;
    }

    private static Task<int> OnboardingAsync(ApiDeAcesso api, string titulo, string situacao = "EM_ANDAMENTO", string publico = "TODOS") => SemearAsync(api, titulo, situacao, obrigatorio: true, publico: publico, onboarding: true);

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, bool ativa = true, int? setor = null) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, setor, ativa, new DateOnly(2026, 1, 2));

    private static Task<string?> LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa) =>
        TextoAsync(api, $"SELECT CONCAT(STATUS_TREINAMENTO, '|', IFNULL(DATE_FORMAT(DATA_INICIO, '%Y-%m-%d'), '-')) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static string Rota(int pessoa) => $"/api/rh/funcionarios/{pessoa}/treinamentos/onboarding";

    // ---------------------------------------------------------------- a lista (#4)

    [Fact]
    public async Task A_lista_traz_so_os_de_onboarding_publicados_e_nao_encerrados_com_o_agendado_pelo_inicio()
    {
        var api = await ApiAsync();
        var p = Titulo("ZOnb1");
        var andamento = await OnboardingAsync(api, $"{p} em andamento");
        var agendado = await OnboardingAsync(api, $"{p} agendado", "AGENDADO");
        _ = await OnboardingAsync(api, $"{p} encerrado", "ENCERRADO");
        var semData = await OnboardingAsync(api, $"{p} sem data");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = NULL WHERE ID_TREINAMENTO = {semData}");
        _ = await SemearAsync(api, $"{p} rascunho", obrigatorio: true, onboarding: true);
        _ = await SemearAsync(api, $"{p} comum", "EM_ANDAMENTO", obrigatorio: true);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync("/api/rh/treinamentos/onboarding");
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarTreinamentosDeOnboarding");
        Assert.Equal([andamento, agendado, semData], corpo.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32())); // 01/10 antes de 20/10 e sem data por último; só os em vigor
        Assert.Equal(($"{p} em andamento", "2026-10-01"), (corpo.GetProperty("itens")[0].GetProperty("titulo").GetString(), corpo.GetProperty("itens")[0].GetProperty("comecaEm").GetString()));
    }

    [Fact]
    public async Task Sem_nenhum_em_vigor_a_lista_e_vazia_e_so_o_RH_acessa_as_duas_rotas()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZOnb2 Ana");

        var vazia = await (await api.ClienteDoRhAgoraAsync()).GetAsync("/api/rh/treinamentos/onboarding");
        var respostas = new[]
        {
            await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync("/api/rh/treinamentos/onboarding"), await api.Api.ClienteComo(api.Elenco.MembroA).PostAsync(Rota(pessoa), null),
            await api.Api.ClienteAnonimo().GetAsync("/api/rh/treinamentos/onboarding"), await api.Api.ClienteAnonimo().PostAsync(Rota(pessoa), null),
        };

        Assert.Equal(0, (await CorpoAsync(vazia)).GetProperty("itens").GetArrayLength());
        Assert.Equal([403, 403, 401, 401], respostas.Select(r => (int)r.StatusCode));
    }

    // ---------------------------------------------------------------- incluir uma pessoa (#19)

    [Fact]
    public async Task Incluir_cria_a_linha_INSCRITO_em_cada_treinamento_em_vigor_qualquer_que_seja_o_publico_e_repetir_so_diz_que_ja_estava()
    {
        var api = await ApiAsync();
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZOnb3 outro setor"));
        var p = Titulo("ZOnb3");
        var deTodos = await OnboardingAsync(api, $"{p} todos");
        var deOutroSetor = await OnboardingAsync(api, $"{p} outro setor", "AGENDADO", "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({deOutroSetor}, {outroSetor})");
        var pessoa = await PessoaAsync(api, "ZOnb3 Ana");
        var rh = await api.ClienteDoRhAgoraAsync();

        var primeira = await rh.PostAsync(Rota(pessoa), null);
        var corpo = await CorpoAsync(primeira);
        var segunda = await rh.PostAsync(Rota(pessoa), null);

        Assert.Equal(200, (int)primeira.StatusCode);
        await primeira.AfirmarConformeAoContratoAsync("incluirFuncionarioNoOnboarding");
        Assert.Equal(pessoa, corpo.GetProperty("funcionarioId").GetInt32());
        Assert.Equal([(deTodos, false), (deOutroSetor, false)], corpo.GetProperty("treinamentos").EnumerateArray().Select(i => (i.GetProperty("treinamentoId").GetInt32(), i.GetProperty("jaEstavaIncluida").GetBoolean())));
        Assert.Equal(("INSCRITO|-", "INSCRITO|-"), (await LinhaAsync(api, deTodos, pessoa), await LinhaAsync(api, deOutroSetor, pessoa))); // mesmo fora do público, sem datas
        Assert.Equal([(deTodos, true), (deOutroSetor, true)], (await CorpoAsync(segunda)).GetProperty("treinamentos").EnumerateArray().Select(i => (i.GetProperty("treinamentoId").GetInt32(), i.GetProperty("jaEstavaIncluida").GetBoolean())));
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ID_TREINAMENTO IN ({deTodos}, {deOutroSetor})"));
        var eventos = await EventosAsync(api, deTodos);
        Assert.Empty(eventos); // o evento é da pessoa (FUNCIONARIOS), não do treinamento
        var daPessoa = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", pessoa)).Where(e => e.Area == AuditoriaArea.Treinamentos).ToList();
        var evento = Assert.Single(daPessoa); // só a primeira chamada criou linha
        Assert.Equal(AuditoriaAcao.Alterou, evento.Acao);
        Assert.Contains("2 treinamentos", evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quem_ja_tem_linha_nao_muda_e_a_concluida_continua_concluida()
    {
        var api = await ApiAsync();
        var concluido = await OnboardingAsync(api, Titulo("ZOnb4 concluido"));
        var novo = await OnboardingAsync(api, Titulo("ZOnb4 novo"));
        var pessoa = await PessoaAsync(api, "ZOnb4 Ana");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO, DATA_CONCLUSAO) VALUES ({concluido}, {pessoa}, 'CONCLUIDO', '2026-09-01', '2026-09-02')");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsync(Rota(pessoa), null);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal([(concluido, true), (novo, false)], (await CorpoAsync(resposta)).GetProperty("treinamentos").EnumerateArray().Select(i => (i.GetProperty("treinamentoId").GetInt32(), i.GetProperty("jaEstavaIncluida").GetBoolean())));
        Assert.Equal("CONCLUIDO|2026-09-01", await LinhaAsync(api, concluido, pessoa));
        Assert.Equal("INSCRITO|-", await LinhaAsync(api, novo, pessoa));
    }

    [Fact]
    public async Task Sem_treinamento_em_vigor_inativa_e_inexistente_recusam_sem_gravar_nada()
    {
        var api = await ApiAsync();
        var ativa = await PessoaAsync(api, "ZOnb5 Ana");
        var inativa = await PessoaAsync(api, "ZOnb5 Bia", ativa: false);
        var rh = await api.ClienteDoRhAgoraAsync();

        var semOnboarding = await rh.PostAsync(Rota(ativa), null);
        var emVigor = await OnboardingAsync(api, Titulo("ZOnb5 vigor"));
        var deInativa = await rh.PostAsync(Rota(inativa), null);
        var inexistente = await rh.PostAsync(Rota(987654), null);

        Assert.Equal([422, 422, 404], new[] { semOnboarding, deInativa, inexistente }.Select(r => (int)r.StatusCode));
        await semOnboarding.AfirmarConformeAoContratoAsync("incluirFuncionarioNoOnboarding");
        await deInativa.AfirmarConformeAoContratoAsync("incluirFuncionarioNoOnboarding");
        var corpoSem = await CorpoAsync(semOnboarding);
        Assert.Equal(("SEM_TREINAMENTO_DE_ONBOARDING", "Não há treinamento de onboarding publicado."), (Codigo(corpoSem), corpoSem.GetProperty("detail").GetString()));
        var corpoInativa = await CorpoAsync(deInativa);
        Assert.Equal(("FUNCIONARIO_INATIVO", "Não é possível incluir uma pessoa inativa em treinamento.", "Nao e possivel inscrever funcionario inativo em treinamento."), (Codigo(corpoInativa), corpoInativa.GetProperty("detail").GetString(), corpoInativa.GetProperty("regraDoBanco").GetString()));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {emVigor}"));
    }

    // ---------------------------------------------------------------- a caixa do cadastro (IInclusaoNoOnboarding real)

    private static object CorpoDoCadastro(int cargo, int setor, bool onboarding) => new Dictionary<string, object?>
    {
        ["nome"] = "Rafael Onboarding",
        ["cpf"] = CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf)),
        ["dataNascimento"] = "1995-02-09",
        ["telefone"] = "11991234567",
        ["emailCorporativo"] = $"onboarding.{Interlocked.Increment(ref _email)}@onb.com.br",
        ["dataAdmissao"] = "2026-02-03",
        ["tipoContrato"] = "CLT",
        ["cargoId"] = cargo,
        ["setorId"] = setor,
        ["perfil"] = "Funcionario",
        ["enviarConvite"] = false,
        ["incluirNoOnboarding"] = onboarding,
    };

    private static int _cpf = 800_000;
    private static int _email;

    [Fact]
    public async Task O_cadastro_com_a_caixa_marcada_inclui_a_pessoa_na_mesma_transacao_e_conta_pessoas_nao_linhas()
    {
        var api = await ApiAsync();
        var cargo = await ApoioDeTeste.CriarCargoAsync(api.Conexao, Titulo("ZOnb6 cargo"), null, 44m);
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZOnb6 setor"));
        var primeiro = await OnboardingAsync(api, Titulo("ZOnb6 primeiro"));
        var segundo = await OnboardingAsync(api, Titulo("ZOnb6 segundo"), "AGENDADO");
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await rh.PostAsJsonAsync("/api/rh/funcionarios", CorpoDoCadastro(cargo, setor, onboarding: true));
        var corpo = await CorpoAsync(resposta);
        var pessoa = corpo.GetProperty("id").GetInt32();

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(("INSCRITO|-", "INSCRITO|-"), (await LinhaAsync(api, primeiro, pessoa), await LinhaAsync(api, segundo, pessoa))); // na mesma transação do cadastro
    }

    [Fact]
    public async Task A_inclusao_da_caixa_conta_pessoas_e_nao_linhas_e_quem_ja_tinha_linha_em_todos_nao_conta()
    {
        var api = await ApiAsync();
        var primeiro = await OnboardingAsync(api, Titulo("ZOnb9 primeiro"));
        var segundo = await OnboardingAsync(api, Titulo("ZOnb9 segundo"), "AGENDADO");
        var a = await PessoaAsync(api, "ZOnb9 Ana");
        var b = await PessoaAsync(api, "ZOnb9 Bia");
        var c = await PessoaAsync(api, "ZOnb9 Caio");
        var jaTinhaTudo = await PessoaAsync(api, "ZOnb9 Dani");
        foreach (var t in new[] { primeiro, segundo })
        {
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({t}, {jaTinhaTudo}, 'INSCRITO')");
        }

        await using var escopo = api.Api.Services.CreateAsyncScope();
        var inclusao = escopo.ServiceProvider.GetRequiredService<RHSystem.Application.Colaboradores.IInclusaoNoOnboarding>();

        var entraram = await inclusao.IncluirAsync([a, b, c, jaTinhaTudo, a], CancellationToken.None);

        Assert.Equal(3, entraram); // 3 pessoas (e 2 treinamentos), não 6 linhas
        Assert.Equal(8L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO IN ({primeiro}, {segundo})"));
    }

    [Fact]
    public async Task O_cadastro_com_a_caixa_marcada_e_sem_treinamento_em_vigor_continua_recusando_com_o_mesmo_422_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var cargo = await ApoioDeTeste.CriarCargoAsync(api.Conexao, Titulo("ZOnb7 cargo"), null, 44m);
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZOnb7 setor"));
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM FUNCIONARIOS");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync("/api/rh/funcionarios", CorpoDoCadastro(cargo, setor, onboarding: true));
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal(("SEM_TREINAMENTO_DE_ONBOARDING", "Não há treinamento de onboarding publicado. Desmarque a caixa ou publique o treinamento."), (Codigo(corpo), corpo.GetProperty("detail").GetString()));
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM FUNCIONARIOS"));
    }

    [Fact]
    public async Task O_treinamento_encerrado_entre_a_conferencia_e_a_inclusao_desfaz_o_cadastro_em_vez_de_dizer_zero_em_silencio()
    {
        var api = await ApiAsync();
        var emVigor = await OnboardingAsync(api, Titulo("ZOnb8 vigor"));
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var inclusao = escopo.ServiceProvider.GetRequiredService<RHSystem.Application.Colaboradores.IInclusaoNoOnboarding>();

        await inclusao.ConferirAsync(CancellationToken.None); // em vigor: passa
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET ENCERRADO_EM = '2026-10-07 09:00:00' WHERE ID_TREINAMENTO = {emVigor}"); // o RH encerra no meio
        var pessoa = await PessoaAsync(api, "ZOnb8 Ana");

        var erro = await Assert.ThrowsAsync<RHSystem.Application.Comum.ErroDeNegocioException>(() => inclusao.IncluirAsync([pessoa], CancellationToken.None));

        Assert.Equal("SEM_TREINAMENTO_DE_ONBOARDING", erro.Codigo);
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {emVigor}"));
    }
}
