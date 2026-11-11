using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Colaboradores;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;
using static RHSystem.Api.IntegrationTests.Colaboradores.DesligamentoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// <c>POST /api/rh/funcionarios/{id}/readmissao</c> (tela 03, "Readmitir") pela API de verdade, no banco REAL com o relógio em 2026-10-07T12:42Z. Cada colaborador "ex" foi desligado pelo serviço de verdade
/// (<c>EFETIVADO</c>, último dia 2026-09-30): o trigger do banco já desativou o login e os benefícios e cancelou as férias futuras, como acontece na empresa.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ReadmissaoDeFuncionariosTests(MySqlFixture mysql)
{
    private static readonly DateOnly UltimoDia = new(2026, 9, 30);
    private static int _matricula = 92_000;

    private Task<ApiDeAcesso> ApiAsync() => ComecarAsync(mysql, nameof(ReadmissaoDeFuncionariosTests));

    private static string Rota(int id) => $"/api/rh/funcionarios/{id}/readmissao";

    private sealed record Ex(int Id, string Nome, string Email, int? UsuarioId, int Cargo, int Setor);

    /// <summary>
    /// Um colaborador desligado (EFETIVADO em 2026-09-30), admitido em 2026-01-02, com cargo, setor, matrícula e tipo de contrato; <paramref name="comBeneficioEFerias"/> dá um benefício ativo e férias futuras
    /// ANTES de desligar (o que o trigger cancela).
    /// </summary>
    private static async Task<Ex> InativoAsync(ApiDeAcesso api, string sufixo, TipoDeLogin login = TipoDeLogin.Provisorio, string nivel = "Funcionario", bool comBeneficioEFerias = false)
    {
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo readm {sufixo}", 44m);
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor readm {sufixo}");
        var pessoa = await api.NovaPessoaAsync($"readm{sufixo}", login: login);
        var matricula = Interlocked.Increment(ref _matricula).ToString(CultureInfo.InvariantCulture);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo}, ID_SETOR = {setor}, TIPO_CONTRATO = 'CLT', MATRICULA = '{matricula}', ID_NIVEL_ACESSO = (SELECT ID_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO WHERE NOME_NIVEL_ACESSO = '{nivel}') WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");
        if (comBeneficioEFerias)
        {
            var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, $"Beneficio readm {sufixo}");
            await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa.FuncionarioId, beneficio);
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, STATUS_FERIAS) VALUES ({pessoa.FuncionarioId}, '2026-01-02', '2027-01-01', '2027-03-01', '2027-03-15', 'APROVADA')");
        }

        await AgendarAsync(api, pessoa.FuncionarioId, UltimoDia);
        Assert.Equal("EFETIVADO", await SituacaoAsync(api, pessoa.FuncionarioId));
        return new Ex(pessoa.FuncionarioId, pessoa.Nome, pessoa.Email ?? string.Empty, pessoa.UsuarioId, cargo, setor);
    }

    /// <summary>Um gestor de setor (com um membro ativo no setor dele) que depois é desligado; devolve o "ex", o <c>ID_GESTOR</c> e o id do membro.</summary>
    private static async Task<(Ex Ex, int GestorId, int MembroId)> ExGestorAsync(ApiDeAcesso api, string sufixo)
    {
        var (pessoa, gestor, setor) = await GestorDeSetorAsync(api, $"readm {sufixo}");
        var cargo = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_CARGO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa}");
        var membro = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Membro readm {sufixo}", $"membro.readm.{Guid.NewGuid():N}@readm.com.br", new DateOnly(2026, 1, 5), setorId: setor, cargoId: cargo, gestorId: gestor);
        await AgendarAsync(api, pessoa, UltimoDia);
        var nome = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT NOME FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa}"))!;
        var email = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa}"))!;
        return (new Ex(pessoa, nome, email, null, cargo, setor), gestor, membro);
    }

    private static object Corpo(Ex ex, string admissao = "2026-10-05", string tipoContrato = "ESTAGIO", int? cargo = null, int? setor = null, bool? onboarding = null, int? formulario = null, bool? convite = null)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["dataAdmissao"] = admissao,
            ["cargoId"] = cargo ?? ex.Cargo,
            ["setorId"] = setor ?? ex.Setor,
            ["tipoContrato"] = tipoContrato,
        };
        if (onboarding is { } o)
        {
            corpo["incluirNoOnboarding"] = o;
        }

        if (formulario is { } f)
        {
            corpo["formularioAdmissaoId"] = f;
        }

        if (convite is { } c)
        {
            corpo["enviarConvite"] = c;
        }

        return corpo;
    }

    private static Task<HttpResponseMessage> ReadmitirAsync(ApiDeAcesso api, int id, object corpo) => api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota(id), corpo);

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta) => (await resposta.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    private static async Task<bool> AtivoDoLoginAsync(ApiDeAcesso api, int usuarioId) => await Sql.ContarAsync(api.Conexao, $"SELECT ATIVO FROM USUARIOS WHERE ID_USUARIO = {usuarioId}") == 1;

    private static Task<long> EmailsDaPessoaAsync(ApiDeAcesso api, int funcionarioId) => Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {funcionarioId}");

    /// <summary>Nada do que a readmissão faria ficou gravado: a pessoa segue inativa com o último dia, o desligamento segue EFETIVADO e nenhum e-mail entrou na fila.</summary>
    private static async Task AfirmarQueNadaMudouAsync(ApiDeAcesso api, Ex ex)
    {
        Assert.False(await AtivoAsync(api, ex.Id));
        Assert.Equal("2026-09-30", await DataDeDesligamentoAsync(api, ex.Id));
        Assert.Equal("EFETIVADO", await SituacaoAsync(api, ex.Id));
        Assert.Equal(0, await EmailsDaPessoaAsync(api, ex.Id));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {ex.Id}"));
    }

    [Fact]
    public async Task Readmitir_ativa_zera_o_desligamento_marca_REVERTIDO_e_devolve_a_ficha_com_a_nova_admissao()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r1");
        var matricula = await Sql.EscalarAsync(api.Conexao, $"SELECT MATRICULA FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {ex.Id}");
        var cargoNovo = await SementeDoCadastro.CargoAsync(api.Conexao, "Cargo readm r1 novo", 40m);
        var setorNovo = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor readm r1 novo");

        var resposta = await ReadmitirAsync(api, ex.Id, Corpo(ex, admissao: "2026-10-05", tipoContrato: "ESTAGIO", cargo: cargoNovo, setor: setorNovo));

        Assert.Equal(200, (int)resposta.StatusCode);
        var ficha = await LerAsync(resposta);
        Assert.Equal((ex.Id, true, "ATIVO", "2026-10-05", "ESTAGIO"), (ficha.GetProperty("id").GetInt32(), ficha.GetProperty("ativo").GetBoolean(), ficha.GetProperty("situacao").GetString(), ficha.GetProperty("dataAdmissao").GetString(), ficha.GetProperty("tipoContrato").GetString()));
        Assert.Equal((cargoNovo, setorNovo), (ficha.GetProperty("cargo").GetProperty("id").GetInt32(), ficha.GetProperty("setor").GetProperty("id").GetInt32()));
        Assert.Equal(matricula, ficha.GetProperty("matricula").GetString()); // a mesma matrícula: é o mesmo cadastro
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("desligamento").ValueKind);

        Assert.True(await AtivoAsync(api, ex.Id));
        Assert.Null(await DataDeDesligamentoAsync(api, ex.Id));
        Assert.Equal("REVERTIDO", await SituacaoAsync(api, ex.Id));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {ex.Id} AND EFETIVADO_EM IS NOT NULL AND REVERTIDO_EM = '2026-10-07 12:42:00'")); // o instante do relógio falso
    }

    [Fact]
    public async Task Readmitir_nao_restaura_beneficios_nem_ferias_e_a_ficha_vem_com_beneficios_vazio()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r2", comBeneficioEFerias: true);

        var resposta = await ReadmitirAsync(api, ex.Id, Corpo(ex));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(0, (await LerAsync(resposta)).GetProperty("beneficios").GetArrayLength());
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT BENEFICIO_ATIVO FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {ex.Id}")); // continua desativado
        Assert.Equal("CANCELADA", await Sql.EscalarAsync(api.Conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FUNCIONARIO = {ex.Id}")); // e as férias continuam canceladas
    }

    [Fact]
    public async Task Readmitir_grava_o_evento_Alterou_com_data_de_admissao_antes_e_depois_e_ultimo_dia_de_trabalho_antes_e_null()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r3");

        await ReadmitirAsync(api, ex.Id, Corpo(ex, admissao: "2026-10-05"));

        var evento = Assert.Single(await EventosAsync(api.Conexao, "FUNCIONARIOS", ex.Id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal((AuditoriaArea.Colaboradores, api.Elenco.Rh.FuncionarioId), (evento.Area, evento.FuncionarioAtorId));
        var admissao = Assert.Single(evento.Mudancas, m => m.Campo == "Data de admissão");
        Assert.Equal(("02/01/2026", "05/10/2026"), (admissao.ValorAntes, admissao.ValorDepois)); // as datas do vínculo anterior ficam aqui
        var ultimoDia = Assert.Single(evento.Mudancas, m => m.Campo == "Último dia de trabalho");
        Assert.Equal(("30/09/2026", (string?)null), (ultimoDia.ValorAntes, ultimoDia.ValorDepois));
        var situacao = Assert.Single(evento.Mudancas, m => m.Campo == "Situação");
        Assert.Equal(("Não", "Sim"), (situacao.ValorAntes, situacao.ValorDepois));
        Assert.Contains(evento.Mudancas, m => m.Campo == "Tipo de contrato"); // CLT -> ESTAGIO
    }

    [Fact]
    public async Task Readmitir_ex_gestor_volta_como_Funcionario_e_o_setor_que_apontava_para_ele_e_os_membros_dele_ficam_sem_gestor()
    {
        var api = await ApiAsync();
        var (ex, gestor, membro) = await ExGestorAsync(api, "r4");
        Assert.Equal(gestor, await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM SETORES WHERE ID_SETOR = {ex.Setor}")); // o setor ainda aponta para ela
        Assert.Equal(gestor, await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {membro}"));

        var resposta = await ReadmitirAsync(api, ex.Id, Corpo(ex, tipoContrato: "CLT")); // volta ao mesmo setor

        Assert.Equal(200, (int)resposta.StatusCode);
        var ficha = await LerAsync(resposta);
        Assert.Equal("Funcionario", ficha.GetProperty("perfil").GetString()); // Gestor volta como Funcionario
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("gestor").ValueKind);
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM SETORES WHERE ID_SETOR = {ex.Setor}")); // o setor ficou sem gestor
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {membro}")); // e o membro dele também
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM GESTORES WHERE ID_GESTOR = {gestor}")); // a linha em GESTORES não é apagada
    }

    [Fact]
    public async Task Readmitir_ex_gestor_chama_PassarParaORhAsync_uma_vez_com_o_id_do_gestor_e_se_o_setor_ja_nao_apontava_para_ele_nao_chama()
    {
        var api = await ApiAsync();
        var (apontada, gestorApontado, _) = await ExGestorAsync(api, "r5a");
        var (trocada, _, _) = await ExGestorAsync(api, "r5b");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = NULL WHERE ID_SETOR = {trocada.Setor}"); // a 1c trocou o gestor depois do desligamento (e já chamou a porta)
        var chamadas = new List<string>();
        void Configurar(IServiceCollection s) => s.AddScoped<IPedidosParadosNoGestorDaFatia>(_ => new FatiaDeTeste(chamadas));

        await api.NoEscopoComAsync(Configurar, sp => ReadmitirNoEscopoAsync(sp, apontada));
        Assert.Equal([$"Passar:{gestorApontado}"], chamadas); // uma vez, com o ID_GESTOR

        chamadas.Clear();
        await api.NoEscopoComAsync(Configurar, sp => ReadmitirNoEscopoAsync(sp, trocada));
        Assert.Empty(chamadas); // o setor já não apontava para ela: nada a passar
        Assert.True(await AtivoAsync(api, trocada.Id));
    }

    [Fact]
    public async Task Se_PassarParaORhAsync_lanca_a_readmissao_inteira_desfaz()
    {
        var api = await ApiAsync();
        var (ex, gestor, membro) = await ExGestorAsync(api, "r6");
        var chamadas = new List<string>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NoEscopoComAsync(
            s => s.AddScoped<IPedidosParadosNoGestorDaFatia>(_ => new FatiaDeTeste(chamadas, lanca: true)),
            sp => ReadmitirNoEscopoAsync(sp, ex)));

        Assert.Single(chamadas); // chegou a chamar
        await AfirmarQueNadaMudouAsync(api, ex);
        Assert.Equal(gestor, await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM SETORES WHERE ID_SETOR = {ex.Setor}")); // o setor continua apontando
        Assert.Equal(gestor, await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {membro}"));
    }

    [Fact]
    public async Task Readmitir_com_gestor_do_setor_inativo_devolve_422_GESTOR_DO_SETOR_INATIVO_e_nada_muda()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r7");
        var (gestora, _, setor) = await GestorDeSetorAsync(api, "readm r7 gestora");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-01' WHERE ID_FUNCIONARIO = {gestora}");

        var resposta = await ReadmitirAsync(api, ex.Id, Corpo(ex, setor: setor));

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("GESTOR_DO_SETOR_INATIVO", (await LerAsync(resposta)).GetProperty("codigo").GetString());
        await AfirmarQueNadaMudouAsync(api, ex);
    }

    [Fact]
    public async Task Readmitir_ativo_ou_com_desligamento_so_agendado_devolve_409_FUNCIONARIO_JA_ATIVO()
    {
        var api = await ApiAsync();
        var ativo = await InativoAsync(api, "r8a");
        Assert.Equal(200, (int)(await ReadmitirAsync(api, ativo.Id, Corpo(ativo))).StatusCode); // agora está ativo
        var agendado = await InativoAsync(api, "r8b");
        await ReadmitirAsync(api, agendado.Id, Corpo(agendado)); // ativo de novo
        await AgendarAsync(api, agendado.Id, new DateOnly(2026, 11, 13)); // e com desligamento só AGENDADO

        foreach (var pessoa in new[] { ativo, agendado })
        {
            var resposta = await ReadmitirAsync(api, pessoa.Id, Corpo(pessoa));
            Assert.Equal(409, (int)resposta.StatusCode);
            var erro = await LerAsync(resposta);
            Assert.Equal("FUNCIONARIO_JA_ATIVO", erro.GetProperty("codigo").GetString());
            Assert.Equal("Este colaborador já está ativo. Só se readmite quem está inativo.", erro.GetProperty("detail").GetString());
        }

        Assert.Equal("AGENDADO", await SituacaoAsync(api, agendado.Id)); // o desligamento agendado não foi tocado
    }

    [Fact]
    public async Task Dois_cliques_em_Readmitir_dao_um_200_e_um_409()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r9");
        using var barreira = new Barrier(6);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => EmParaleloAsync(barreira, () => ReadmitirAsync(api, ex.Id, Corpo(ex)))));

        Assert.Equal(1, respostas.Count(r => (int)r.StatusCode == 200)); // um clique ganha
        Assert.All(respostas.Where(r => (int)r.StatusCode != 200), r => Assert.Equal(409, (int)r.StatusCode)); // os outros recebem o 409, nunca um 500
        Assert.Equal("REVERTIDO", await SituacaoAsync(api, ex.Id));
        Assert.True(await AtivoAsync(api, ex.Id));
    }

    [Fact]
    public async Task Readmitir_com_dataAdmissao_antes_do_ultimo_dia_de_trabalho_devolve_400_em_dataAdmissao()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r10a");
        var noLimite = await InativoAsync(api, "r10b");

        var antes = await ReadmitirAsync(api, ex.Id, Corpo(ex, admissao: "2026-09-29"));

        Assert.Equal(400, (int)antes.StatusCode);
        Assert.True((await LerAsync(antes)).GetProperty("errors").TryGetProperty("dataAdmissao", out _));
        await AfirmarQueNadaMudouAsync(api, ex);
        Assert.Equal(200, (int)(await ReadmitirAsync(api, noLimite.Id, Corpo(noLimite, admissao: "2026-09-30"))).StatusCode); // no próprio último dia vale ("anterior" ao último dia é que não)
    }

    [Fact]
    public async Task Readmitir_com_data_futura_fica_EM_ADMISSAO_e_situacaoHoje_null()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r11");

        var resposta = await ReadmitirAsync(api, ex.Id, Corpo(ex, admissao: "2026-11-02"));

        Assert.Equal(200, (int)resposta.StatusCode);
        var ficha = await LerAsync(resposta);
        Assert.Equal(("EM_ADMISSAO", true), (ficha.GetProperty("situacao").GetString(), ficha.GetProperty("ativo").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("situacaoHoje").ValueKind);
    }

    [Fact]
    public async Task Sem_convite_o_login_continua_desativado_e_a_ficha_mostra_acesso_ativo_false()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r12");

        var resposta = await ReadmitirAsync(api, ex.Id, Corpo(ex)); // enviarConvite fica false

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.False((await LerAsync(resposta)).GetProperty("acesso").GetProperty("ativo").GetBoolean());
        Assert.False(await AtivoDoLoginAsync(api, ex.UsuarioId!.Value)); // quem libera é o "Ativar" de Usuários do sistema
        Assert.Equal(0, await EmailsDaPessoaAsync(api, ex.Id));
    }

    [Fact]
    public async Task Convite_para_quem_nunca_definiu_a_senha_ativa_o_login_e_enfileira_o_CONVITE()
    {
        var api = await ApiAsync();
        var comLoginProvisorio = await InativoAsync(api, "r13a", TipoDeLogin.Provisorio);
        var semLogin = await InativoAsync(api, "r13b", TipoDeLogin.Nenhum);

        var primeira = await ReadmitirAsync(api, comLoginProvisorio.Id, Corpo(comLoginProvisorio, convite: true));
        var segunda = await ReadmitirAsync(api, semLogin.Id, Corpo(semLogin, convite: true));

        Assert.Equal(200, (int)primeira.StatusCode);
        Assert.True((await LerAsync(primeira)).GetProperty("acesso").GetProperty("ativo").GetBoolean());
        Assert.True(await AtivoDoLoginAsync(api, comLoginProvisorio.UsuarioId!.Value)); // reativou o login que a inativação desativou
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {comLoginProvisorio.Id} AND TIPO = 'CONVITE'"));

        Assert.Equal(200, (int)segunda.StatusCode); // quem não tinha login ganha um
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {semLogin.Id} AND ATIVO = TRUE AND SENHA_PROVISORIA = TRUE"));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {semLogin.Id} AND TIPO = 'CONVITE'"));
    }

    [Fact]
    public async Task Convite_para_quem_ja_definiu_a_senha_devolve_422_SENHA_JA_DEFINIDA_e_nada_muda()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r14", TipoDeLogin.SenhaDefinida);

        var resposta = await ReadmitirAsync(api, ex.Id, Corpo(ex, convite: true));

        Assert.Equal(422, (int)resposta.StatusCode);
        var erro = await LerAsync(resposta);
        Assert.Equal("SENHA_JA_DEFINIDA", erro.GetProperty("codigo").GetString());
        Assert.Equal("Esta pessoa já definiu a senha. Desmarque o convite e libere o acesso em Usuários do sistema (Ativar).", erro.GetProperty("detail").GetString());
        await AfirmarQueNadaMudouAsync(api, ex);
        Assert.False(await AtivoDoLoginAsync(api, ex.UsuarioId!.Value));
    }

    [Fact]
    public async Task Caixas_sem_fonte_devolvem_o_422_e_o_400_e_nada_muda()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r15");

        var onboarding = await ReadmitirAsync(api, ex.Id, Corpo(ex, onboarding: true));
        var formulario = await ReadmitirAsync(api, ex.Id, Corpo(ex, formulario: 77));

        Assert.Equal(422, (int)onboarding.StatusCode);
        Assert.Equal("SEM_TREINAMENTO_DE_ONBOARDING", (await LerAsync(onboarding)).GetProperty("codigo").GetString());
        Assert.Equal(400, (int)formulario.StatusCode);
        Assert.True((await LerAsync(formulario)).GetProperty("errors").TryGetProperty("formularioAdmissaoId", out _));
        await AfirmarQueNadaMudouAsync(api, ex);
    }

    [Fact]
    public async Task Falha_depois_de_salvar_o_colaborador_desfaz_tudo_inclusive_o_REVERTIDO_e_o_login()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r16", TipoDeLogin.Provisorio);

        // O onboarding confere (passa) e quebra DEPOIS de o colaborador, o desligamento revertido e o login reativado já terem sido gravados na transação.
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NoEscopoComAsync(
            s => s.AddScoped<IInclusaoNoOnboarding>(_ => new OnboardingQueQuebra()),
            sp => ReadmitirNoEscopoAsync(sp, ex, onboarding: true, convite: true)));

        await AfirmarQueNadaMudouAsync(api, ex);
        Assert.False(await AtivoDoLoginAsync(api, ex.UsuarioId!.Value)); // a reativação do login também foi desfeita
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE ID_USUARIO = {ex.UsuarioId}"));
    }

    [Fact]
    public async Task Readmitir_nao_enfileira_nenhum_email_alem_do_convite()
    {
        var api = await ApiAsync();
        var semConvite = await InativoAsync(api, "r17a");
        var comConvite = await InativoAsync(api, "r17b");

        await ReadmitirAsync(api, semConvite.Id, Corpo(semConvite));
        await ReadmitirAsync(api, comConvite.Id, Corpo(comConvite, convite: true));

        Assert.Equal(0, await EmailsDaPessoaAsync(api, semConvite.Id)); // nenhum e-mail pelas caixas nem pelo fato de voltar
        Assert.Equal(1, await EmailsDaPessoaAsync(api, comConvite.Id));
        Assert.Equal("CONVITE", await Sql.EscalarAsync(api.Conexao, $"SELECT TIPO FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {comConvite.Id}"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE DESTINATARIO_EMAIL = '{semConvite.Email}'"));
    }

    [Fact]
    public async Task Readmitir_inexistente_404_perfil_errado_403_e_anonimo_401_e_respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r18a");
        var invalido = await InativoAsync(api, "r18b");
        var ativo = await InativoAsync(api, "r18c");
        var senhaDefinida = await InativoAsync(api, "r18d", TipoDeLogin.SenhaDefinida);
        var corpo = Corpo(ex);

        Assert.Equal(404, (int)(await ReadmitirAsync(api, 987_654, corpo)).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.Gestor).PostAsJsonAsync(Rota(ex.Id), corpo)).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.MembroA).PostAsJsonAsync(Rota(ex.Id), corpo)).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().PostAsJsonAsync(Rota(ex.Id), corpo)).StatusCode);
        await AfirmarQueNadaMudouAsync(api, ex); // nenhuma das recusadas gravou

        await (await ReadmitirAsync(api, ex.Id, corpo)).AfirmarConformeAoContratoAsync("readmitirFuncionario"); // o 200
        await (await ReadmitirAsync(api, invalido.Id, Corpo(invalido, admissao: "2026-09-01"))).AfirmarConformeAoContratoAsync("readmitirFuncionario"); // o 400
        await (await ReadmitirAsync(api, 987_654, corpo)).AfirmarConformeAoContratoAsync("readmitirFuncionario"); // o 404
        await (await ReadmitirAsync(api, ex.Id, corpo)).AfirmarConformeAoContratoAsync("readmitirFuncionario"); // o 409
        await ReadmitirAsync(api, ativo.Id, Corpo(ativo));
        await (await ReadmitirAsync(api, senhaDefinida.Id, Corpo(senhaDefinida, convite: true))).AfirmarConformeAoContratoAsync("readmitirFuncionario"); // o 422
    }

    [Fact]
    public async Task Readmitir_sem_tipoContrato_ou_com_cargo_e_setor_inexistentes_devolve_400_por_campo_e_nada_muda()
    {
        var api = await ApiAsync();
        var ex = await InativoAsync(api, "r19");

        // O System.Text.Json deixa o enum omitido no padrão (0, que não é um tipo): sem a validação a pessoa voltaria sem tipo de contrato.
        var semTipo = await ReadmitirAsync(api, ex.Id, new { dataAdmissao = "2026-10-05", cargoId = ex.Cargo, setorId = ex.Setor });
        var inexistentes = await ReadmitirAsync(api, ex.Id, Corpo(ex, cargo: 987_654, setor: 987_655));

        Assert.Equal(400, (int)semTipo.StatusCode);
        Assert.True((await LerAsync(semTipo)).GetProperty("errors").TryGetProperty("tipoContrato", out _));
        Assert.Equal(400, (int)inexistentes.StatusCode);
        var erros = (await LerAsync(inexistentes)).GetProperty("errors");
        Assert.True(erros.TryGetProperty("cargoId", out _) && erros.TryGetProperty("setorId", out _)); // todos os campos juntos, nunca um 500 de chave estrangeira
        await AfirmarQueNadaMudouAsync(api, ex);
    }

    [Fact]
    public async Task Readmitir_ex_RH_continua_RH_e_o_gestor_da_pessoa_passa_a_ser_o_do_setor_escolhido()
    {
        var api = await ApiAsync();
        var rh = await InativoAsync(api, "r20a", nivel: "RH");
        var comum = await InativoAsync(api, "r20b");
        var (_, gestorId, setorComGestor) = await GestorDeSetorAsync(api, "readm r20 gestora");

        var fichaDoRh = await LerAsync(await ReadmitirAsync(api, rh.Id, Corpo(rh)));
        var fichaDoComum = await LerAsync(await ReadmitirAsync(api, comum.Id, Corpo(comum, setor: setorComGestor)));

        Assert.Equal("RH", fichaDoRh.GetProperty("perfil").GetString()); // o nível é o que tinha; só Gestor volta como Funcionario
        Assert.Equal("Funcionario", fichaDoComum.GetProperty("perfil").GetString());
        Assert.Equal(gestorId, fichaDoComum.GetProperty("gestor").GetProperty("gestorId").GetInt32()); // gestor = o do setor escolhido, como no cadastro
    }

    /// <summary>Chama o serviço direto (sem a API), no escopo em que o ator é o RH; a exceção do serviço sobe como está.</summary>
    private static async Task<bool> ReadmitirNoEscopoAsync(IServiceProvider sp, Ex ex, bool onboarding = false, bool convite = false)
    {
        await sp.GetRequiredService<IReadmissaoDeFuncionarios>().ReadmitirAsync(ex.Id, new DadosDaReadmissao(new DateOnly(2026, 10, 5), ex.Cargo, ex.Setor, TipoDeContrato.Clt, onboarding, null, convite), CancellationToken.None);
        return true;
    }

    private sealed class OnboardingQueQuebra : IInclusaoNoOnboarding
    {
        public Task ConferirAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<int> IncluirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct) => throw new InvalidOperationException("o onboarding quebrou (de propósito)");
    }
}
