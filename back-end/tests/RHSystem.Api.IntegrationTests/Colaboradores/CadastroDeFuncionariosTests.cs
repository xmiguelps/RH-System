using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// O serviço de cadastro (o caminho único de criação), chamado DIRETO dentro de uma transação, com o ator RH, no banco REAL com o relógio em 2026-10-07T12:42Z. Um banco para a classe; cada teste
/// zera o contador de matrícula num valor próprio e usa CPF e e-mail únicos.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CadastroDeFuncionariosTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static int _cpf = 200_000;
    private static int _email;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(CadastroDeFuncionariosTests));

    private static NovoFuncionario Novo(int cargoId, int setorId, IReadOnlyList<int>? beneficios = null, Perfil perfil = Perfil.Funcionario, string? cpf = null, string? email = null) =>
        new(
            "Pessoa Nova",
            cpf ?? CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf)),
            new DateOnly(1990, 5, 17),
            "11987654410",
            email ?? $"cadastro.{Interlocked.Increment(ref _email)}@cadastro.com.br",
            new DateOnly(2026, 2, 3),
            TipoDeContrato.Clt,
            cargoId,
            setorId,
            perfil,
            beneficios ?? []);

    private static readonly OpcoesDoCadastro SemCaixas = new(EnviarConvite: false, IncluirNoOnboarding: false, FormularioAdmissaoId: null);

    private static async Task DefinirContadorAsync(ApiDeAcesso api, long valor) =>
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SEQUENCIAS SET ULTIMO_VALOR = {valor} WHERE NOME = 'MATRICULA'");

    private static async Task<long> ContadorAsync(ApiDeAcesso api) =>
        await Sql.ContarAsync(api.Conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'");

    private static Task<ResultadoDoCadastro> CadastrarAsync(ApiDeAcesso api, IReadOnlyList<NovoFuncionario> novos, OpcoesDoCadastro? opcoes = null) =>
        api.NaTransacaoAsync(sp => sp.GetRequiredService<ICadastroDeFuncionarios>().CadastrarAsync(novos, opcoes ?? SemCaixas, Ct));

    private static Task<IReadOnlyList<ProblemaDeCadastro>> ValidarAsync(ApiDeAcesso api, IReadOnlyList<NovoFuncionario> novos) =>
        api.NoEscopoAsync(sp => sp.GetRequiredService<ICadastroDeFuncionarios>().ValidarAsync(novos, Ct));

    private static async Task<(int Cargo, int Setor)> CargoESetorAsync(ApiDeAcesso api, string sufixo, int? gestorId = null) =>
        (await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo {sufixo}", 40m), await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor {sufixo}", gestorId));

    private static async Task<int> GestorAsync(ApiDeAcesso api, string nome, bool ativo = true)
    {
        var funcionario = await SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, $"gestor.{Interlocked.Increment(ref _email)}@cadastro.com.br", new DateOnly(2025, 1, 6), nivel: "Funcionario Gestor");
        await api.NovoGestorAsync(funcionario);
        var gestorId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionario}");
        if (!ativo)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {funcionario}");
        }

        return gestorId;
    }

    [Fact]
    public async Task ValidarAsync_nao_grava_nada_e_acusa_CPF_invalido_e_cargo_inexistente_e_gestor_inativo_por_linha()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "validar1");
        var gestorInativo = await GestorAsync(api, "Gestor inativo validar1", ativo: false);
        var setorComGestorInativo = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor gestor inativo validar1", gestorInativo);
        var antes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS");
        var linhas = new[]
        {
            Novo(cargo, setor, cpf: "11111111111"),
            Novo(999_999, setor),
            Novo(cargo, setorComGestorInativo),
            Novo(cargo, setor),
        };

        var problemas = await ValidarAsync(api, linhas);

        Assert.Equal(
            [(0, TipoDeProblemaDeCadastro.Campo, "cpf"), (1, TipoDeProblemaDeCadastro.Campo, "cargoId"), (2, TipoDeProblemaDeCadastro.Regra, "setorId")],
            problemas.Select(p => (p.Indice, p.Tipo, p.Campo)));
        Assert.Equal("GESTOR_DO_SETOR_INATIVO", problemas[2].Codigo);
        Assert.Equal(antes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS")); // validar nunca grava
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES WHERE CPF = '11111111111'"));
    }

    [Fact]
    public async Task ValidarAsync_acusa_CPF_e_email_repetidos_no_lote_com_OutroIndice_e_os_ja_cadastrados_com_Existente()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "validar2");
        var ativa = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Ana ativa validar2", "ana.validar2@cadastro.com.br", new DateOnly(2026, 1, 5), setorId: setor);
        await SementeDoCadastro.DetalhesAsync(api.Conexao, ativa, "52998224725");
        var inativa = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Henrique inativo validar2", "henrique.validar2@cadastro.com.br", new DateOnly(2026, 1, 5), ativo: false, setorId: setor);
        await SementeDoCadastro.DetalhesAsync(api.Conexao, inativa, "11144477735");
        var repetidoNoLote = CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf));
        var linhas = new[]
        {
            Novo(cargo, setor, cpf: "52998224725"), // CPF da Ana (ativa)
            Novo(cargo, setor, email: "  HENRIQUE.VALIDAR2@CADASTRO.COM.BR "), // e-mail do Henrique (inativo), com outra caixa e espaços
            Novo(cargo, setor, cpf: repetidoNoLote, email: "repetido.validar2@cadastro.com.br"),
            Novo(cargo, setor, cpf: repetidoNoLote, email: "REPETIDO.validar2@cadastro.com.br"), // repete o CPF e o e-mail da linha 2
            Novo(cargo, setor),
        };

        var problemas = await ValidarAsync(api, linhas);

        var daAna = Assert.Single(problemas, p => p is { Indice: 0, Tipo: TipoDeProblemaDeCadastro.Duplicado });
        Assert.Equal(("cpf", "CPF_JA_CADASTRADO", new CadastroExistente(ativa, "Ana ativa validar2", true)), (daAna.Campo, daAna.Codigo, daAna.Existente));
        Assert.Equal("Já existe um colaborador com este CPF.", daAna.Mensagem);
        var doHenrique = Assert.Single(problemas, p => p is { Indice: 1, Tipo: TipoDeProblemaDeCadastro.Duplicado });
        Assert.Equal(("emailCorporativo", "EMAIL_JA_CADASTRADO", new CadastroExistente(inativa, "Henrique inativo validar2", false)), (doHenrique.Campo, doHenrique.Codigo, doHenrique.Existente));
        Assert.Equal("Este e-mail corporativo pertence a um cadastro inativo (Henrique inativo validar2). Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.", doHenrique.Mensagem);
        var noLote = problemas.Where(p => p.Indice == 3).ToList();
        Assert.Equal([("cpf", 2), ("emailCorporativo", 2)], noLote.Select(p => (p.Campo, p.OutroIndice!.Value))); // aponta a linha de cima; o CPF vem antes do e-mail
        Assert.All(noLote, p => Assert.Null(p.Existente));
        Assert.DoesNotContain(problemas, p => p.Indice is 2 or 4); // a primeira ocorrência e a linha limpa não têm problema
    }

    [Fact]
    public async Task Cadastrar_3_reserva_3_matriculas_consecutivas_de_5_digitos_e_grava_funcionario_detalhes_beneficios_e_gestor_do_setor()
    {
        var api = await ApiAsync();
        var gestor = await GestorAsync(api, "Gestor cadastrar3");
        var (cargo, setor) = await CargoESetorAsync(api, "cadastrar3", gestor);
        var b1 = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Vale-refeição cadastrar3");
        var b2 = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Plano de saúde cadastrar3");
        await DefinirContadorAsync(api, 50_000);
        var linhas = new[] { Novo(cargo, setor, [b1, b2]), Novo(cargo, setor, [b1]), Novo(cargo, setor, perfil: Perfil.Gestor) };

        var resultado = await CadastrarAsync(api, linhas, new OpcoesDoCadastro(EnviarConvite: true, IncluirNoOnboarding: false, FormularioAdmissaoId: null));

        Assert.Equal(["50001", "50002", "50003"], resultado.Cadastrados.Select(c => c.Matricula));
        Assert.All(resultado.Cadastrados, c => Assert.Matches("^[0-9]{5}$", c.Matricula));
        Assert.Equal(50_003, await ContadorAsync(api));
        Assert.Equal((3, 0, 0), (resultado.ConvitesEnviados, resultado.IncluidosNoOnboarding, resultado.IncluidosNoFormularioDeAdmissao));
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var ids = resultado.Cadastrados.Select(c => c.Id).ToList();
        var gravados = await db.Funcionarios.Include(f => f.Detalhes).Include(f => f.NivelDeAcesso).Where(f => ids.Contains(f.Id)).OrderBy(f => f.Id).ToListAsync(Ct);
        Assert.Equal(["50001", "50002", "50003"], gravados.Select(f => f.Matricula));
        Assert.All(gravados, f => Assert.Equal((cargo, setor, gestor, true), (f.CargoId, f.SetorId, f.GestorId, f.Ativo))); // o gestor é o do setor
        Assert.Equal([linhas[0].Cpf, linhas[1].Cpf, linhas[2].Cpf], gravados.Select(f => f.Detalhes!.Cpf));
        Assert.Equal(["Funcionario", "Funcionario", "Funcionario Gestor"], gravados.Select(f => f.NivelDeAcesso!.Nome));
        var beneficiosAtivos = new List<int>(); // um de cada vez: um DbContext não aceita duas consultas ao mesmo tempo
        foreach (var id in ids)
        {
            beneficiosAtivos.Add(await db.BeneficiosDosFuncionarios.CountAsync(b => b.FuncionarioId == id && b.Ativo, Ct));
        }

        Assert.Equal([2, 1, 0], beneficiosAtivos);
        Assert.Equal(3, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE' AND ID_FUNCIONARIO IN ({string.Join(",", ids)})"));
    }

    [Fact]
    public async Task Cadastrar_500_em_uma_transacao_reserva_500_matriculas_sem_buraco()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "cadastrar500");
        await DefinirContadorAsync(api, 60_000);
        var linhas = Enumerable.Range(0, 500).Select(_ => Novo(cargo, setor)).ToList();

        var resultado = await CadastrarAsync(api, linhas);

        Assert.Equal(500, resultado.Cadastrados.Count);
        Assert.Equal(Enumerable.Range(60_001, 500).Select(n => n.ToString("D5", CultureInfo.InvariantCulture)), resultado.Cadastrados.Select(c => c.Matricula)); // consecutivas
        Assert.Equal(60_500, await ContadorAsync(api)); // sem buraco: uma reserva do tamanho do lote
    }

    [Fact]
    public async Task Linha_recusada_pelo_banco_desfaz_o_lote_inteiro_e_a_matricula_volta()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "recusada");
        await DefinirContadorAsync(api, 90_000);
        await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Dono da matrícula 90003", "dono.recusada@cadastro.com.br", new DateOnly(2026, 1, 5), matricula: "90003"); // a 3a linha pegaria a 90003
        var linhas = new[] { Novo(cargo, setor), Novo(cargo, setor), Novo(cargo, setor) };

        var erro = await Assert.ThrowsAsync<LinhaRecusadaException>(() => CadastrarAsync(api, linhas));

        Assert.Equal((2, true), (erro.Indice, erro.PorDuplicidade)); // o banco aponta a linha
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES WHERE CPF IN ('{linhas[0].Cpf}', '{linhas[1].Cpf}', '{linhas[2].Cpf}')")); // as duas primeiras também desfeitas
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO IN ('{linhas[0].EmailCorporativo}', '{linhas[1].EmailCorporativo}', '{linhas[2].EmailCorporativo}')"));
        Assert.Equal(90_000, await ContadorAsync(api)); // a matrícula volta
    }

    [Fact]
    public async Task Cadastro_com_duplicidade_ou_campo_invalido_lanca_CadastroInvalido_sem_reservar_matricula()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "invalido");
        await DefinirContadorAsync(api, 91_000);

        var erro = await Assert.ThrowsAsync<CadastroInvalidoException>(() => CadastrarAsync(api, [Novo(cargo, setor), Novo(cargo, setor, cpf: "11111111111")]));

        Assert.Equal([(1, "cpf")], erro.Problemas.Select(p => (p.Indice, p.Campo)));
        Assert.Equal(91_000, await ContadorAsync(api));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CadastrarAsync(api, []));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CadastrarAsync(api, [.. Enumerable.Range(0, 501).Select(_ => Novo(cargo, setor))]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NoEscopoAsync(sp => sp.GetRequiredService<ICadastroDeFuncionarios>().CadastrarAsync([Novo(cargo, setor)], SemCaixas, Ct))); // sem transação
    }

    [Fact]
    public async Task Falha_na_caixa_de_onboarding_ou_de_formulario_desfaz_colaborador_dados_pessoais_beneficios_login_token_e_email()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "caixa-falha");
        var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Benefício caixa-falha");
        await DefinirContadorAsync(api, 92_000);

        async Task ConferirNadaFicouAsync(NovoFuncionario linha)
        {
            Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{linha.EmailCorporativo}'"));
            Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES WHERE CPF = '{linha.Cpf}'"));
            Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE DESTINATARIO_EMAIL = '{linha.EmailCorporativo}'"));
            Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS U JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = U.ID_FUNCIONARIO WHERE F.EMAIL_CORPORATIVO = '{linha.EmailCorporativo}'"));
            Assert.Equal(92_000, await ContadorAsync(api));
        }

        var comOnboardingQueFalha = Novo(cargo, setor, [beneficio]);
        var opcoesComOnboarding = new OpcoesDoCadastro(EnviarConvite: true, IncluirNoOnboarding: true, FormularioAdmissaoId: null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NaTransacaoComAsync(
            servicos => servicos.AddScoped<IInclusaoNoOnboarding, OnboardingQueFalhaDepois>(),
            sp => sp.GetRequiredService<ICadastroDeFuncionarios>().CadastrarAsync([comOnboardingQueFalha], opcoesComOnboarding, Ct)));
        await ConferirNadaFicouAsync(comOnboardingQueFalha);

        var comFormularioQueFalha = Novo(cargo, setor, [beneficio]);
        var opcoesComFormulario = new OpcoesDoCadastro(EnviarConvite: true, IncluirNoOnboarding: false, FormularioAdmissaoId: 11);
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NaTransacaoComAsync(
            servicos => servicos.AddScoped<IEnvioDoFormularioDeAdmissao, FormularioQueFalhaDepois>(),
            sp => sp.GetRequiredService<ICadastroDeFuncionarios>().CadastrarAsync([comFormularioQueFalha], opcoesComFormulario, Ct)));
        await ConferirNadaFicouAsync(comFormularioQueFalha);
    }

    [Fact]
    public async Task Caixas_marcadas_sem_fonte_devolvem_o_422_SEM_TREINAMENTO_DE_ONBOARDING_e_o_400_em_formularioAdmissaoId_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "sem-fonte");
        await DefinirContadorAsync(api, 93_000);
        var linha = Novo(cargo, setor);

        var onboarding = await Assert.ThrowsAsync<ErroDeNegocioException>(() => CadastrarAsync(api, [linha], new OpcoesDoCadastro(true, IncluirNoOnboarding: true, null)));
        var formulario = await Assert.ThrowsAsync<ErroDeValidacaoException>(() => CadastrarAsync(api, [linha], new OpcoesDoCadastro(true, false, FormularioAdmissaoId: 11)));

        Assert.Equal(("SEM_TREINAMENTO_DE_ONBOARDING", "Não há treinamento de onboarding publicado. Desmarque a caixa ou publique o treinamento."), (onboarding.Codigo, onboarding.Detalhe));
        Assert.Contains("formularioAdmissaoId", formulario.Erros.Keys);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{linha.EmailCorporativo}'"));
        Assert.Equal(93_000, await ContadorAsync(api)); // as caixas são conferidas ANTES de reservar a matrícula
    }

    [Fact]
    public async Task Cadastrar_com_as_caixas_atendidas_por_fakes_nao_enfileira_outro_email_alem_do_convite()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "caixas-ok");
        await DefinirContadorAsync(api, 94_000);
        var linhas = new[] { Novo(cargo, setor), Novo(cargo, setor) };

        var resultado = await api.NaTransacaoComAsync(
            servicos =>
            {
                servicos.AddScoped<IInclusaoNoOnboarding, OnboardingQueAtende>();
                servicos.AddScoped<IEnvioDoFormularioDeAdmissao, FormularioQueAtende>();
            },
            sp => sp.GetRequiredService<ICadastroDeFuncionarios>().CadastrarAsync(linhas, new OpcoesDoCadastro(true, true, 11), Ct));

        Assert.Equal((2, 2, 2), (resultado.ConvitesEnviados, resultado.IncluidosNoOnboarding, resultado.IncluidosNoFormularioDeAdmissao));
        var ids = string.Join(",", resultado.Cadastrados.Select(c => c.Id));
        Assert.Equal(2, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE ID_FUNCIONARIO IN ({ids})"));
        Assert.Equal(2, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE' AND ID_FUNCIONARIO IN ({ids})")); // a lista fechada: só o convite
    }

    [Fact]
    public async Task Dois_cadastros_simultaneos_do_mesmo_CPF_gravam_um_so_e_o_outro_recebe_409_sem_matricula_repetida()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "simultaneo");
        await DefinirContadorAsync(api, 70_000);
        var cpf = CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf));
        var largada = new TaskCompletionSource();
        var tarefas = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            await largada.Task;
            try
            {
                var resultado = await CadastrarAsync(api, [Novo(cargo, setor, cpf: cpf, email: $"simultaneo.{i}@cadastro.com.br")]);
                return new Desfecho(true, null, resultado.Cadastrados[0].Matricula);
            }
            catch (Exception erro) when (erro is CadastroInvalidoException or LinhaRecusadaException)
            {
                return new Desfecho(false, erro, null);
            }
        })).ToList();

        largada.SetResult();
        var resultados = await Task.WhenAll(tarefas); // nenhuma lançou outra coisa (um 500 faria o WhenAll falhar)

        Assert.Equal(1, resultados.Count(r => r.Ok)); // só um grava
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES WHERE CPF = '{cpf}'"));
        Assert.Equal(70_001, await ContadorAsync(api)); // as matrículas dos 19 que perderam voltaram: nenhuma repetida, nenhum buraco
        Assert.Equal("70001", resultados.Single(r => r.Ok).Matricula);
        Assert.All(resultados.Where(r => !r.Ok), r => Assert.True(r.Excecao is CadastroInvalidoException or LinhaRecusadaException));
    }

    private sealed record Desfecho(bool Ok, Exception? Excecao, string? Matricula);

    private sealed class OnboardingQueFalhaDepois : IInclusaoNoOnboarding
    {
        public Task ConferirAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<int> IncluirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct) => throw new InvalidOperationException("onboarding fora do ar (falha de teste)");
    }

    private sealed class FormularioQueFalhaDepois : IEnvioDoFormularioDeAdmissao
    {
        public Task ConferirAsync(int formularioId, CancellationToken ct) => Task.CompletedTask;

        public Task<int> AdicionarDestinatariosAsync(int formularioId, IReadOnlyList<int> funcionarioIds, CancellationToken ct) => throw new InvalidOperationException("formulário fora do ar (falha de teste)");
    }

    private sealed class OnboardingQueAtende : IInclusaoNoOnboarding
    {
        public Task ConferirAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<int> IncluirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct) => Task.FromResult(funcionarioIds.Count);
    }

    private sealed class FormularioQueAtende : IEnvioDoFormularioDeAdmissao
    {
        public Task ConferirAsync(int formularioId, CancellationToken ct) => Task.CompletedTask;

        public Task<int> AdicionarDestinatariosAsync(int formularioId, IReadOnlyList<int> funcionarioIds, CancellationToken ct) => Task.FromResult(funcionarioIds.Count);
    }
}
