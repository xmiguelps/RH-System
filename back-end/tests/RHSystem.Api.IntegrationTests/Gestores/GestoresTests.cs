using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Gestores;

/// <summary>
/// <c>GET</c> e <c>POST /api/rh/gestores</c> (tela 29, cadastro "Gestores"), só para o RH, no banco REAL com o relógio fixo em 2026-10-07T12:42Z. "Quem é gestor": linha em <c>GESTORES</c>, pessoa ativa e
/// (gerencia um setor ou tem o nível RH). O banco é um por classe: os testes conferem as linhas pelo <c>gestorId</c> que criaram, nunca a lista inteira.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class GestoresTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/gestores";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(GestoresTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static async Task<List<JsonElement>> ListaAsync(ApiDeAcesso api) => await ItensAsync(await ComoRh(api).GetAsync(Rota));

    private static JsonElement Linha(List<JsonElement> itens, int gestorId) => Assert.Single(itens, i => i.GetProperty("gestorId").GetInt32() == gestorId);

    private static List<int> Ids(List<JsonElement> itens) => [.. itens.Select(i => i.GetProperty("gestorId").GetInt32())];

    private static Task<HttpResponseMessage> CadastrarAsync(ApiDeAcesso api, int funcionarioId) => ComoRh(api).PostAsJsonAsync(Rota, new { funcionarioId });

    // ---------------------------------------------------------------- lista

    [Fact]
    public async Task Lista_traz_so_quem_gerencia_setor_ativo_ou_e_do_RH_cadastrado_na_ordem_de_gestorId()
    {
        var api = await ApiAsync();
        var carlos = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.Gestor.FuncionarioId);
        var marina = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.GestorDoFinanceiro.FuncionarioId);
        var gestorSemSetor = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.GestorSemSetor.FuncionarioId); // controle: nível Gestor, sem setor
        var rh = await GestoresDeTeste.RhCadastradoAsync(api, "Lista1");
        var rebaixado = await GestoresDeTeste.ExGestorRebaixadoAsync(api, "Lista1"); // controle: voltou a Funcionario
        var inativo = await GestoresDeTeste.GestorInativoAsync(api, "Lista1"); // controle: gerencia o setor, mas foi desligado

        var ids = Ids(await ListaAsync(api));

        Assert.Contains(carlos, ids);
        Assert.Contains(marina, ids);
        Assert.Contains(rh.GestorId, ids);
        Assert.DoesNotContain(gestorSemSetor, ids);
        Assert.DoesNotContain(rebaixado.GestorId, ids);
        Assert.DoesNotContain(inativo.GestorId, ids);
        Assert.Equal(ids.Order().ToList(), ids); // a ordem é a de gestorId (o RH cadastrado, de nome "Lista1 RH", tem o maior id e ficaria no meio se a ordem fosse a do nome)
    }

    [Fact]
    public async Task Linha_de_gestor_de_setor_tem_setor_equipe_igual_a_definicao_da_1a_e_soDecide_false()
    {
        var api = await ApiAsync();
        var carlos = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.Gestor.FuncionarioId);

        var linha = Linha(await ListaAsync(api), carlos);

        Assert.Equal((api.Elenco.SetorComercialId, "Comercial"), (linha.GetProperty("setor").GetProperty("id").GetInt32(), linha.GetProperty("setor").GetProperty("nome").GetString()));
        Assert.False(linha.GetProperty("soDecidePedidosDeGestores").GetBoolean());
        Assert.False(linha.GetProperty("ausente").GetBoolean());
        Assert.Equal(JsonValueKind.Null, linha.GetProperty("ausenteAte").ValueKind);
        Assert.False(linha.GetProperty("ausenteHoje").GetBoolean());
        // A definição única de equipe: ativos, admissão já começada, do setor, sem o gestor. Fora: EmAdmissao (entra em 2026-10-13), Inativo e o próprio Carlos.
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var equipe = new EquipeDoGestor(carlos, api.Elenco.SetorComercialId, "Comercial", api.Elenco.Gestor.FuncionarioId, new DateOnly(2026, 10, 7));
        var membros = db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).OrderBy(id => id).ToList();
        Assert.Equal([api.Elenco.MembroA.FuncionarioId, api.Elenco.MembroB.FuncionarioId], membros);
        Assert.Equal(membros.Count, linha.GetProperty("quantidadeEquipe").GetInt32());
    }

    [Fact]
    public async Task Linha_do_RH_cadastrado_tem_setor_null_equipe_0_e_soDecide_true()
    {
        var api = await ApiAsync();
        var setorDele = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Lista3 Setor do RH");
        await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Lista3 colega A", Perfil.Funcionario, setorDele);
        await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Lista3 colega B", Perfil.Funcionario, setorDele);
        var rh = await GestoresDeTeste.RhCadastradoAsync(api, "Lista3", setorDaPessoa: setorDele); // pertence a um setor com gente, mas não o gerencia

        var linha = Linha(await ListaAsync(api), rh.GestorId);

        Assert.Equal(JsonValueKind.Null, linha.GetProperty("setor").ValueKind);
        Assert.True(linha.GetProperty("soDecidePedidosDeGestores").GetBoolean());
        Assert.Equal(0, linha.GetProperty("quantidadeEquipe").GetInt32());
    }

    [Fact]
    public async Task Funcionario_da_linha_traz_o_cargo_e_o_setor_da_pessoa_e_ativo()
    {
        var api = await ApiAsync();
        var comCargo = await GestoresDeTeste.GestorDeSetorAsync(api, "Func4", cargo: "Func4 Gerente comercial");
        var semCargo = await GestoresDeTeste.RhCadastradoAsync(api, "Func4");
        var itens = await ListaAsync(api);

        var pessoa = Linha(itens, comCargo.GestorId).GetProperty("funcionario");
        var pessoaSemCargo = Linha(itens, semCargo.GestorId).GetProperty("funcionario");

        Assert.Equal((comCargo.FuncionarioId, "Func4 Gestor", "Func4 Gerente comercial", "Func4 Setor", true), (pessoa.GetProperty("id").GetInt32(), pessoa.GetProperty("nome").GetString(), pessoa.GetProperty("cargo").GetString(), pessoa.GetProperty("setor").GetString(), pessoa.GetProperty("ativo").GetBoolean()));
        Assert.Equal((semCargo.FuncionarioId, JsonValueKind.Null, JsonValueKind.Null), (pessoaSemCargo.GetProperty("id").GetInt32(), pessoaSemCargo.GetProperty("cargo").ValueKind, pessoaSemCargo.GetProperty("setor").ValueKind));
    }

    [Fact]
    public async Task Gestor_de_dois_setores_mostra_o_de_menor_id_e_a_equipe_desse_setor()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Doissetores4b", equipe: 1); // o primeiro setor (menor id) tem 1 membro
        var segundoSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Doissetores4b Segundo");
        await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Doissetores4b membro do segundo A", Perfil.Funcionario, segundoSetor);
        await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Doissetores4b membro do segundo B", Perfil.Funcionario, segundoSetor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor.GestorId} WHERE ID_SETOR = {segundoSetor}"); // o banco permite; a regra "um setor por gestor" é da Task 5

        var linha = Linha(await ListaAsync(api), gestor.GestorId);

        Assert.Equal((gestor.SetorId!.Value, 1), (linha.GetProperty("setor").GetProperty("id").GetInt32(), linha.GetProperty("quantidadeEquipe").GetInt32()));
    }

    // ---------------------------------------------------------------- cadastrar

    [Fact]
    public async Task Cadastrar_RH_ativo_devolve_201_com_setor_null_soDecide_true_ausente_false_e_grava_Criou_na_area_Cadastros()
    {
        var api = await ApiAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Cadastrar5 Sofia", Perfil.RH);

        var resposta = await CadastrarAsync(api, pessoa);

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var gestorId = corpo.GetProperty("gestorId").GetInt32();
        Assert.Equal(gestorId, Convert.ToInt32(await Sql.EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {pessoa}")));
        Assert.Equal((pessoa, "Cadastrar5 Sofia", true), (corpo.GetProperty("funcionario").GetProperty("id").GetInt32(), corpo.GetProperty("funcionario").GetProperty("nome").GetString(), corpo.GetProperty("funcionario").GetProperty("ativo").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("setor").ValueKind);
        Assert.Equal((true, 0, false, JsonValueKind.Null, false), (corpo.GetProperty("soDecidePedidosDeGestores").GetBoolean(), corpo.GetProperty("quantidadeEquipe").GetInt32(), corpo.GetProperty("ausente").GetBoolean(), corpo.GetProperty("ausenteAte").ValueKind, corpo.GetProperty("ausenteHoje").GetBoolean()));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "GESTORES", gestorId));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Criou, "Gestor Cadastrar5 Sofia", api.Elenco.Rh.FuncionarioId, TipoDeAtor.Pessoa), (evento.Area, evento.Acao, evento.RegistroAfetado, evento.FuncionarioAtorId, evento.AtorTipo));
    }

    [Fact]
    public async Task Cadastrar_inexistente_devolve_400_em_funcionarioId_inativo_422_FUNCIONARIO_INATIVO_nao_RH_422_GESTOR_SEM_SETOR_SO_PARA_RH_e_repetido_409_GESTOR_JA_CADASTRADO()
    {
        var api = await ApiAsync();
        var antes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM GESTORES");

        // 400 em funcionarioId: pessoa que não existe (o contrato não declara 404 aqui) e corpo sem o campo.
        Assert.True((await ErrosAsync(await CadastrarAsync(api, int.MaxValue))).TryGetProperty("funcionarioId", out _));
        Assert.True((await ErrosAsync(await ComoRh(api).PostAsync(Rota, Json("{}")))).TryGetProperty("funcionarioId", out _));

        // 422 FUNCIONARIO_INATIVO: a regra da pessoa inativa vem ANTES da do nível, então uma pessoa inativa e sem o nível RH também recebe este código.
        var inativoRh = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Cadastrar6 RH inativo", Perfil.RH, ativo: false);
        var inativoComum = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Cadastrar6 colaborador inativo", Perfil.Funcionario, ativo: false);
        foreach (var pessoa in new[] { inativoRh, inativoComum })
        {
            Assert.Equal((422, "FUNCIONARIO_INATIVO", "A pessoa escolhida está inativa."), await ProblemaAsync(await CadastrarAsync(api, pessoa)));
        }

        // 422 GESTOR_SEM_SETOR_SO_PARA_RH: quem não tem o nível RH, inclusive quem já é gestor (a regra do nível vem ANTES da do 409).
        var colaborador = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Cadastrar6 colaborador", Perfil.Funcionario);
        var textoDoNivel = "Só quem é do RH é cadastrado como gestor sem setor. Para gerenciar um setor, escolha a pessoa no cadastro de Setores.";
        foreach (var pessoa in new[] { colaborador, api.Elenco.Gestor.FuncionarioId })
        {
            Assert.Equal((422, "GESTOR_SEM_SETOR_SO_PARA_RH", textoDoNivel), await ProblemaAsync(await CadastrarAsync(api, pessoa)));
        }

        // 409 GESTOR_JA_CADASTRADO: o segundo cadastro da mesma pessoa do RH.
        var rh = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Cadastrar6 RH", Perfil.RH);
        Assert.Equal(201, (int)(await CadastrarAsync(api, rh)).StatusCode);
        Assert.Equal((409, "GESTOR_JA_CADASTRADO", "Esta pessoa já está cadastrada como gestor."), await ProblemaAsync(await CadastrarAsync(api, rh)));

        Assert.Equal(antes + 1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM GESTORES")); // só o RH cadastrado de verdade entrou
    }

    [Fact]
    public async Task Cadastrar_a_mesma_pessoa_do_RH_em_simultaneo_da_um_201_e_o_resto_409_GESTOR_JA_CADASTRADO_nunca_500()
    {
        var api = await ApiAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Cadastrar7 RH em corrida", Perfil.RH);

        var respostas = await EmSimultaneoAsync(10, _ => CadastrarAsync(api, pessoa));

        Assert.Equal(new[] { 201 }.Concat(Enumerable.Repeat(409, 9)), respostas.Select(r => (int)r.StatusCode).Order());
        foreach (var perdedora in respostas.Where(r => (int)r.StatusCode == 409))
        {
            Assert.Equal((409, "GESTOR_JA_CADASTRADO", "Esta pessoa já está cadastrada como gestor."), await ProblemaAsync(perdedora));
        }

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM GESTORES WHERE ID_FUNCIONARIO = {pessoa}"));
    }

    [Fact]
    public async Task Cadastrar_nao_muda_o_nivel_da_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Cadastrar8 RH", Perfil.RH);

        Assert.Equal(201, (int)(await CadastrarAsync(api, pessoa)).StatusCode);

        Assert.Equal("RH", await Sql.EscalarAsync(api.Conexao, $"SELECT n.NOME_NIVEL_ACESSO FROM FUNCIONARIOS f JOIN NIVEIS_DE_ACESSO n ON n.ID_NIVEL_ACESSO = f.ID_NIVEL_ACESSO WHERE f.ID_FUNCIONARIO = {pessoa}"));
    }

    // ---------------------------------------------------------------- perfis e contrato

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas()
    {
        var api = await ApiAsync();
        var rh = await GestoresDeTeste.RhCadastradoAsync(api, "Permissao9");
        var alvo = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Permissao9 alvo", Perfil.RH);
        var rotas = new (HttpMethod Metodo, string Caminho, object? Corpo)[]
        {
            (HttpMethod.Get, Rota, null),
            (HttpMethod.Post, Rota, new { funcionarioId = alvo }),
            (HttpMethod.Put, $"{Rota}/{rh.GestorId}/ausencia", new { ausente = true, ausenteAte = (string?)null }),
        };

        foreach (var (metodo, caminho, corpo) in rotas)
        {
            foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
            {
                var pedido = new HttpRequestMessage(metodo, caminho) { Content = corpo is null ? null : JsonContent.Create(corpo) };
                Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).SendAsync(pedido)).StatusCode);
            }

            Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().SendAsync(new HttpRequestMessage(metodo, caminho))).StatusCode);
        }

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM GESTORES WHERE ID_FUNCIONARIO = {alvo}")); // o POST do perfil errado não cadastrou ninguém
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT APROVACAO_AUTOMATICA FROM GESTORES WHERE ID_GESTOR = {rh.GestorId}")); // nem o PUT marcou
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var rh = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Schema10 RH", Perfil.RH);
        var inativo = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Schema10 inativo", Perfil.RH, ativo: false);
        var comum = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Schema10 comum", Perfil.Funcionario);
        await GestoresDeTeste.GestorDeSetorAsync(api, "Schema10", equipe: 1);

        await (await cliente.GetAsync(Rota)).AfirmarConformeAoContratoAsync("listarGestores");
        await (await CadastrarAsync(api, rh)).AfirmarConformeAoContratoAsync("cadastrarGestor"); // o 201
        await (await CadastrarAsync(api, rh)).AfirmarConformeAoContratoAsync("cadastrarGestor"); // o 409
        await (await CadastrarAsync(api, int.MaxValue)).AfirmarConformeAoContratoAsync("cadastrarGestor"); // o 400
        await (await CadastrarAsync(api, inativo)).AfirmarConformeAoContratoAsync("cadastrarGestor"); // o 422 da pessoa inativa
        await (await CadastrarAsync(api, comum)).AfirmarConformeAoContratoAsync("cadastrarGestor"); // o 422 do nível
    }
}
