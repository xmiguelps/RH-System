using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Acesso;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary><c>GET /api/rh/funcionarios/{id}</c> (a ficha do RH): tudo completo, sem máscara. Banco REAL, relógio em 2026-10-07T12:42Z.</summary>
[Collection(MySqlCollection.Nome)]
public class FichaDoFuncionarioTests(MySqlFixture mysql)
{
    private static int _contador;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(FichaDoFuncionarioTests));

    private static string Email() => $"ficha.{Interlocked.Increment(ref _contador)}@ficha.com.br";

    private static async Task<JsonElement> FichaAsync(HttpClient cliente, int id)
    {
        var resposta = await cliente.GetAsync($"/api/rh/funcionarios/{id}");
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    [Fact]
    public async Task Ficha_traz_cpf_e_telefone_completos_endereco_cargo_setor_gestor_jornada_beneficios_e_acesso()
    {
        var api = await ApiAsync();
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, "Analista ficha1", 44.00m);
        var gestorFunc = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Gestora ficha1", Email(), new DateOnly(2025, 3, 3), nivel: "Funcionario Gestor");
        await api.NovoGestorAsync(gestorFunc);
        var idGestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {gestorFunc}");
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor ficha1", idGestor);
        var pessoa = await SementeDoCadastro.ColaboradorAsync(
            api.Conexao, "Bruno ficha1", "bruno.ficha1@ficha.com.br", new DateOnly(2026, 2, 3), setorId: setor, cargoId: cargo, gestorId: idGestor, tipoContrato: "ESTAGIO", matricula: "00482", nascimento: new DateOnly(1990, 5, 17));
        await SementeDoCadastro.DetalhesAsync(
            api.Conexao, pessoa, "52998224725", "11987654410", "123456789", "SSP", "bruno@pessoal.com", "01310100", "Av. Paulista", "1000", "Sala 2", "Bela Vista", "São Paulo", "SP");
        var refeicao = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Vale-refeição ficha1");
        var saude = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Plano de saúde ficha1");
        var transporte = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Vale-transporte ficha1");
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa, saude);
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa, refeicao);
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa, transporte, ativo: false); // desativado: não aparece
        await api.NaTransacaoAsync(sp => sp.GetRequiredService<IServicoDeConvite>().EmitirAsync([pessoa], CancellationToken.None));

        var ficha = await FichaAsync(api.Api.ClienteComo(api.Elenco.Rh), pessoa);

        Assert.Equal(("00482", "Bruno ficha1", "bruno.ficha1@ficha.com.br"), (ficha.GetProperty("matricula").GetString(), ficha.GetProperty("nome").GetString(), ficha.GetProperty("emailCorporativo").GetString()));
        Assert.Equal(("52998224725", "11987654410", "1990-05-17"), (ficha.GetProperty("cpf").GetString(), ficha.GetProperty("telefone").GetString(), ficha.GetProperty("dataNascimento").GetString())); // sem máscara
        Assert.Equal(("123456789", "SSP", "bruno@pessoal.com"), (ficha.GetProperty("rg").GetString(), ficha.GetProperty("rgOrgaoEmissor").GetString(), ficha.GetProperty("emailPessoal").GetString()));
        var endereco = ficha.GetProperty("endereco");
        Assert.Equal(["01310100", "Av. Paulista", "1000", "Sala 2", "Bela Vista", "São Paulo", "SP"], new[] { "cep", "logradouro", "numero", "complemento", "bairro", "cidade", "uf" }.Select(c => endereco.GetProperty(c).GetString()));
        Assert.Equal((cargo, "Analista ficha1"), (ficha.GetProperty("cargo").GetProperty("id").GetInt32(), ficha.GetProperty("cargo").GetProperty("nome").GetString()));
        Assert.Equal((setor, "Setor ficha1"), (ficha.GetProperty("setor").GetProperty("id").GetInt32(), ficha.GetProperty("setor").GetProperty("nome").GetString()));
        var gestor = ficha.GetProperty("gestor");
        Assert.Equal((idGestor, gestorFunc, "Gestora ficha1"), (gestor.GetProperty("gestorId").GetInt32(), gestor.GetProperty("funcionarioId").GetInt32(), gestor.GetProperty("nome").GetString()));
        Assert.Equal(("ESTAGIO", 44.0), (ficha.GetProperty("tipoContrato").GetString(), ficha.GetProperty("jornadaSemanalHoras").GetDouble()));
        Assert.Equal(("2026-02-03", "Funcionario", true, "ATIVO", "AINDA_NAO_COMECOU"), (ficha.GetProperty("dataAdmissao").GetString(), ficha.GetProperty("perfil").GetString(), ficha.GetProperty("ativo").GetBoolean(), ficha.GetProperty("situacao").GetString(), ficha.GetProperty("situacaoHoje").GetString()));
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("desligamento").ValueKind);
        var beneficios = ficha.GetProperty("beneficios").EnumerateArray().Select(b => (b.GetProperty("beneficioId").GetInt32(), b.GetProperty("nome").GetString(), b.GetProperty("quantidadeDependentes").GetInt32())).ToList();
        Assert.Equal([(refeicao, "Vale-refeição ficha1", 0), (saude, "Plano de saúde ficha1", 0)], beneficios); // só ativos e aprovados, por id do benefício
        var acesso = ficha.GetProperty("acesso");
        Assert.Equal(("CONVITE_ENVIADO", true), (acesso.GetProperty("situacao").GetString(), acesso.GetProperty("ativo").GetBoolean()));
    }

    [Fact]
    public async Task Ficha_do_gestor_do_setor_tem_gestor_null_e_a_de_setor_sem_gestor_tambem()
    {
        var api = await ApiAsync();
        var gestorFunc = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Gestor ficha2", Email(), new DateOnly(2025, 3, 3), nivel: "Funcionario Gestor");
        await api.NovoGestorAsync(gestorFunc);
        var idGestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {gestorFunc}");
        var setorComGestor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor ficha2 com gestor", idGestor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setorComGestor} WHERE ID_FUNCIONARIO = {gestorFunc}"); // o gestor é do setor que gere (o trigger não o deixa ser gestor de si mesmo)
        var setorSemGestor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor ficha2 sem gestor");
        var semGestor = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Sem gestor ficha2", Email(), new DateOnly(2026, 1, 5), setorId: setorSemGestor);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        Assert.Equal(JsonValueKind.Null, (await FichaAsync(cliente, gestorFunc)).GetProperty("gestor").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await FichaAsync(cliente, semGestor)).GetProperty("gestor").ValueKind);
    }

    [Fact]
    public async Task Ficha_do_admin_do_seed_devolve_cpf_telefone_e_endereco_nulos_cargo_setor_e_matricula_null()
    {
        var api = await ApiAsync();

        // A pessoa do RH do elenco foi criada como o admin do seed: sem dados pessoais, cargo, setor nem matrícula.
        var ficha = await FichaAsync(api.Api.ClienteComo(api.Elenco.Rh), api.Elenco.Rh.FuncionarioId);

        foreach (var campo in new[] { "matricula", "cpf", "telefone", "dataNascimento", "rg", "rgOrgaoEmissor", "emailPessoal", "cargo", "setor", "gestor", "tipoContrato", "jornadaSemanalHoras" })
        {
            Assert.Equal(JsonValueKind.Null, ficha.GetProperty(campo).ValueKind);
        }

        var endereco = ficha.GetProperty("endereco"); // o objeto vem, com os sete campos null
        Assert.Equal(JsonValueKind.Object, endereco.ValueKind);
        Assert.All(new[] { "cep", "logradouro", "numero", "complemento", "bairro", "cidade", "uf" }, c => Assert.Equal(JsonValueKind.Null, endereco.GetProperty(c).ValueKind));
        Assert.Equal(("RH", "Ana Souza"), (ficha.GetProperty("perfil").GetString(), ficha.GetProperty("nome").GetString()));
        Assert.Empty(ficha.GetProperty("beneficios").EnumerateArray());
    }

    [Fact]
    public async Task Ficha_de_inativo_mostra_ativo_false_situacao_INATIVO_e_desligamento_EFETIVADO()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var ficha = await FichaAsync(cliente, api.Elenco.Inativo.FuncionarioId); // Henrique Alves, desligado em 2026-09-30

        Assert.Equal((false, "INATIVO"), (ficha.GetProperty("ativo").GetBoolean(), ficha.GetProperty("situacao").GetString()));
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("situacaoHoje").ValueKind);
        var desligamento = ficha.GetProperty("desligamento");
        Assert.Equal(("2026-09-30", "EFETIVADO"), (desligamento.GetProperty("ultimoDiaDeTrabalho").GetString(), desligamento.GetProperty("situacao").GetString()));

        // E quem só tem o desligamento AGENDADO continua ativo, com o desligamento AGENDADO na ficha.
        var agendado = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Agendado ficha4", Email(), new DateOnly(2026, 1, 5));
        await SementeDoCadastro.DesligamentoAgendadoAsync(api.Conexao, agendado, new DateOnly(2026, 10, 30));
        var fichaAgendada = await FichaAsync(cliente, agendado);
        Assert.Equal(("ATIVO", true), (fichaAgendada.GetProperty("situacao").GetString(), fichaAgendada.GetProperty("ativo").GetBoolean()));
        Assert.Equal(("2026-10-30", "AGENDADO"), (fichaAgendada.GetProperty("desligamento").GetProperty("ultimoDiaDeTrabalho").GetString(), fichaAgendada.GetProperty("desligamento").GetProperty("situacao").GetString()));
    }

    [Fact]
    public async Task Ficha_de_id_inexistente_devolve_404_e_de_outro_perfil_403_e_anonimo_401()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;

        Assert.Equal(404, (int)(await api.Api.ClienteComo(api.Elenco.Rh).GetAsync("/api/rh/funcionarios/987654321")).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync($"/api/rh/funcionarios/{pessoa}")).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.MembroA).GetAsync($"/api/rh/funcionarios/{pessoa}")).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync($"/api/rh/funcionarios/{pessoa}")).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync("/api/rh/funcionarios")).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync("/api/rh/funcionarios")).StatusCode);
    }

    [Fact]
    public async Task Respostas_da_lista_e_da_ficha_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        await (await cliente.GetAsync("/api/rh/funcionarios?tamanhoPagina=100&situacao=ATIVO")).AfirmarConformeAoContratoAsync("listarFuncionarios");
        await (await cliente.GetAsync("/api/rh/funcionarios?situacao=INATIVO")).AfirmarConformeAoContratoAsync("listarFuncionarios");
        await (await cliente.GetAsync("/api/rh/funcionarios?situacao=XYZ")).AfirmarConformeAoContratoAsync("listarFuncionarios"); // o 400
        await (await cliente.GetAsync($"/api/rh/funcionarios/{api.Elenco.Rh.FuncionarioId}")).AfirmarConformeAoContratoAsync("obterFichaDoFuncionario");
        await (await cliente.GetAsync($"/api/rh/funcionarios/{api.Elenco.Inativo.FuncionarioId}")).AfirmarConformeAoContratoAsync("obterFichaDoFuncionario");
        await (await cliente.GetAsync("/api/rh/funcionarios/987654321")).AfirmarConformeAoContratoAsync("obterFichaDoFuncionario"); // o 404
    }
}
