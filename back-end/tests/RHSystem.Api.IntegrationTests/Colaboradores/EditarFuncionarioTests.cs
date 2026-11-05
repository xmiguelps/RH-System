using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary><c>PUT /api/rh/funcionarios/{id}</c> (tela 03, "Editar dados") pela API de verdade, no banco REAL com o relógio em 2026-10-07T12:42Z.</summary>
[Collection(MySqlCollection.Nome)]
public class EditarFuncionarioTests(MySqlFixture mysql)
{
    private static int _cpf = 400_000;
    private static int _sequencia;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(EditarFuncionarioTests));

    private static string Rota(int id) => $"/api/rh/funcionarios/{id}";

    private static string CpfNovo() => CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf));

    private static string EmailNovo() => $"editar.{Interlocked.Increment(ref _sequencia)}@editar.com.br";

    private static string MatriculaNova() => (91_000 + Interlocked.Increment(ref _sequencia)).ToString(CultureInfo.InvariantCulture);

    private sealed record Pessoa(int Id, string Nome, string Email, string Cpf, int Cargo, int Setor);

    private static async Task<(int Cargo, int Setor)> CargoESetorAsync(ApiDeAcesso api, string sufixo, int? gestorId = null) =>
        (await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo editar {sufixo}", 44m), await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor editar {sufixo}", gestorId));

    /// <summary>Um colaborador por SQL, nascido em 1990-06-18, admitido em 2026-01-05, telefone 11987654410: o corpo padrão de <see cref="Corpo"/> não muda nada nele.</summary>
    private static async Task<Pessoa> PessoaAsync(
        ApiDeAcesso api,
        string sufixo,
        (int Cargo, int Setor)? lugar = null,
        int? gestorId = null,
        bool ativo = true,
        string nivel = "Funcionario",
        bool comDetalhes = true,
        bool comMatricula = true)
    {
        var (cargo, setor) = lugar ?? await CargoESetorAsync(api, sufixo);
        var nome = $"Pessoa {sufixo}";
        var email = EmailNovo();
        var cpf = CpfNovo();
        var id = await SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, email, new DateOnly(2026, 1, 5), ativo, nivel, setor, cargo, gestorId, matricula: comMatricula ? MatriculaNova() : null, nascimento: new DateOnly(1990, 6, 18));
        if (comDetalhes)
        {
            await SementeDoCadastro.DetalhesAsync(api.Conexao, id, cpf);
        }

        return new Pessoa(id, nome, email, cpf, cargo, setor);
    }

    /// <summary>Uma pessoa que é gestora (linha em <c>GESTORES</c>) e o <c>ID_GESTOR</c> dela.</summary>
    private static async Task<(Pessoa Pessoa, int GestorId)> GestoraAsync(ApiDeAcesso api, string sufixo, bool ativo = true)
    {
        var pessoa = await PessoaAsync(api, sufixo, nivel: "Funcionario Gestor");
        await api.NovoGestorAsync(pessoa.Id);
        var gestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {pessoa.Id}");
        if (!ativo)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {pessoa.Id}");
        }

        return (pessoa, gestor);
    }

    private static Dictionary<string, object?> Corpo(
        Pessoa p,
        int? cargo = null,
        int? setor = null,
        string? cpf = null,
        string? email = null,
        string? nome = null,
        string perfil = "Funcionario",
        int[]? beneficios = null,
        string admissao = "2026-01-05") =>
        new()
        {
            ["nome"] = nome ?? p.Nome,
            ["cpf"] = cpf ?? p.Cpf,
            ["dataNascimento"] = "1990-06-18",
            ["telefone"] = "11987654410",
            ["emailCorporativo"] = email ?? p.Email,
            ["emailPessoal"] = null,
            ["rg"] = null,
            ["rgOrgaoEmissor"] = null,
            ["endereco"] = Endereco(),
            ["dataAdmissao"] = admissao,
            ["tipoContrato"] = "CLT",
            ["cargoId"] = cargo ?? p.Cargo,
            ["setorId"] = setor ?? p.Setor,
            ["perfil"] = perfil,
            ["beneficioIds"] = beneficios ?? [],
        };

    private static Dictionary<string, object?> Endereco(string? cep = null, string? uf = null) =>
        new() { ["cep"] = cep, ["logradouro"] = null, ["numero"] = null, ["complemento"] = null, ["bairro"] = null, ["cidade"] = null, ["uf"] = uf };

    private static Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, int id, object corpo) => api.Api.ClienteComo(api.Elenco.Rh).PutAsJsonAsync(Rota(id), corpo);

    /// <summary>A linha de <c>BENEFICIOS_FUNCIONARIOS</c> de (pessoa, benefício): o id, se está ativa e o status; <c>null</c> se não há linha.</summary>
    private static async Task<(long Id, bool Ativo, string Status)?> LinhaAsync(ApiDeAcesso api, int pessoa, int beneficio)
    {
        var texto = await Sql.EscalarAsync(api.Conexao, $"SELECT CONCAT(ID_BENEFICIO_FUNCIONARIOS, '|', BENEFICIO_ATIVO, '|', STATUS_PEDIDO) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ID_BENEFICIO = {beneficio}") as string;
        if (texto is null)
        {
            return null;
        }

        var partes = texto.Split('|');
        return (long.Parse(partes[0], CultureInfo.InvariantCulture), partes[1] == "1", partes[2]);
    }

    private static async Task<int> LinhasDoBeneficioAsync(ApiDeAcesso api, int pessoa, int beneficio) =>
        (int)await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ID_BENEFICIO = {beneficio}");

    /// <summary>Uma linha de benefício DESATIVADA no status dado. O banco exige que o <c>RECUSADO</c> traga motivo, quem respondeu e quando (<c>CHK_BENEFICIOS_FUNCIONARIOS_RECUSA_COMPLETA</c>).</summary>
    private static async Task LinhaDeStatusAsync(ApiDeAcesso api, int pessoa, int beneficio, string status)
    {
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa, beneficio, ativo: false, status: status == "RECUSADO" ? "PENDENTE" : status);
        if (status == "RECUSADO")
        {
            await Sql.ExecutarAsync(
                api.Conexao,
                $"UPDATE BENEFICIOS_FUNCIONARIOS SET STATUS_PEDIDO = 'RECUSADO', MOTIVO_RECUSA = 'Sem verba', ID_FUNCIONARIO_RH = {api.Elenco.Rh.FuncionarioId}, DATA_RESPOSTA = '2026-10-02 10:00:00' WHERE ID_FUNCIONARIO = {pessoa} AND ID_BENEFICIO = {beneficio}");
        }
    }

    [Fact]
    public async Task Editar_devolve_200_com_a_ficha_atualizada_e_troca_do_email_troca_o_login()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "editar1");
        var pessoa = await api.NovaPessoaAsync("editar1", login: TipoDeLogin.SenhaDefinida);
        var novoEmail = EmailNovo();
        var cpf = CpfNovo();
        var corpo = Corpo(new Pessoa(pessoa.FuncionarioId, pessoa.Nome, pessoa.Email!, cpf, cargo, setor), email: novoEmail, nome: "Nome Novo editar1");

        var resposta = await EditarAsync(api, pessoa.FuncionarioId, corpo);

        Assert.Equal(200, (int)resposta.StatusCode);
        var ficha = await CorpoAsync(resposta);
        Assert.Equal((pessoa.FuncionarioId, "Nome Novo editar1", novoEmail, cpf), (ficha.GetProperty("id").GetInt32(), ficha.GetProperty("nome").GetString(), ficha.GetProperty("emailCorporativo").GetString(), ficha.GetProperty("cpf").GetString()));
        Assert.Equal(("1990-06-18", "2026-01-05", "CLT", "Funcionario"), (ficha.GetProperty("dataNascimento").GetString(), ficha.GetProperty("dataAdmissao").GetString(), ficha.GetProperty("tipoContrato").GetString(), ficha.GetProperty("perfil").GetString()));
        Assert.Equal((cargo, setor), (ficha.GetProperty("cargo").GetProperty("id").GetInt32(), ficha.GetProperty("setor").GetProperty("id").GetInt32()));
        var anonimo = api.Api.ClienteAnonimo();
        Assert.Equal(200, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = novoEmail, senha = ApiFactory.SenhaPadrao })).StatusCode); // o novo e-mail entra
        Assert.Equal(401, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = pessoa.Email, senha = ApiFactory.SenhaPadrao })).StatusCode); // o antigo não
    }

    [Fact]
    public async Task Editar_troca_de_setor_refaz_o_gestor_e_se_o_gestor_do_novo_setor_e_a_pessoa_ela_fica_sem_gestor()
    {
        var api = await ApiAsync();
        var (gestora, gestorA) = await GestoraAsync(api, "Gestora A editar2");
        var (_, gestorB) = await GestoraAsync(api, "Gestora B editar2");
        var (cargo, setorA) = await CargoESetorAsync(api, "editar2a", gestorA);
        var setorB = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor editar editar2b", gestorB);
        var comum = await PessoaAsync(api, "editar2 comum", (cargo, setorA), gestorId: gestorA);

        var depoisDaTroca = await CorpoAsync(await EditarAsync(api, comum.Id, Corpo(comum, setor: setorB)));

        Assert.Equal(gestorB, depoisDaTroca.GetProperty("gestor").GetProperty("gestorId").GetInt32());
        Assert.Equal(setorB, depoisDaTroca.GetProperty("setor").GetProperty("id").GetInt32());

        // A gestora do setor C é a própria pessoa: ela passa para o setor C e fica sem gestor (o banco recusa "gestor de si mesmo").
        var (propria, gestoraC) = await GestoraAsync(api, "Gestora C editar2");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setorA}, ID_GESTOR = {gestorA} WHERE ID_FUNCIONARIO = {propria.Id}");
        var setorC = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor editar editar2c", gestoraC);

        var propriaDepois = await CorpoAsync(await EditarAsync(api, propria.Id, Corpo(propria, cargo: cargo, setor: setorC, perfil: "Gestor")));

        Assert.Equal(setorC, propriaDepois.GetProperty("setor").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, propriaDepois.GetProperty("gestor").ValueKind);
        _ = gestora;
    }

    [Fact]
    public async Task Editar_beneficioIds_ativa_os_que_entram_e_desativa_so_os_APROVADO_ativos_que_saem()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "editar3");
        var fica = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Fica editar3");
        var sai = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Sai editar3");
        var entra = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Entra editar3");
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa.Id, fica);
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa.Id, sai);
        var linhaDoQueFica = await LinhaAsync(api, pessoa.Id, fica);

        var resposta = await EditarAsync(api, pessoa.Id, Corpo(pessoa, beneficios: [fica, entra]));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(linhaDoQueFica, await LinhaAsync(api, pessoa.Id, fica)); // o que já estava ativo não muda
        var saiu = await LinhaAsync(api, pessoa.Id, sai);
        Assert.Equal((false, "APROVADO"), (saiu!.Value.Ativo, saiu.Value.Status)); // desativado: a linha continua
        var entrou = await LinhaAsync(api, pessoa.Id, entra);
        Assert.Equal((true, "APROVADO"), (entrou!.Value.Ativo, entrou.Value.Status));
        var ficha = await CorpoAsync(resposta);
        Assert.Equal(new[] { fica, entra }.Order(), ficha.GetProperty("beneficios").EnumerateArray().Select(b => b.GetProperty("beneficioId").GetInt32()).Order());
    }

    [Theory]
    [InlineData("PENDENTE")]
    [InlineData("RECUSADO")]
    [InlineData("CANCELADO")]
    public async Task Editar_dando_beneficio_que_ja_tem_linha_PENDENTE_RECUSADO_ou_CANCELADO_atualiza_a_mesma_linha_para_ativa_e_aprovada_sem_inserir_outra(string status)
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, $"editar4 {status}");
        var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, $"Beneficio editar4 {status}");
        await LinhaDeStatusAsync(api, pessoa.Id, beneficio, status);
        var antes = await LinhaAsync(api, pessoa.Id, beneficio);

        var resposta = await EditarAsync(api, pessoa.Id, Corpo(pessoa, beneficios: [beneficio]));

        Assert.Equal(200, (int)resposta.StatusCode);
        var depois = await LinhaAsync(api, pessoa.Id, beneficio);
        Assert.Equal((antes!.Value.Id, true, "APROVADO"), (depois!.Value.Id, depois.Value.Ativo, depois.Value.Status)); // a MESMA linha
        Assert.Equal(1, await LinhasDoBeneficioAsync(api, pessoa.Id, beneficio)); // nunca uma segunda
    }

    [Fact]
    public async Task Editar_nao_toca_em_linha_PENDENTE_RECUSADO_ou_CANCELADO_de_beneficio_que_fica_fora_do_conjunto()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "editar5");
        var status = new[] { "PENDENTE", "RECUSADO", "CANCELADO" };
        var beneficios = new List<int>();
        foreach (var s in status)
        {
            var id = await SementeDoCadastro.BeneficioAsync(api.Conexao, $"Beneficio editar5 {s}");
            await LinhaDeStatusAsync(api, pessoa.Id, id, s);
            beneficios.Add(id);
        }

        var antes = new List<(long Id, bool Ativo, string Status)?>();
        foreach (var id in beneficios)
        {
            antes.Add(await LinhaAsync(api, pessoa.Id, id));
        }

        var resposta = await EditarAsync(api, pessoa.Id, Corpo(pessoa, beneficios: []));

        Assert.Equal(200, (int)resposta.StatusCode);
        for (var i = 0; i < beneficios.Count; i++)
        {
            Assert.Equal(antes[i], await LinhaAsync(api, pessoa.Id, beneficios[i]));
        }
    }

    [Fact]
    public async Task Editar_dando_beneficio_com_linha_APROVADO_desativada_reativa_a_mesma_linha()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "editar6");
        var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Beneficio editar6");
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa.Id, beneficio, ativo: false);
        var antes = await LinhaAsync(api, pessoa.Id, beneficio);

        var resposta = await EditarAsync(api, pessoa.Id, Corpo(pessoa, beneficios: [beneficio]));

        Assert.Equal(200, (int)resposta.StatusCode);
        var depois = await LinhaAsync(api, pessoa.Id, beneficio);
        Assert.Equal((antes!.Value.Id, true, "APROVADO"), (depois!.Value.Id, depois.Value.Ativo, depois.Value.Status));
        Assert.Equal(1, await LinhasDoBeneficioAsync(api, pessoa.Id, beneficio));
    }

    [Fact]
    public async Task Editar_o_admin_do_seed_cria_os_dados_pessoais_e_recebe_a_proxima_matricula()
    {
        var api = await ApiAsync();
        var admin = await PessoaAsync(api, "editar7 admin", comDetalhes: false, comMatricula: false); // como o administrador do seed: sem detalhes e sem matrícula
        await Sql.ExecutarAsync(api.Conexao, "UPDATE SEQUENCIAS SET ULTIMO_VALOR = 70000 WHERE NOME = 'MATRICULA'");

        var resposta = await EditarAsync(api, admin.Id, Corpo(admin));

        Assert.Equal(200, (int)resposta.StatusCode);
        var ficha = await CorpoAsync(resposta);
        Assert.Equal(("70001", admin.Cpf, "11987654410"), (ficha.GetProperty("matricula").GetString(), ficha.GetProperty("cpf").GetString(), ficha.GetProperty("telefone").GetString()));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES WHERE ID_FUNCIONARIO = {admin.Id}"));
        Assert.Equal(70_001, await Sql.ContarAsync(api.Conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));

        // Quem já tem matrícula não recebe outra: a segunda edição não mexe no contador.
        Assert.Equal(200, (int)(await EditarAsync(api, admin.Id, Corpo(admin))).StatusCode);
        Assert.Equal(70_001, await Sql.ContarAsync(api.Conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));
    }

    [Fact]
    public async Task Editar_dados_pessoais_grava_evento_com_valorOculto_e_sem_antes_nem_depois_e_cargo_pelo_nome()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "editar8");
        var cargoNovo = await SementeDoCadastro.CargoAsync(api.Conexao, $"{pessoa.Nome} pleno", 40m);
        var cpfNovo = CpfNovo();
        var corpo = Corpo(pessoa, cargo: cargoNovo, cpf: cpfNovo);
        corpo["telefone"] = "11999998888";
        corpo["emailPessoal"] = "pessoal.editar8@exemplo.com";
        corpo["rg"] = "45.678.912-3";
        corpo["endereco"] = Endereco(cep: "01310100", uf: "SP");

        Assert.Equal(200, (int)(await EditarAsync(api, pessoa.Id, corpo)).StatusCode);

        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", pessoa.Id);
        var pessoais = Assert.Single(eventos, e => e.Descricao == $"Dados pessoais de {pessoa.Nome}");
        Assert.Equal((AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou, api.Elenco.Rh.FuncionarioId), (pessoais.Area, pessoais.Acao, pessoais.FuncionarioAtorId));
        Assert.Equal(["CPF", "E-mail pessoal", "Endereço", "RG", "Telefone"], pessoais.Mudancas.Select(m => m.Campo).Order());
        Assert.All(pessoais.Mudancas, m => Assert.Equal((true, (string?)null, (string?)null), (m.ValorOculto, m.ValorAntes, m.ValorDepois)));

        var doCargo = Assert.Single(eventos, e => e.Descricao == $"Cargo de {pessoa.Nome}");
        var mudancaDoCargo = Assert.Single(doCargo.Mudancas);
        Assert.Equal(("Cargo", $"Cargo editar {pessoa.Nome.Replace("Pessoa ", string.Empty, StringComparison.Ordinal)}", $"{pessoa.Nome} pleno", false), (mudancaDoCargo.Campo, mudancaDoCargo.ValorAntes, mudancaDoCargo.ValorDepois, mudancaDoCargo.ValorOculto));

        foreach (var tabela in new[] { "AUDITORIA_EVENTOS", "AUDITORIA_MUDANCAS" })
        {
            var texto = await Sql.TextoDaTabelaAsync(api.Conexao, tabela);
            foreach (var segredo in new[] { pessoa.Cpf, cpfNovo, "11999998888", "pessoal.editar8@exemplo.com", "45.678.912-3", "01310100", "1990-06-18" })
            {
                Assert.DoesNotContain(segredo, texto, StringComparison.Ordinal);
            }
        }

        // Mandar de novo o que já está gravado não gera outro evento de dados pessoais.
        Assert.Equal(200, (int)(await EditarAsync(api, pessoa.Id, corpo)).StatusCode);
        Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", pessoa.Id), e => e.Descricao == $"Dados pessoais de {pessoa.Nome}");
    }

    [Fact]
    public async Task Editar_o_proprio_perfil_devolve_422_NAO_PODE_ALTERAR_O_PROPRIO_PERFIL()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api, "editar9");
        var rhId = api.Elenco.Rh.FuncionarioId;
        var nome = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT NOME FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {rhId}"))!;
        var email = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {rhId}"))!;
        var eu = new Pessoa(rhId, nome, email, CpfNovo(), cargo, setor);

        var recusada = await EditarAsync(api, rhId, Corpo(eu, perfil: "Gestor"));

        Assert.Equal(422, (int)recusada.StatusCode);
        var problema = await CorpoAsync(recusada);
        Assert.Equal(("NAO_PODE_ALTERAR_O_PROPRIO_PERFIL", "Você não pode alterar o seu próprio nível de acesso."), (problema.GetProperty("codigo").GetString(), problema.GetProperty("detail").GetString()));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES WHERE ID_FUNCIONARIO = {rhId} AND CPF = '{eu.Cpf}'")); // nada foi gravado

        // Mandar o mesmo perfil (RH) é editar os outros campos: passa.
        Assert.Equal(200, (int)(await EditarAsync(api, rhId, Corpo(eu, perfil: "RH"))).StatusCode);
    }

    [Fact]
    public async Task Editar_o_perfil_de_quem_gerencia_setor_devolve_422_GESTOR_PRECISA_TER_PERFIL_GESTOR()
    {
        var api = await ApiAsync();
        var (gestora, gestorId) = await GestoraAsync(api, "Gestora editar10");
        await SementeDoCadastro.SetorAsync(api.Conexao, "Setor gerido editar10", gestorId);

        var recusada = await EditarAsync(api, gestora.Id, Corpo(gestora, perfil: "Funcionario"));

        Assert.Equal(422, (int)recusada.StatusCode);
        var problema = await CorpoAsync(recusada);
        Assert.Equal(("GESTOR_PRECISA_TER_PERFIL_GESTOR", $"{gestora.Nome} gerencia o setor Setor gerido editar10 e precisa ter o nível Gestor. Tire-o do setor antes de mudar o nível."), (problema.GetProperty("codigo").GetString(), problema.GetProperty("detail").GetString()));
        Assert.Equal("Funcionario Gestor", await Sql.EscalarAsync(api.Conexao, $"SELECT n.NOME_NIVEL_ACESSO FROM FUNCIONARIOS f JOIN NIVEIS_DE_ACESSO n ON n.ID_NIVEL_ACESSO = f.ID_NIVEL_ACESSO WHERE f.ID_FUNCIONARIO = {gestora.Id}")); // nada foi gravado

        Assert.Equal(200, (int)(await EditarAsync(api, gestora.Id, Corpo(gestora, perfil: "Gestor"))).StatusCode);
    }

    [Fact]
    public async Task Editar_dataAdmissao_depois_do_ultimo_dia_de_trabalho_devolve_400_e_nao_500()
    {
        var api = await ApiAsync();
        var agendado = await PessoaAsync(api, "editar11 agendado");
        await SementeDoCadastro.DesligamentoAgendadoAsync(api.Conexao, agendado.Id, new DateOnly(2026, 12, 31));
        var efetivado = await PessoaAsync(api, "editar11 efetivado", ativo: false); // desligado em 2026-09-30

        var depoisDoAgendado = await EditarAsync(api, agendado.Id, Corpo(agendado, admissao: "2027-01-02"));
        var depoisDoEfetivado = await EditarAsync(api, efetivado.Id, Corpo(efetivado, admissao: "2026-10-05"));

        foreach (var resposta in new[] { depoisDoAgendado, depoisDoEfetivado })
        {
            Assert.Equal(400, (int)resposta.StatusCode);
            Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty("dataAdmissao", out _));
        }

        // No próprio último dia ainda vale (não é "posterior").
        Assert.Equal(200, (int)(await EditarAsync(api, agendado.Id, Corpo(agendado, admissao: "2026-12-31"))).StatusCode);
        Assert.Equal(200, (int)(await EditarAsync(api, efetivado.Id, Corpo(efetivado, admissao: "2026-09-30"))).StatusCode);
    }

    [Fact]
    public async Task Editar_dar_beneficio_a_inativo_devolve_422_FUNCIONARIO_INATIVO()
    {
        var api = await ApiAsync();
        var inativo = await PessoaAsync(api, "editar12", ativo: false);
        var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Beneficio editar12");

        var recusada = await EditarAsync(api, inativo.Id, Corpo(inativo, beneficios: [beneficio]));

        Assert.Equal(422, (int)recusada.StatusCode);
        var problema = await CorpoAsync(recusada);
        Assert.Equal(("FUNCIONARIO_INATIVO", "Não é possível dar benefício a um colaborador inativo."), (problema.GetProperty("codigo").GetString(), problema.GetProperty("detail").GetString()));
        Assert.Equal(0, await LinhasDoBeneficioAsync(api, inativo.Id, beneficio));

        // Editar o inativo sem dar benefício nenhum passa.
        Assert.Equal(200, (int)(await EditarAsync(api, inativo.Id, Corpo(inativo))).StatusCode);
    }

    [Fact]
    public async Task Editar_com_CPF_de_outra_pessoa_devolve_409_e_com_o_proprio_CPF_passa()
    {
        var api = await ApiAsync();
        var outra = await PessoaAsync(api, "editar13 outra");
        var pessoa = await PessoaAsync(api, "editar13");

        var porCpf = await EditarAsync(api, pessoa.Id, Corpo(pessoa, cpf: outra.Cpf));
        var porEmail = await EditarAsync(api, pessoa.Id, Corpo(pessoa, email: outra.Email.ToUpperInvariant()));

        foreach (var (resposta, codigo) in new[] { (porCpf, "CPF_JA_CADASTRADO"), (porEmail, "EMAIL_JA_CADASTRADO") })
        {
            Assert.Equal(409, (int)resposta.StatusCode);
            var problema = await CorpoAsync(resposta);
            Assert.Equal(codigo, problema.GetProperty("codigo").GetString());
            Assert.Equal((outra.Id, outra.Nome, true), (problema.GetProperty("cadastroExistente").GetProperty("funcionarioId").GetInt32(), problema.GetProperty("cadastroExistente").GetProperty("nome").GetString(), problema.GetProperty("cadastroExistente").GetProperty("ativo").GetBoolean()));
        }

        Assert.Equal(200, (int)(await EditarAsync(api, pessoa.Id, Corpo(pessoa))).StatusCode); // o próprio CPF e o próprio e-mail não contam
    }

    [Fact]
    public async Task Editar_com_gestor_do_setor_inativo_so_recusa_se_o_setor_mudou()
    {
        var api = await ApiAsync();
        var (gestora, gestorId) = await GestoraAsync(api, "Gestora editar14");
        var (cargo, setor) = await CargoESetorAsync(api, "editar14", gestorId);
        var pessoa = await PessoaAsync(api, "editar14", (cargo, setor), gestorId: gestorId);
        var (_, gestorInativo) = await GestoraAsync(api, "Gestora inativa editar14", ativo: false);
        var setorComInativo = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor editar editar14 inativo", gestorInativo);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {gestora.Id}"); // a gestora do setor da pessoa saiu da empresa

        var mudando = await EditarAsync(api, pessoa.Id, Corpo(pessoa, setor: setorComInativo));
        var ficando = await EditarAsync(api, pessoa.Id, Corpo(pessoa, nome: "Nome Novo editar14"));

        Assert.Equal(422, (int)mudando.StatusCode);
        Assert.Equal("GESTOR_DO_SETOR_INATIVO", (await CorpoAsync(mudando)).GetProperty("codigo").GetString());
        Assert.Equal(setor, (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_SETOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa.Id}")); // nada foi gravado
        Assert.Equal(200, (int)ficando.StatusCode); // sem mudar de setor o gestor inativo não atrapalha
    }

    [Fact]
    public async Task Editar_com_campos_invalidos_devolve_400_por_campo_e_aceita_os_limites()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "editar15");
        var invalido = Corpo(pessoa);
        invalido["emailPessoal"] = "sem-arroba";
        invalido["rg"] = new string('9', 21);
        invalido["rgOrgaoEmissor"] = new string('S', 21);
        invalido["endereco"] = Endereco(cep: "1234567", uf: "XX");

        var recusada = await EditarAsync(api, pessoa.Id, invalido);

        Assert.Equal(400, (int)recusada.StatusCode);
        var erros = (await CorpoAsync(recusada)).GetProperty("errors");
        foreach (var campo in new[] { "emailPessoal", "rg", "rgOrgaoEmissor", "endereco.cep", "endereco.uf" })
        {
            Assert.True(erros.TryGetProperty(campo, out _), campo);
        }

        var nosLimites = Corpo(pessoa);
        nosLimites["emailPessoal"] = new string('a', 138) + "@exemplo.com"; // 150 caracteres
        nosLimites["rg"] = new string('9', 20);
        nosLimites["rgOrgaoEmissor"] = new string('S', 20);
        nosLimites["endereco"] = Endereco(cep: "01310100", uf: "SP");
        Assert.Equal(200, (int)(await EditarAsync(api, pessoa.Id, nosLimites)).StatusCode);
    }

    [Fact]
    public async Task Editar_inexistente_404_perfil_errado_403_e_anonimo_401()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "editar16");
        var corpo = Corpo(pessoa);

        Assert.Equal(404, (int)(await EditarAsync(api, 987_654, corpo)).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.Gestor).PutAsJsonAsync(Rota(pessoa.Id), corpo)).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.MembroA).PutAsJsonAsync(Rota(pessoa.Id), corpo)).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().PutAsJsonAsync(Rota(pessoa.Id), corpo)).StatusCode);
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "editar17");
        var outra = await PessoaAsync(api, "editar17 outra");
        var inativo = await PessoaAsync(api, "editar17 inativo", ativo: false);
        var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Beneficio editar17");

        await (await EditarAsync(api, pessoa.Id, Corpo(pessoa))).AfirmarConformeAoContratoAsync("editarFuncionario"); // o 200
        await (await EditarAsync(api, pessoa.Id, Corpo(pessoa, nome: string.Empty))).AfirmarConformeAoContratoAsync("editarFuncionario"); // o 400
        await (await EditarAsync(api, 987_654, Corpo(pessoa))).AfirmarConformeAoContratoAsync("editarFuncionario"); // o 404
        await (await EditarAsync(api, pessoa.Id, Corpo(pessoa, cpf: outra.Cpf))).AfirmarConformeAoContratoAsync("editarFuncionario"); // o 409
        await (await EditarAsync(api, inativo.Id, Corpo(inativo, beneficios: [beneficio]))).AfirmarConformeAoContratoAsync("editarFuncionario"); // o 422
    }
}
