using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary><c>POST /api/rh/funcionarios</c> (tela 02) pela API de verdade, no banco REAL com o relógio em 2026-10-07T12:42Z.</summary>
[Collection(MySqlCollection.Nome)]
public class CriarFuncionarioTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/funcionarios";

    private static int _cpf = 300_000;
    private static int _email;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(CriarFuncionarioTests));

    private static string CpfNovo() => CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf));

    private static string EmailNovo() => $"criar.{Interlocked.Increment(ref _email)}@criar.com.br";

    private static object Corpo(int cargo, int setor, string? cpf = null, string? email = null, string nome = "Rafael Torres", string admissao = "2026-02-03", string perfil = "Funcionario", int[]? beneficios = null, bool? convite = null, bool? onboarding = null, int? formulario = null)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["nome"] = nome,
            ["cpf"] = cpf ?? CpfNovo(),
            ["dataNascimento"] = "1995-02-09",
            ["telefone"] = "11991234567",
            ["emailCorporativo"] = email ?? EmailNovo(),
            ["dataAdmissao"] = admissao,
            ["tipoContrato"] = "CLT",
            ["cargoId"] = cargo,
            ["setorId"] = setor,
            ["perfil"] = perfil,
        };
        if (beneficios is not null)
        {
            corpo["beneficioIds"] = beneficios;
        }

        if (convite is not null)
        {
            corpo["enviarConvite"] = convite;
        }

        if (onboarding is not null)
        {
            corpo["incluirNoOnboarding"] = onboarding;
        }

        if (formulario is not null)
        {
            corpo["formularioAdmissaoId"] = formulario;
        }

        return corpo;
    }

    private static async Task<(int Cargo, int Setor)> CargoESetorAsync(ApiDeAcesso api, string sufixo, int? gestorId = null) =>
        (await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo {sufixo}", 44m), await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor {sufixo}", gestorId));

    private static async Task<(int FuncionarioId, int GestorId)> GestorAsync(ApiDeAcesso api, string nome, bool ativo = true)
    {
        var funcionario = await SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, EmailNovo(), new DateOnly(2025, 1, 6), nivel: "Funcionario Gestor");
        await api.NovoGestorAsync(funcionario);
        var gestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionario}");
        if (!ativo)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {funcionario}");
        }

        return (funcionario, gestor);
    }

    [Fact]
    public async Task Criar_devolve_201_com_a_ficha_matricula_gerada_gestor_do_setor_beneficios_e_acesso_CONVITE_ENVIADO()
    {
        var api = await ApiAsync();
        var (gestorFunc, gestor) = await GestorAsync(api, "Gestor criar1");
        var (cargo, setor) = await CargoESetorAsync(api, "criar1", gestor);
        var refeicao = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Vale-refeição criar1");
        var saude = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Plano de saúde criar1");
        var cpf = CpfNovo();
        var email = EmailNovo();

        var resposta = await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota, Corpo(cargo, setor, cpf, email, beneficios: [saude, refeicao]));

        Assert.Equal(201, (int)resposta.StatusCode);
        var ficha = await CorpoAsync(resposta);
        var id = ficha.GetProperty("id").GetInt32();
        Assert.Equal($"{Rota}/{id}", resposta.Headers.Location?.ToString());
        Assert.Matches("^[0-9]{5}$", ficha.GetProperty("matricula").GetString()!);
        Assert.Equal(("Rafael Torres", email, cpf, "11991234567"), (ficha.GetProperty("nome").GetString(), ficha.GetProperty("emailCorporativo").GetString(), ficha.GetProperty("cpf").GetString(), ficha.GetProperty("telefone").GetString()));
        Assert.Equal(("1995-02-09", "2026-02-03", "CLT", "Funcionario", "ATIVO"), (ficha.GetProperty("dataNascimento").GetString(), ficha.GetProperty("dataAdmissao").GetString(), ficha.GetProperty("tipoContrato").GetString(), ficha.GetProperty("perfil").GetString(), ficha.GetProperty("situacao").GetString()));
        var gestorDaFicha = ficha.GetProperty("gestor");
        Assert.Equal((gestor, gestorFunc, "Gestor criar1"), (gestorDaFicha.GetProperty("gestorId").GetInt32(), gestorDaFicha.GetProperty("funcionarioId").GetInt32(), gestorDaFicha.GetProperty("nome").GetString()));
        Assert.Equal(44.0, ficha.GetProperty("jornadaSemanalHoras").GetDouble());
        Assert.Equal([refeicao, saude], ficha.GetProperty("beneficios").EnumerateArray().Select(b => b.GetProperty("beneficioId").GetInt32()));
        Assert.Equal(("CONVITE_ENVIADO", true), (ficha.GetProperty("acesso").GetProperty("situacao").GetString(), ficha.GetProperty("acesso").GetProperty("ativo").GetBoolean()));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE' AND ID_FUNCIONARIO = {id}"));
    }

    [Fact]
    public async Task Criar_sem_convite_deixa_acesso_SEM_CONVITE_e_nao_enfileira_email()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar2");

        var resposta = await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota, Corpo(cargo, setor, convite: false));

        Assert.Equal(201, (int)resposta.StatusCode);
        var ficha = await CorpoAsync(resposta);
        var id = ficha.GetProperty("id").GetInt32();
        Assert.Equal(("SEM_CONVITE", false), (ficha.GetProperty("acesso").GetProperty("situacao").GetString(), ficha.GetProperty("acesso").GetProperty("ativo").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("gestor").ValueKind); // setor sem gestor
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {id}"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {id}"));
    }

    [Fact]
    public async Task Criar_com_CPF_ou_email_de_cadastro_ativo_devolve_409_com_cadastroExistente_ativo_true()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar3");
        var existente = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Bruno Lima", "bruno.criar3@criar.com.br", new DateOnly(2026, 1, 5), setorId: setor);
        var cpfDele = CpfNovo();
        await SementeDoCadastro.DetalhesAsync(api.Conexao, existente, cpfDele);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var porCpf = await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, cpf: cpfDele));
        var porEmail = await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, email: "BRUNO.criar3@criar.com.br"));

        foreach (var (resposta, codigo, detalhe) in new[] { (porCpf, "CPF_JA_CADASTRADO", "Já existe um colaborador com este CPF."), (porEmail, "EMAIL_JA_CADASTRADO", "Já existe um colaborador com este e-mail corporativo.") })
        {
            Assert.Equal(409, (int)resposta.StatusCode);
            var problema = await CorpoAsync(resposta);
            Assert.Equal(("Conflito.", detalhe, codigo), (problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString(), problema.GetProperty("codigo").GetString()));
            var existenteNoCorpo = problema.GetProperty("cadastroExistente");
            Assert.Equal((existente, "Bruno Lima", true), (existenteNoCorpo.GetProperty("funcionarioId").GetInt32(), existenteNoCorpo.GetProperty("nome").GetString(), existenteNoCorpo.GetProperty("ativo").GetBoolean()));
        }
    }

    [Fact]
    public async Task Criar_com_CPF_ou_email_de_cadastro_inativo_devolve_409_que_manda_usar_Readmitir_com_ativo_false()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar4");
        var inativo = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Henrique Alves", "henrique.criar4@criar.com.br", new DateOnly(2026, 1, 5), ativo: false, setorId: setor);
        var cpfDele = CpfNovo();
        await SementeDoCadastro.DetalhesAsync(api.Conexao, inativo, cpfDele);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var porCpf = await CorpoAsync(await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, cpf: cpfDele)));
        var porEmail = await CorpoAsync(await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, email: "henrique.criar4@criar.com.br")));

        Assert.Equal("Este CPF pertence a um cadastro inativo (Henrique Alves). Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.", porCpf.GetProperty("detail").GetString());
        Assert.Equal("Este e-mail corporativo pertence a um cadastro inativo (Henrique Alves). Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.", porEmail.GetProperty("detail").GetString());
        Assert.Equal(("CPF_JA_CADASTRADO", false, inativo), (porCpf.GetProperty("codigo").GetString(), porCpf.GetProperty("cadastroExistente").GetProperty("ativo").GetBoolean(), porCpf.GetProperty("cadastroExistente").GetProperty("funcionarioId").GetInt32()));
        Assert.Equal(("EMAIL_JA_CADASTRADO", false), (porEmail.GetProperty("codigo").GetString(), porEmail.GetProperty("cadastroExistente").GetProperty("ativo").GetBoolean()));
    }

    [Fact]
    public async Task Criar_com_gestor_do_setor_inativo_devolve_422_GESTOR_DO_SETOR_INATIVO_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var (_, gestorInativo) = await GestorAsync(api, "Gestor inativo criar5", ativo: false);
        var (cargo, setor) = await CargoESetorAsync(api, "criar5", gestorInativo);
        var email = EmailNovo();

        var resposta = await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota, Corpo(cargo, setor, email: email));

        Assert.Equal(422, (int)resposta.StatusCode);
        var problema = await CorpoAsync(resposta);
        Assert.Equal(("Operação recusada.", "O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar.", "GESTOR_DO_SETOR_INATIVO"), (problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString(), problema.GetProperty("codigo").GetString()));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'"));
    }

    [Fact]
    public async Task Criar_com_dataAdmissao_amanha_as_22h_de_Brasilia_fica_EM_ADMISSAO()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar6");
        try
        {
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 1, 0, 0, DateTimeKind.Utc)); // 22:00 de 07/10 em Brasília
            var cliente = await api.ClienteDoRhAgoraAsync();

            var amanha = await CorpoAsync(await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, admissao: "2026-10-08")));
            var hoje = await CorpoAsync(await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, admissao: "2026-10-07")));

            Assert.Equal(("EM_ADMISSAO", true), (amanha.GetProperty("situacao").GetString(), amanha.GetProperty("ativo").GetBoolean()));
            Assert.Equal(JsonValueKind.Null, amanha.GetProperty("situacaoHoje").ValueKind);
            Assert.Equal("ATIVO", hoje.GetProperty("situacao").GetString()); // 22h de 07/10 em Brasília ainda é 07/10
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Theory]
    [InlineData("Gestor", "Funcionario Gestor")]
    [InlineData("RH", "RH")]
    [InlineData("Funcionario", "Funcionario")]
    public async Task Criar_com_perfil_Gestor_e_com_perfil_RH_grava_o_nivel_certo(string perfil, string nivelNoBanco)
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, $"criar7-{perfil}");

        var ficha = await CorpoAsync(await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota, Corpo(cargo, setor, perfil: perfil)));

        Assert.Equal(perfil, ficha.GetProperty("perfil").GetString());
        var id = ficha.GetProperty("id").GetInt32();
        Assert.Equal(nivelNoBanco, (await Sql.EscalarAsync(api.Conexao, $"SELECT N.NOME_NIVEL_ACESSO FROM FUNCIONARIOS F JOIN NIVEIS_DE_ACESSO N ON N.ID_NIVEL_ACESSO = F.ID_NIVEL_ACESSO WHERE F.ID_FUNCIONARIO = {id}"))!.ToString());
    }

    [Fact]
    public async Task Criar_grava_Colaboradores_Criou_pelo_interceptor_sem_CPF_e_o_convite_como_Acesso_Criou()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar8");
        var cpf = CpfNovo();

        var ficha = await CorpoAsync(await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota, Corpo(cargo, setor, cpf: cpf)));

        var id = ficha.GetProperty("id").GetInt32();
        var doColaborador = await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", id);
        var criou = Assert.Single(doColaborador, e => e.Acao == AuditoriaAcao.Criou);
        Assert.Equal((AuditoriaArea.Colaboradores, api.Elenco.Rh.FuncionarioId), (criou.Area, criou.FuncionarioAtorId));
        var usuarioId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {id}");
        var doConvite = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", usuarioId));
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Criou), (doConvite.Area, doConvite.Acao));
        foreach (var tabela in new[] { "AUDITORIA_EVENTOS", "AUDITORIA_MUDANCAS" })
        {
            var texto = await Sql.TextoDaTabelaAsync(api.Conexao, tabela);
            Assert.DoesNotContain(cpf, texto, StringComparison.Ordinal); // o log nunca leva o CPF
            Assert.DoesNotContain("1995-02-09", texto, StringComparison.Ordinal); // nem a data de nascimento (campo sensível)
        }
    }

    [Fact]
    public async Task Criar_com_corpo_vazio_ou_enum_invalido_devolve_400_e_nome_de_150_e_151_caracteres_passam_e_falham()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar9");
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var vazio = await cliente.PostAsync(Rota, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
        var semCampos = await cliente.PostAsJsonAsync(Rota, new { });
        var enumInvalido = await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, perfil: "Presidente"));
        var com150 = await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, nome: new string('a', 150)));
        var com151 = await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor, nome: new string('a', 151)));

        Assert.Equal(400, (int)vazio.StatusCode);
        Assert.Equal(400, (int)semCampos.StatusCode);
        var errosDoCorpoVazio = (await CorpoAsync(semCampos)).GetProperty("errors"); // todos os campos obrigatórios juntos
        foreach (var campo in new[] { "nome", "cpf", "dataNascimento", "telefone", "emailCorporativo", "dataAdmissao", "tipoContrato", "cargoId", "setorId", "perfil" })
        {
            Assert.True(errosDoCorpoVazio.TryGetProperty(campo, out _), campo);
        }

        Assert.Equal(400, (int)enumInvalido.StatusCode);
        Assert.Equal(201, (int)com150.StatusCode);
        Assert.Equal(400, (int)com151.StatusCode);
        Assert.True((await CorpoAsync(com151)).GetProperty("errors").TryGetProperty("nome", out _));
    }

    [Fact]
    public async Task Criar_com_beneficioIds_null_ou_omitido_vale_como_sem_beneficios_e_o_ficha_nao_tem_beneficio_ativo()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar10");
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);
        var comNull = (Dictionary<string, object?>)Corpo(cargo, setor);
        comNull["beneficioIds"] = null;

        var resposta = await cliente.PostAsJsonAsync(Rota, comNull);
        var omitido = await cliente.PostAsJsonAsync(Rota, Corpo(cargo, setor));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(201, (int)omitido.StatusCode);
        var ids = new[] { (await CorpoAsync(resposta)).GetProperty("id").GetInt32(), (await CorpoAsync(omitido)).GetProperty("id").GetInt32() };
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO IN ({string.Join(",", ids)})"));
    }

    [Fact]
    public async Task Criar_com_perfil_errado_403_e_anonimo_401_e_a_resposta_bate_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "criar10");
        var corpo = Corpo(cargo, setor);

        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.Gestor).PostAsJsonAsync(Rota, corpo)).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.MembroA).PostAsJsonAsync(Rota, corpo)).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().PostAsJsonAsync(Rota, corpo)).StatusCode);

        var rh = api.Api.ClienteComo(api.Elenco.Rh);
        var existente = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Existente criar10", EmailNovo(), new DateOnly(2026, 1, 5), setorId: setor);
        var cpfDele = CpfNovo();
        await SementeDoCadastro.DetalhesAsync(api.Conexao, existente, cpfDele);
        await (await rh.PostAsJsonAsync(Rota, Corpo(cargo, setor, convite: true))).AfirmarConformeAoContratoAsync("criarFuncionario"); // o 201
        await (await rh.PostAsJsonAsync(Rota, Corpo(cargo, setor, cpf: cpfDele))).AfirmarConformeAoContratoAsync("criarFuncionario"); // o 409
        await (await rh.PostAsJsonAsync(Rota, Corpo(cargo, setor, nome: ""))).AfirmarConformeAoContratoAsync("criarFuncionario"); // o 400
        await (await rh.PostAsJsonAsync(Rota, Corpo(cargo, setor, onboarding: true))).AfirmarConformeAoContratoAsync("criarFuncionario"); // o 422 SEM_TREINAMENTO_DE_ONBOARDING
        await (await rh.PostAsJsonAsync(Rota, Corpo(cargo, setor, formulario: 11))).AfirmarConformeAoContratoAsync("criarFuncionario"); // o 400 em formularioAdmissaoId
    }
}
