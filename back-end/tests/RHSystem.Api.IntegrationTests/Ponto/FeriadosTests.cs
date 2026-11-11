using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Empresa;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// Os feriados do ponto (<c>/api/rh/ponto/feriados</c>), no banco REAL. Só o RH. A lista traz os que valem para a empresa (nacionais, estaduais da UF, municipais do município e da empresa; só ativos, a menos que <c>incluirInativos</c>), por data, sem
/// paginação, e abre mesmo sem a empresa configurada. Criar cadastra sempre um MUNICIPAL com a UF e o município da empresa (422 sem ela; 409 na mesma data e nome, sem diferenciar caixa nem acento) e editar só vale para MUNICIPAL (422 nos outros). Cada teste usa o
/// seu próprio ano (de 2031 em diante, longe da carga de 2026 do <c>V001</c>), porque os testes da classe dividem o mesmo banco.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FeriadosTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/ponto/feriados";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(FeriadosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> ListarAsync(ApiDeAcesso api, string consulta) => await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?{consulta}");

    private static async Task<HttpResponseMessage> CriarAsync(ApiDeAcesso api, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync(Rota, corpo);

    private static async Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}", corpo);

    private static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(f => f.GetProperty("id").GetInt32())];

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static async Task<int> SemearAsync(ApiDeAcesso api, string nome, string data, string tipo = "NACIONAL", string? uf = null, string? municipio = null, bool ativo = true, bool repete = false)
    {
        await ApoioDeTeste.ExecutarAsync(
            api.Conexao,
            "INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO, UF, MUNICIPIO, ATIVO, REPETE_TODO_ANO) VALUES (@nome, @data, @tipo, @uf, @municipio, @ativo, @repete)",
            ("@nome", nome),
            ("@data", DateTime.Parse(data, System.Globalization.CultureInfo.InvariantCulture)),
            ("@tipo", tipo),
            ("@uf", uf),
            ("@municipio", municipio),
            ("@ativo", ativo),
            ("@repete", repete));
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FERIADO FROM FERIADOS WHERE DATA_FERIADO = '{data}' AND NOME_FERIADO = '{nome.Replace("'", "''")}'");
    }

    private static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    private static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int feriadoId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "FERIADOS", feriadoId);

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_traz_os_nacionais_os_estaduais_da_UF_os_municipais_do_municipio_e_os_da_empresa_por_data()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        var nacional = await SemearAsync(api, "Nacional da lista", "2031-03-10");
        var estadual = await SemearAsync(api, "Estadual SP da lista", "2031-04-10", "ESTADUAL", "SP");
        var municipal = await SemearAsync(api, "Municipal SP da lista", "2031-05-10", "MUNICIPAL", "SP", "São Paulo", repete: true);
        var daEmpresa = await SemearAsync(api, "Da empresa da lista", "2031-06-10", "EMPRESA");
        var estadualDeOutraUf = await SemearAsync(api, "Estadual RJ da lista", "2031-04-11", "ESTADUAL", "RJ");
        var municipalDeOutraCidade = await SemearAsync(api, "Municipal Campinas da lista", "2031-05-11", "MUNICIPAL", "SP", "Campinas");
        var municipalDeOutraUf = await SemearAsync(api, "Municipal SP do RJ da lista", "2031-05-12", "MUNICIPAL", "RJ", "São Paulo");
        var inativo = await SemearAsync(api, "Inativo da lista", "2031-07-10", ativo: false);

        var resposta = await ListarAsync(api, "ano=2031");
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal([nacional, estadual, municipal, daEmpresa], Ids(lista));
        Assert.DoesNotContain(estadualDeOutraUf, Ids(lista));
        Assert.DoesNotContain(municipalDeOutraCidade, Ids(lista));
        Assert.DoesNotContain(municipalDeOutraUf, Ids(lista));
        Assert.DoesNotContain(inativo, Ids(lista));
        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal(
            new (string, string, string?, string?, bool, bool, bool)[]
            {
                ("Nacional da lista", "NACIONAL", null, null, false, true, false),
                ("Estadual SP da lista", "ESTADUAL", "SP", null, false, true, false),
                ("Municipal SP da lista", "MUNICIPAL", "SP", "São Paulo", true, true, true),
                ("Da empresa da lista", "EMPRESA", null, null, false, true, false),
            },
            itens.Select(f => (f.GetProperty("nome").GetString()!, f.GetProperty("tipo").GetString()!, f.GetProperty("uf").GetString(), f.GetProperty("municipio").GetString(), f.GetProperty("repeteTodoAno").GetBoolean(), f.GetProperty("ativo").GetBoolean(), f.GetProperty("podeEditar").GetBoolean())));
        Assert.Equal("2031-03-10", itens[0].GetProperty("data").GetString());
    }

    [Fact]
    public async Task O_mes_filtra_e_sem_mes_vem_o_ano_todo_e_incluirInativos_traz_os_desativados_na_ordem_da_data()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        var marco = await SemearAsync(api, "De março do filtro", "2032-03-31");
        var abril = await SemearAsync(api, "De abril do filtro", "2032-04-01");
        var abrilInativo = await SemearAsync(api, "De abril inativo do filtro", "2032-04-15", "MUNICIPAL", "SP", "São Paulo", ativo: false);
        var dezembro = await SemearAsync(api, "De dezembro do filtro", "2032-12-31");
        var proximoAno = await SemearAsync(api, "Do ano seguinte do filtro", "2033-01-01");

        Assert.Equal([marco, abril, dezembro], Ids(await CorpoAsync(await ListarAsync(api, "ano=2032"))));
        Assert.Equal([abril], Ids(await CorpoAsync(await ListarAsync(api, "ano=2032&mes=4"))));
        Assert.Equal([abril, abrilInativo], Ids(await CorpoAsync(await ListarAsync(api, "ano=2032&mes=4&incluirInativos=true"))));
        Assert.Equal([marco, abril, abrilInativo, dezembro], Ids(await CorpoAsync(await ListarAsync(api, "ano=2032&incluirInativos=true"))));
        Assert.Equal([dezembro], Ids(await CorpoAsync(await ListarAsync(api, "ano=2032&mes=12"))));
        Assert.Equal([proximoAno], Ids(await CorpoAsync(await ListarAsync(api, "ano=2033&mes=1"))));
    }

    [Fact]
    public async Task Sem_a_empresa_configurada_a_lista_nao_da_erro_e_traz_so_os_nacionais_e_os_da_empresa()
    {
        var api = await ApiAsync();
        var nacional = await SemearAsync(api, "Nacional sem empresa", "2034-03-10");
        var estadual = await SemearAsync(api, "Estadual sem empresa", "2034-04-10", "ESTADUAL", "SP");
        var municipal = await SemearAsync(api, "Municipal sem empresa", "2034-05-10", "MUNICIPAL", "SP", "São Paulo");
        var daEmpresa = await SemearAsync(api, "Da empresa sem empresa", "2034-06-10", "EMPRESA");
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        try
        {
            var resposta = await ListarAsync(api, "ano=2034");

            Assert.Equal(200, (int)resposta.StatusCode);
            var ids = Ids(await CorpoAsync(resposta));
            Assert.Equal([nacional, daEmpresa], ids);
            Assert.DoesNotContain(estadual, ids);
            Assert.DoesNotContain(municipal, ids);
        }
        finally
        {
            await ConfigurarEmpresaAsync(api.Conexao);
        }
    }

    [Fact]
    public async Task No_mesmo_dia_vem_primeiro_o_nacional_depois_o_estadual_o_municipal_e_o_da_empresa_e_dentro_do_tipo_pelo_nome()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        var empresaZ = await SemearAsync(api, "Empresa", "2041-05-05", "EMPRESA");
        var municipal = await SemearAsync(api, "Beta municipal", "2041-05-05", "MUNICIPAL", "SP", "São Paulo");
        var nacionalZeta = await SemearAsync(api, "Zeta nacional", "2041-05-05");
        var estadual = await SemearAsync(api, "Gama estadual", "2041-05-05", "ESTADUAL", "SP");
        var nacionalAlfa = await SemearAsync(api, "Alfa nacional", "2041-05-05");

        var ids = Ids(await CorpoAsync(await ListarAsync(api, "ano=2041&mes=5")));

        Assert.Equal([nacionalAlfa, nacionalZeta, estadual, municipal, empresaZ], ids);
    }

    [Theory]
    [InlineData("")]
    [InlineData("mes=4")]
    [InlineData("ano=1999")]
    [InlineData("ano=2101")]
    [InlineData("ano=abc")]
    [InlineData("ano=2031&mes=0")]
    [InlineData("ano=2031&mes=13")]
    [InlineData("ano=2031&mes=abril")]
    [InlineData("ano=2031&incluirInativos=talvez")]
    public async Task Ano_ausente_ou_fora_de_2000_a_2100_e_mes_fora_de_1_a_12_dao_400_e_nada_e_listado(string consulta)
    {
        var api = await ApiAsync();

        var resposta = await ListarAsync(api, consulta);

        Assert.Equal(400, (int)resposta.StatusCode);
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2100)]
    public async Task Os_anos_dos_limites_valem(int ano)
    {
        var api = await ApiAsync();

        Assert.Equal(200, (int)(await ListarAsync(api, $"ano={ano}")).StatusCode);
    }

    // ---------------------------------------------------------------- criar

    [Fact]
    public async Task Criar_grava_um_municipal_da_empresa_aparado_e_a_lista_ja_o_mostra_e_o_log_diz_quem_criou()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");

        var resposta = await CriarAsync(api, new { nome = "  Dia de São Judas Tadeu  ", data = "2035-10-28", repeteTodoAno = true });
        var criado = await CorpoAsync(resposta);

        Assert.Equal(201, (int)resposta.StatusCode);
        var id = criado.GetProperty("id").GetInt32();
        Assert.Equal(
            ("Dia de São Judas Tadeu", "2035-10-28", "MUNICIPAL", "SP", "São Paulo", true, true, true),
            (criado.GetProperty("nome").GetString(), criado.GetProperty("data").GetString(), criado.GetProperty("tipo").GetString(), criado.GetProperty("uf").GetString(), criado.GetProperty("municipio").GetString(), criado.GetProperty("repeteTodoAno").GetBoolean(), criado.GetProperty("ativo").GetBoolean(), criado.GetProperty("podeEditar").GetBoolean()));
        Assert.Contains(id, Ids(await CorpoAsync(await ListarAsync(api, "ano=2035&mes=10"))));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Feriados, AuditoriaAcao.Criou, api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.FuncionarioAtorId));
    }

    [Fact]
    public async Task Criar_sem_repeteTodoAno_vale_falso_e_a_UF_e_o_municipio_sao_os_da_empresa_de_agora()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "RJ", "Niterói");

        try
        {
            var criado = await CorpoAsync(await CriarAsync(api, new { nome = "Aniversário de Niterói", data = "2035-11-22" }));

            Assert.Equal((false, "RJ", "Niterói"), (criado.GetProperty("repeteTodoAno").GetBoolean(), criado.GetProperty("uf").GetString(), criado.GetProperty("municipio").GetString()));
        }
        finally
        {
            await ConfigurarEmpresaAsync(api.Conexao);
        }
    }

    [Fact]
    public async Task Nome_vazio_ou_com_mais_de_100_caracteres_e_data_invalida_ou_ausente_dao_400_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM FERIADOS");

        var vazio = await CriarAsync(api, new { nome = "", data = "2036-01-10" });
        var espacos = await CriarAsync(api, new { nome = "     ", data = "2036-01-10" });
        var longo = await CriarAsync(api, new { nome = new string('x', 101), data = "2036-01-10" });
        var dataInvalida = await CriarAsync(api, new { nome = "Data ruim", data = "2036-02-30" });
        var semData = await CriarAsync(api, new { nome = "Sem data" });
        var semCorpo = await (await api.ClienteDoRhAgoraAsync()).PostAsync(Rota, content: null);

        foreach (var resposta in new[] { vazio, espacos, longo, dataInvalida, semData, semCorpo })
        {
            Assert.Equal(400, (int)resposta.StatusCode);
        }

        Assert.All(new[] { vazio, espacos, longo }, r => Assert.True(CorpoAsync(r).GetAwaiter().GetResult().GetProperty("errors").TryGetProperty("nome", out _)));
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM FERIADOS"));
        Assert.Equal(201, (int)(await CriarAsync(api, new { nome = new string('x', 100), data = "2036-01-10" })).StatusCode); // 100 vale
    }

    [Fact]
    public async Task Nome_de_um_caractere_vale()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);

        var resposta = await CriarAsync(api, new { nome = "X", data = "2036-03-01" });

        Assert.Equal(201, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_com_a_mesma_data_e_o_mesmo_nome_da_409_sem_diferenciar_caixa_nem_acento_e_outra_data_ou_outro_nome_passam()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var original = await CorpoAsync(await CriarAsync(api, new { nome = "São João do teste", data = "2036-06-24" }));

        var igual = await CriarAsync(api, new { nome = "São João do teste", data = "2036-06-24" });
        var semAcentoEEmCaixaAlta = await CriarAsync(api, new { nome = "SAO JOAO DO TESTE", data = "2036-06-24" });
        var outraData = await CriarAsync(api, new { nome = "São João do teste", data = "2036-06-25" });
        var outroNome = await CriarAsync(api, new { nome = "Outro nome do teste", data = "2036-06-24" });
        var comONacional = await SemearAsync(api, "Nacional do conflito", "2036-09-07");
        var contraONacional = await CriarAsync(api, new { nome = "Nacional do conflito", data = "2036-09-07" });

        Assert.Equal(new[] { 409, 409, 201, 201, 409 }, new[] { (int)igual.StatusCode, (int)semAcentoEEmCaixaAlta.StatusCode, (int)outraData.StatusCode, (int)outroNome.StatusCode, (int)contraONacional.StatusCode });
        var problema = await CorpoAsync(igual);
        Assert.Equal(("FERIADO_DUPLICADO", "Feriado repetido.", "Já existe um feriado com este nome nesta data."), (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString()));
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM FERIADOS WHERE DATA_FERIADO = '2036-06-24' AND NOME_FERIADO = 'São João do teste'"));
        Assert.True(comONacional > 0);
        Assert.True(original.GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task Criar_sem_a_empresa_configurada_da_422_EMPRESA_NAO_CONFIGURADA_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        try
        {
            var resposta = await CriarAsync(api, new { nome = "Sem empresa", data = "2037-01-10" });
            var problema = await CorpoAsync(resposta);

            Assert.Equal(422, (int)resposta.StatusCode);
            Assert.Equal(("EMPRESA_NAO_CONFIGURADA", "Empresa não configurada.", "Cadastre a UF e o município da empresa antes de criar feriados municipais."), (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString()));
            Assert.Equal(0, await ContarAsync(api, "SELECT COUNT(*) FROM FERIADOS WHERE NOME_FERIADO = 'Sem empresa'"));
        }
        finally
        {
            await ConfigurarEmpresaAsync(api.Conexao);
        }
    }

    // ---------------------------------------------------------------- editar

    [Fact]
    public async Task Editar_muda_nome_data_repeticao_e_situacao_do_municipal_e_o_log_traz_so_o_que_mudou()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var criado = await CorpoAsync(await CriarAsync(api, new { nome = "Para editar", data = "2038-03-10" }));
        var id = criado.GetProperty("id").GetInt32();

        var resposta = await EditarAsync(api, id, new { nome = "  Editado  ", data = "2038-03-11", repeteTodoAno = true, ativo = true });
        var editado = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("Editado", "2038-03-11", "MUNICIPAL", true, true, true), (editado.GetProperty("nome").GetString(), editado.GetProperty("data").GetString(), editado.GetProperty("tipo").GetString(), editado.GetProperty("repeteTodoAno").GetBoolean(), editado.GetProperty("ativo").GetBoolean(), editado.GetProperty("podeEditar").GetBoolean()));
        var eventos = await EventosAsync(api, id);
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Alterou], eventos.OrderBy(e => e.Id).Select(e => e.Acao));
        var campos = eventos.Single(e => e.Acao == AuditoriaAcao.Alterou).Mudancas.Select(m => m.Campo).Order();
        Assert.Equal(new[] { "Data", "Nome", "Repete todo ano" }, campos);
    }

    [Fact]
    public async Task Editar_com_ativo_falso_desativa_sem_apagar_e_so_aparece_com_incluirInativos()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var id = (await CorpoAsync(await CriarAsync(api, new { nome = "Cadastrado por engano", data = "2038-05-10" }))).GetProperty("id").GetInt32();

        var resposta = await EditarAsync(api, id, new { nome = "Cadastrado por engano", data = "2038-05-10", repeteTodoAno = false, ativo = false });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.False((await CorpoAsync(resposta)).GetProperty("ativo").GetBoolean());
        Assert.DoesNotContain(id, Ids(await CorpoAsync(await ListarAsync(api, "ano=2038&mes=5"))));
        Assert.Contains(id, Ids(await CorpoAsync(await ListarAsync(api, "ano=2038&mes=5&incluirInativos=true"))));
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM FERIADOS WHERE ID_FERIADO = {id}"));
        Assert.True((await CorpoAsync(await EditarAsync(api, id, new { nome = "Cadastrado por engano", data = "2038-05-10", repeteTodoAno = false, ativo = true }))).GetProperty("ativo").GetBoolean()); // e volta
    }

    [Fact]
    public async Task Editar_sem_mudar_nada_e_200_e_nao_grava_evento_e_o_proprio_nome_e_data_nao_conflitam_consigo()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var id = (await CorpoAsync(await CriarAsync(api, new { nome = "Igual a si", data = "2038-06-10" }))).GetProperty("id").GetInt32();

        var resposta = await EditarAsync(api, id, new { nome = "Igual a si", data = "2038-06-10", repeteTodoAno = false, ativo = true });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal([AuditoriaAcao.Criou], (await EventosAsync(api, id)).Select(e => e.Acao));
    }

    [Theory]
    [InlineData("NACIONAL", null, null)]
    [InlineData("ESTADUAL", "SP", null)]
    [InlineData("EMPRESA", null, null)]
    public async Task Editar_um_feriado_que_nao_e_municipal_da_422_FERIADO_NAO_EDITAVEL_e_nada_muda(string tipo, string? uf, string? municipio)
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, $"Nao editavel {tipo}", "2039-02-10", tipo, uf, municipio);

        var resposta = await EditarAsync(api, id, new { nome = "Mudou", data = "2039-02-11", repeteTodoAno = true, ativo = false });
        var problema = await CorpoAsync(resposta);

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal(("FERIADO_NAO_EDITAVEL", "Feriado não editável.", "Só feriados municipais podem ser editados."), (Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("detail").GetString()));
        Assert.Equal($"Nao editavel {tipo}", (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_FERIADO FROM FERIADOS WHERE ID_FERIADO = {id}"))?.ToString());
    }

    [Fact]
    public async Task Editar_um_inexistente_e_404_e_editar_para_data_e_nome_de_outro_feriado_e_409()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var primeiro = (await CorpoAsync(await CriarAsync(api, new { nome = "Primeiro do conflito", data = "2039-03-10" }))).GetProperty("id").GetInt32();
        var segundo = (await CorpoAsync(await CriarAsync(api, new { nome = "Segundo do conflito", data = "2039-03-11" }))).GetProperty("id").GetInt32();

        var inexistente = await EditarAsync(api, int.MaxValue, new { nome = "x", data = "2039-03-10", repeteTodoAno = false, ativo = true });
        var conflito = await EditarAsync(api, segundo, new { nome = "primeiro do conflito", data = "2039-03-10", repeteTodoAno = false, ativo = true });

        Assert.Equal((404, 409), ((int)inexistente.StatusCode, (int)conflito.StatusCode));
        Assert.Equal("FERIADO_DUPLICADO", Codigo(await CorpoAsync(conflito)));
        Assert.Equal("Segundo do conflito", (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_FERIADO FROM FERIADOS WHERE ID_FERIADO = {segundo}"))?.ToString());
        Assert.True(primeiro > 0);
    }

    [Fact]
    public async Task Editar_com_nome_vazio_ou_comprido_ou_sem_um_campo_obrigatorio_da_400()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var id = (await CorpoAsync(await CriarAsync(api, new { nome = "Para validar", data = "2039-04-10" }))).GetProperty("id").GetInt32();

        var vazio = await EditarAsync(api, id, new { nome = "", data = "2039-04-10", repeteTodoAno = false, ativo = true });
        var longo = await EditarAsync(api, id, new { nome = new string('x', 101), data = "2039-04-10", repeteTodoAno = false, ativo = true });
        var dataInvalida = await EditarAsync(api, id, new { nome = "Para validar", data = "2039-13-10", repeteTodoAno = false, ativo = true });
        var semAtivo = await EditarAsync(api, id, new { nome = "Para validar", data = "2039-04-10", repeteTodoAno = false });
        var semRepete = await EditarAsync(api, id, new { nome = "Para validar", data = "2039-04-10", ativo = true });

        Assert.All(new[] { vazio, longo, dataInvalida, semAtivo, semRepete }, r => Assert.Equal(400, (int)r.StatusCode));
        Assert.All(new[] { vazio, longo }, r => Assert.True(CorpoAsync(r).GetAwaiter().GetResult().GetProperty("errors").TryGetProperty("nome", out _)));
        Assert.Equal("Para validar", (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_FERIADO FROM FERIADOS WHERE ID_FERIADO = {id}"))?.ToString());
    }

    // ---------------------------------------------------------------- perfis e contrato

    [Fact]
    public async Task So_o_RH_entra_nas_tres_rotas_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var e = api.Elenco;
        var id = (await CorpoAsync(await CriarAsync(api, new { nome = "Para os perfis", data = "2039-05-10" }))).GetProperty("id").GetInt32();

        foreach (var intruso in new[] { e.Gestor, e.MembroA })
        {
            var cliente = await api.ClienteAgoraAsync(intruso.UsuarioId);
            var statuses = new[]
            {
                (int)(await cliente.GetAsync($"{Rota}?ano=2039")).StatusCode,
                (int)(await cliente.PostAsJsonAsync(Rota, new { nome = "Intruso", data = "2039-05-11" })).StatusCode,
                (int)(await cliente.PutAsJsonAsync($"{Rota}/{id}", new { nome = "Intruso", data = "2039-05-10", repeteTodoAno = false, ativo = false })).StatusCode,
            };
            Assert.Equal(new[] { 403, 403, 403 }, statuses);
        }

        var anonimo = api.Api.ClienteAnonimo();
        var semLogin = new[] { (int)(await anonimo.GetAsync($"{Rota}?ano=2039")).StatusCode, (int)(await anonimo.PostAsJsonAsync(Rota, new { nome = "Anônimo", data = "2039-05-11" })).StatusCode, (int)(await anonimo.PutAsJsonAsync($"{Rota}/{id}", new { nome = "Anônimo", data = "2039-05-10", repeteTodoAno = false, ativo = false })).StatusCode };
        Assert.Equal(new[] { 401, 401, 401 }, semLogin);
        Assert.Equal("Para os perfis", (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_FERIADO FROM FERIADOS WHERE ID_FERIADO = {id}"))?.ToString());
        Assert.Equal(0, await ContarAsync(api, "SELECT COUNT(*) FROM FERIADOS WHERE NOME_FERIADO IN ('Intruso', 'Anônimo')"));
    }

    [Fact]
    public async Task As_respostas_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var nacional = await SemearAsync(api, "Nacional do contrato", "2040-04-10");

        await (await ListarAsync(api, "ano=2040")).AfirmarConformeAoContratoAsync("listarFeriados");
        await (await ListarAsync(api, "ano=1999")).AfirmarConformeAoContratoAsync("listarFeriados"); // 400
        var criado = await CriarAsync(api, new { nome = "Do contrato", data = "2040-04-11", repeteTodoAno = true });
        await criado.AfirmarConformeAoContratoAsync("criarFeriado");
        await (await CriarAsync(api, new { nome = "Do contrato", data = "2040-04-11" })).AfirmarConformeAoContratoAsync("criarFeriado"); // 409
        await (await CriarAsync(api, new { nome = "", data = "2040-04-11" })).AfirmarConformeAoContratoAsync("criarFeriado"); // 400
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        try
        {
            await (await CriarAsync(api, new { nome = "Sem empresa do contrato", data = "2040-04-12" })).AfirmarConformeAoContratoAsync("criarFeriado"); // 422
        }
        finally
        {
            await ConfigurarEmpresaAsync(api.Conexao);
        }

        var id = (await CorpoAsync(criado)).GetProperty("id").GetInt32();
        await (await EditarAsync(api, id, new { nome = "Do contrato editado", data = "2040-04-11", repeteTodoAno = true, ativo = true })).AfirmarConformeAoContratoAsync("editarFeriado");
        await (await EditarAsync(api, int.MaxValue, new { nome = "x", data = "2040-04-11", repeteTodoAno = true, ativo = true })).AfirmarConformeAoContratoAsync("editarFeriado"); // 404
        await (await EditarAsync(api, nacional, new { nome = "x", data = "2040-04-10", repeteTodoAno = true, ativo = true })).AfirmarConformeAoContratoAsync("editarFeriado"); // 422
        await (await EditarAsync(api, id, new { nome = "Nacional do contrato", data = "2040-04-10", repeteTodoAno = true, ativo = true })).AfirmarConformeAoContratoAsync("editarFeriado"); // 409
        await (await EditarAsync(api, id, new { nome = "", data = "2040-04-11", repeteTodoAno = true, ativo = true })).AfirmarConformeAoContratoAsync("editarFeriado"); // 400
    }
}
