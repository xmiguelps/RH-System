using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Gestores;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Cadastros;

/// <summary>
/// <c>/api/rh/setores</c> (tela 29, cadastro "Setores"), só para o RH, no banco REAL com o relógio fixo em 2026-10-07T12:42Z. A ordem de avaliação é a do contrato: 404, 400, 409 (nome), 422 (inativo), 422 (RH), 409 (outro setor).
/// O nome é único sem diferenciar maiúsculas nem acentos. A troca de gestor, com os seus efeitos, é de <see cref="TrocaDeGestorDoSetorTests"/>.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SetoresTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/setores";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(SetoresTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static object Corpo(string nome, string? descricao = null, int? gestorFuncionarioId = null) => new { nome, descricao, gestorFuncionarioId };

    private static Task<HttpResponseMessage> CriarAsync(ApiDeAcesso api, string nome, string? descricao = null, int? gestorFuncionarioId = null) =>
        ComoRh(api).PostAsJsonAsync(Rota, Corpo(nome, descricao, gestorFuncionarioId));

    private static Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, int setorId, string nome, string? descricao = null, int? gestorFuncionarioId = null) =>
        ComoRh(api).PutAsJsonAsync($"{Rota}/{setorId}", Corpo(nome, descricao, gestorFuncionarioId));

    private static async Task<long> QuantosComONomeAsync(ApiDeAcesso api, string nome) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM SETORES WHERE NOME_SETOR = '{nome}'");

    // ---------------------------------------------------------------- lista

    [Fact]
    public async Task Lista_traz_gestor_ou_null_e_quantidadePessoas_de_ativos_ja_admitidos_inclusive_o_gestor()
    {
        var api = await ApiAsync();
        var carlos = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.Gestor.FuncionarioId);
        var marina = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.GestorDoFinanceiro.FuncionarioId);
        var compras = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Lista1 Compras sem gestor");

        var itens = await ItensAsync(await ComoRh(api).GetAsync($"{Rota}?tamanhoPagina=100"));

        var comercial = Assert.Single(itens, i => i.GetProperty("id").GetInt32() == api.Elenco.SetorComercialId);
        Assert.Equal(("Comercial", 3), (comercial.GetProperty("nome").GetString(), comercial.GetProperty("quantidadePessoas").GetInt32())); // Carlos, MembroA e MembroB; EmAdmissao e Inativo ficam fora
        Assert.Equal((carlos, api.Elenco.Gestor.FuncionarioId, "Carlos Ribeiro"), (comercial.GetProperty("gestor").GetProperty("gestorId").GetInt32(), comercial.GetProperty("gestor").GetProperty("funcionarioId").GetInt32(), comercial.GetProperty("gestor").GetProperty("nome").GetString()));
        var financeiro = Assert.Single(itens, i => i.GetProperty("id").GetInt32() == api.Elenco.SetorFinanceiroId);
        Assert.Equal((2, marina), (financeiro.GetProperty("quantidadePessoas").GetInt32(), financeiro.GetProperty("gestor").GetProperty("gestorId").GetInt32())); // Marina e DeOutroSetor
        var semGestor = Assert.Single(itens, i => i.GetProperty("id").GetInt32() == compras);
        Assert.Equal((JsonValueKind.Null, 0), (semGestor.GetProperty("gestor").ValueKind, semGestor.GetProperty("quantidadePessoas").GetInt32()));
    }

    [Fact]
    public async Task Busca_no_nome_e_na_descricao_ordem_por_nome_e_setor_com_gestor_inativo_continua_mostrando_o_gestor()
    {
        var api = await ApiAsync();
        var comGestorInativo = await GestoresDeTeste.GestorInativoAsync(api, "Busca2 Z"); // o setor "Busca2 Z Setor" continua apontando para o gestor desligado
        await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Busca2 A Setor");
        var porDescricao = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Busca2 M Setor");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET DESCRICAO = 'zebras e girafas Busca2' WHERE ID_SETOR = {porDescricao}");

        var todos = await ItensAsync(await ComoRh(api).GetAsync($"{Rota}?busca=Busca2&tamanhoPagina=100"));
        var soDescricao = await ItensAsync(await ComoRh(api).GetAsync($"{Rota}?busca=girafas&tamanhoPagina=100"));

        Assert.Equal(["Busca2 A Setor", "Busca2 M Setor", "Busca2 Z Setor"], todos.Select(i => i.GetProperty("nome").GetString())); // por nome, não por id
        Assert.Equal([porDescricao], soDescricao.Select(i => i.GetProperty("id").GetInt32()));
        var inativo = todos[2];
        Assert.Equal((comGestorInativo.SetorId, comGestorInativo.GestorId, "Busca2 Z Gestor"), (inativo.GetProperty("id").GetInt32(), inativo.GetProperty("gestor").GetProperty("gestorId").GetInt32(), inativo.GetProperty("gestor").GetProperty("nome").GetString()));
    }

    // ---------------------------------------------------------------- criar

    [Fact]
    public async Task Criar_sem_gestor_devolve_201_com_gestor_null_e_quantidadePessoas_0_e_grava_Criou()
    {
        var api = await ApiAsync();

        var resposta = await CriarAsync(api, "Criar3 Compras", "Cotações e fornecedores");

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.Equal(("Criar3 Compras", "Cotações e fornecedores", JsonValueKind.Null, 0), (corpo.GetProperty("nome").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("gestor").ValueKind, corpo.GetProperty("quantidadePessoas").GetInt32()));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "SETORES", id));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Criou, "Setor Criar3 Compras", api.Elenco.Rh.FuncionarioId, TipoDeAtor.Pessoa), (evento.Area, evento.Acao, evento.RegistroAfetado, evento.FuncionarioAtorId, evento.AtorTipo));
    }

    [Fact]
    public async Task Criar_com_gestor_Funcionario_promove_a_Gestor_cria_a_linha_em_Gestores_e_define_o_gestor_do_setor()
    {
        var api = await ApiAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Criar4 Candidato", Perfil.Funcionario);
        Assert.Equal(0, await SetoresDeTeste.LinhasEmGestoresAsync(api, pessoa));

        var resposta = await CriarAsync(api, "Criar4 Setor novo", null, pessoa);

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var gestorId = corpo.GetProperty("gestor").GetProperty("gestorId").GetInt32();
        Assert.Equal((pessoa, 0), (corpo.GetProperty("gestor").GetProperty("funcionarioId").GetInt32(), corpo.GetProperty("quantidadePessoas").GetInt32())); // ele gerencia o setor sem fazer parte dele
        Assert.Equal("Funcionario Gestor", await SetoresDeTeste.NivelAsync(api, pessoa));
        Assert.Equal(1, await SetoresDeTeste.LinhasEmGestoresAsync(api, pessoa));
        Assert.Equal(gestorId, await SetoresDeTeste.GestorDoSetorAsync(api, corpo.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Criar_com_gestor_RH_devolve_422_GESTOR_PRECISA_TER_PERFIL_GESTOR_com_o_texto_do_contrato_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var rh = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Criar5 Ana", Perfil.RH);

        var resposta = await CriarAsync(api, "Criar5 Setor recusado", null, rh);

        Assert.Equal((422, "GESTOR_PRECISA_TER_PERFIL_GESTOR", "Criar5 Ana tem o nível RH. Só quem tem o nível Gestor pode gerenciar um setor."), await ProblemaAsync(resposta));
        Assert.Equal(0, await QuantosComONomeAsync(api, "Criar5 Setor recusado"));
        Assert.Equal(0, await SetoresDeTeste.LinhasEmGestoresAsync(api, rh));
        Assert.Equal("RH", await SetoresDeTeste.NivelAsync(api, rh));
    }

    [Fact]
    public async Task Criar_ou_editar_com_gestor_inativo_devolve_422_FUNCIONARIO_INATIVO_e_com_inexistente_400_em_gestorFuncionarioId()
    {
        var api = await ApiAsync();
        var inativo = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Inativo6 Funcionario", Perfil.Funcionario, ativo: false);
        var existente = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Inativo6 Setor existente");

        Assert.Equal((422, "FUNCIONARIO_INATIVO", "A pessoa escolhida está inativa."), await ProblemaAsync(await CriarAsync(api, "Inativo6 Novo", null, inativo)));
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "A pessoa escolhida está inativa."), await ProblemaAsync(await EditarAsync(api, existente, "Inativo6 Setor existente", null, inativo)));
        Assert.True((await ErrosAsync(await CriarAsync(api, "Inativo6 Novo", null, int.MaxValue))).TryGetProperty("gestorFuncionarioId", out _));
        Assert.True((await ErrosAsync(await EditarAsync(api, existente, "Inativo6 Setor existente", null, int.MaxValue))).TryGetProperty("gestorFuncionarioId", out _));

        Assert.Equal(0, await QuantosComONomeAsync(api, "Inativo6 Novo"));
        Assert.Null(await SetoresDeTeste.GestorDoSetorAsync(api, existente));
        Assert.Equal(0, await SetoresDeTeste.LinhasEmGestoresAsync(api, inativo));
    }

    [Fact]
    public async Task Criar_com_gestor_que_ja_responde_por_outro_setor_devolve_409_GESTOR_JA_RESPONDE_POR_OUTRO_SETOR_com_o_nome_dele_e_do_setor()
    {
        var api = await ApiAsync();
        var ocupado = await GestoresDeTeste.GestorDeSetorAsync(api, "Outro7"); // "Outro7 Gestor" responde por "Outro7 Setor"
        var livre = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Outro7 Setor livre");
        var texto = "Outro7 Gestor já é o gestor do setor Outro7 Setor. Tire-o de lá antes de escolhê-lo aqui.";

        Assert.Equal((409, "GESTOR_JA_RESPONDE_POR_OUTRO_SETOR", texto), await ProblemaAsync(await CriarAsync(api, "Outro7 Novo", null, ocupado.FuncionarioId)));
        Assert.Equal((409, "GESTOR_JA_RESPONDE_POR_OUTRO_SETOR", texto), await ProblemaAsync(await EditarAsync(api, livre, "Outro7 Setor livre", null, ocupado.FuncionarioId)));

        Assert.Equal(0, await QuantosComONomeAsync(api, "Outro7 Novo"));
        Assert.Null(await SetoresDeTeste.GestorDoSetorAsync(api, livre));
        Assert.Equal(ocupado.GestorId, await SetoresDeTeste.GestorDoSetorAsync(api, ocupado.SetorId!.Value)); // e o setor dele continua dele
        Assert.Equal(200, (int)(await EditarAsync(api, ocupado.SetorId.Value, "Outro7 Setor", "mesmo gestor", ocupado.FuncionarioId)).StatusCode); // o próprio setor dele pode ser editado com ele
    }

    [Fact]
    public async Task Nome_repetido_409_NOME_JA_CADASTRADO_e_vinte_criacoes_simultaneas_dao_um_201_e_nenhum_500()
    {
        var api = await ApiAsync();
        await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Nome8 Logística");
        var outro = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Nome8 Outro");
        var rh = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Nome8 Ana", Perfil.RH);

        // Sem diferenciar maiúsculas nem acentos (a chave única é utf8mb4_unicode_ci), na criação e na edição.
        foreach (var repetido in new[] { "Nome8 Logística", "NOME8 LOGISTICA", "nome8 logistica" })
        {
            Assert.Equal((409, "NOME_JA_CADASTRADO", "Já existe um cadastro com este nome."), await ProblemaAsync(await CriarAsync(api, repetido)));
        }

        Assert.Equal(409, (int)(await EditarAsync(api, outro, "nome8 LOGISTICA")).StatusCode);
        Assert.Equal(200, (int)(await EditarAsync(api, outro, "Nome8 Outro", "o próprio nome não conflita")).StatusCode);
        // O 409 do nome vem antes do 422 do gestor.
        Assert.Equal("NOME_JA_CADASTRADO", (await ProblemaAsync(await CriarAsync(api, "Nome8 Logística", null, rh))).Codigo);

        var respostas = await EmSimultaneoAsync(20, _ => CriarAsync(api, "Nome8 Simultâneo"));

        Assert.Equal(new[] { 201 }.Concat(Enumerable.Repeat(409, 19)), respostas.Select(r => (int)r.StatusCode).Order());
        foreach (var perdedora in respostas.Where(r => (int)r.StatusCode == 409))
        {
            Assert.Equal("NOME_JA_CADASTRADO", (await ProblemaAsync(perdedora)).Codigo);
        }

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM SETORES WHERE NOME_SETOR = 'Nome8 Simultâneo'"));
    }

    [Theory]
    [InlineData("""{"descricao":null,"gestorFuncionarioId":null}""", "nome")]
    [InlineData("""{"nome":"   ","descricao":null,"gestorFuncionarioId":null}""", "nome")]
    public async Task Corpo_sem_nome_ou_com_nome_em_branco_devolve_400_em_nome(string corpo, string campo)
    {
        var api = await ApiAsync();

        Assert.True((await ErrosAsync(await ComoRh(api).PostAsync(Rota, Json(corpo)))).TryGetProperty(campo, out _));
    }

    [Theory]
    [InlineData("""{"nome":"Obrigatorio9 sem descricao","gestorFuncionarioId":null}""")]
    [InlineData("""{"nome":"Obrigatorio9 sem gestor","descricao":null}""")]
    public async Task Corpo_sem_descricao_ou_sem_gestorFuncionarioId_devolve_400_e_nao_grava(string corpo)
    {
        var api = await ApiAsync();
        var existente = await GestoresDeTeste.GestorDeSetorAsync(api, $"Obrigatorio9b{Guid.NewGuid():N}"[..20]); // um setor por caso da teoria

        Assert.Equal(400, (int)(await ComoRh(api).PostAsync(Rota, Json(corpo))).StatusCode);
        // Na edição o campo que falta valeria "tirar o gestor" ou "apagar a descrição": é 400, e o setor fica como estava.
        Assert.Equal(400, (int)(await ComoRh(api).PutAsync($"{Rota}/{existente.SetorId}", Json(corpo.Replace("Obrigatorio9 ", "Obrigatorio9b ")))).StatusCode);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM SETORES WHERE NOME_SETOR LIKE 'Obrigatorio9 %'"));
        Assert.Equal(existente.GestorId, await SetoresDeTeste.GestorDoSetorAsync(api, existente.SetorId!.Value));
    }

    [Fact]
    public async Task Nome_e_descricao_fora_dos_limites_devolvem_400_e_100_e_255_sao_o_limite()
    {
        var api = await ApiAsync();

        foreach (var nome in new[] { "", "   ", new string('N', 101) })
        {
            Assert.True((await ErrosAsync(await CriarAsync(api, nome))).TryGetProperty("nome", out _), $"nome \"{nome}\"");
        }

        var comDescricaoLonga = await ErrosAsync(await CriarAsync(api, "Limites10 valido", new string('d', 256)));
        Assert.True(comDescricaoLonga.TryGetProperty("descricao", out _));
        Assert.False(comDescricaoLonga.TryGetProperty("nome", out _)); // só o campo errado é acusado
        Assert.Equal(201, (int)(await CriarAsync(api, new string('L', 100), new string('d', 255))).StatusCode);
    }

    // ---------------------------------------------------------------- editar, perfis e contrato

    [Fact]
    public async Task Setor_inexistente_devolve_404_na_edicao_e_o_404_vem_antes_do_400()
    {
        var api = await ApiAsync();

        var existente = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Inexistente11 controle");
        Assert.Equal(200, (int)(await EditarAsync(api, existente, "Inexistente11 controle")).StatusCode); // o controle: a mesma chamada num setor que existe dá 200 (sem ele, o 404 de uma rota que não existe passaria)
        Assert.Equal(404, (int)(await EditarAsync(api, int.MaxValue, "Inexistente11")).StatusCode);
        Assert.Equal(404, (int)(await EditarAsync(api, int.MaxValue, "")).StatusCode); // corpo inválido para setor que não existe: 404
    }

    [Fact]
    public async Task Editar_substitui_nome_e_descricao_e_grava_Alterou_com_antes_e_depois()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Editar12 antes");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET DESCRICAO = 'descrição antiga' WHERE ID_SETOR = {id}");

        var resposta = await EditarAsync(api, id, "Editar12 depois", "descrição nova");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((id, "Editar12 depois", "descrição nova"), (corpo.GetProperty("id").GetInt32(), corpo.GetProperty("nome").GetString(), corpo.GetProperty("descricao").GetString()));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "SETORES", id));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Alterou), (evento.Area, evento.Acao));
        Assert.Contains(evento.Mudancas, m => m is { Campo: "Nome", ValorAntes: "Editar12 antes", ValorDepois: "Editar12 depois" });
        Assert.Contains(evento.Mudancas, m => m is { Campo: "Descrição", ValorAntes: "descrição antiga", ValorDepois: "descrição nova" });
    }

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Permissao13 setor");
        var rotas = new (HttpMethod Metodo, string Caminho)[] { (HttpMethod.Get, Rota), (HttpMethod.Post, Rota), (HttpMethod.Put, $"{Rota}/{id}") };

        foreach (var (metodo, caminho) in rotas)
        {
            foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
            {
                var pedido = new HttpRequestMessage(metodo, caminho) { Content = metodo == HttpMethod.Get ? null : JsonContent.Create(Corpo("Permissao13 invasor")) };
                Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).SendAsync(pedido)).StatusCode);
            }

            Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().SendAsync(new HttpRequestMessage(metodo, caminho))).StatusCode);
        }

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM SETORES WHERE NOME_SETOR = 'Permissao13 invasor'"));
        Assert.Equal("Permissao13 setor", await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_SETOR FROM SETORES WHERE ID_SETOR = {id}"));
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Schema14 alvo");
        var rh = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Schema14 Ana", Perfil.RH);
        var ocupado = await GestoresDeTeste.GestorDeSetorAsync(api, "Schema14");

        // O status de cada resposta é conferido antes do schema: o 404 de uma rota que não existe também "bate" com o 404 declarado.
        var (lista, listaInvalida, criado, repetido, recusado, ocupadoPorOutro, semNome) = (
            await ComoRh(api).GetAsync($"{Rota}?busca=Schema14"),
            await ComoRh(api).GetAsync($"{Rota}?tamanhoPagina=0"),
            await CriarAsync(api, "Schema14 criado"),
            await CriarAsync(api, "Schema14 criado"),
            await CriarAsync(api, "Schema14 recusado", null, rh),
            await CriarAsync(api, "Schema14 ocupado", null, ocupado.FuncionarioId),
            await CriarAsync(api, ""));
        var (editado, inexistente, editadoRecusado, editadoRepetido) = (
            await EditarAsync(api, id, "Schema14 editado", "descrição"),
            await EditarAsync(api, int.MaxValue, "Schema14 nenhum"),
            await EditarAsync(api, id, "Schema14 editado", null, rh),
            await EditarAsync(api, id, "Schema14 criado"));
        Assert.Equal([200, 400, 201, 409, 422, 409, 400, 200, 404, 422, 409], new[] { lista, listaInvalida, criado, repetido, recusado, ocupadoPorOutro, semNome, editado, inexistente, editadoRecusado, editadoRepetido }.Select(r => (int)r.StatusCode));

        await lista.AfirmarConformeAoContratoAsync("listarSetores");
        await listaInvalida.AfirmarConformeAoContratoAsync("listarSetores");
        foreach (var resposta in new[] { criado, repetido, recusado, ocupadoPorOutro, semNome })
        {
            await resposta.AfirmarConformeAoContratoAsync("criarSetor");
        }

        foreach (var resposta in new[] { editado, inexistente, editadoRecusado, editadoRepetido })
        {
            await resposta.AfirmarConformeAoContratoAsync("editarSetor");
        }
    }
}
