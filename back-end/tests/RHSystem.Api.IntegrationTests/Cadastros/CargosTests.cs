using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Cadastros;

/// <summary>
/// <c>/api/rh/cargos</c> (tela 29, cadastro "Cargos"), só para o RH, no banco REAL com o relógio fixo em 2026-10-07T12:42Z. Cada teste cria os cargos dele (nome com um marcador próprio, para a busca isolá-los dos
/// outros testes). O nome é único sem diferenciar maiúsculas nem acentos (chave única em <c>utf8mb4_unicode_ci</c>).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CargosTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/cargos";
    private const string NomeRepetido = "Já existe um cadastro com este nome.";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(CargosTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static object Corpo(string nome, string? descricao = null, decimal carga = 44m) => new { nome, descricao, cargaSemanalHoras = carga };

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int cargoId, bool ativo = true, DateOnly? admissao = null) =>
        SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, $"{Guid.NewGuid():N}@cargos.com.br", admissao ?? new DateOnly(2026, 1, 2), ativo, cargoId: cargoId);

    // ---------------------------------------------------------------- lista

    [Fact]
    public async Task Lista_devolve_nome_descricao_carga_e_quantidadePessoas_so_de_ativos_ordenada_por_nome()
    {
        var api = await ApiAsync();
        var segundo = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Lista1 B analista", "Contas a pagar", 44m);
        var primeiro = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Lista1 A estagiário", null, 30m);
        await PessoaAsync(api, "Lista1 ativa 1", segundo);
        await PessoaAsync(api, "Lista1 ativa 2", segundo);
        await PessoaAsync(api, "Lista1 inativa", segundo, ativo: false);
        await PessoaAsync(api, "Lista1 em admissão", segundo, admissao: new DateOnly(2026, 12, 1)); // ativa, admitida no futuro: conta (o contrato só diz "ativas")

        var itens = await ItensAsync(await ComoRh(api).GetAsync($"{Rota}?busca=Lista1&tamanhoPagina=100"));

        Assert.Equal(["Lista1 A estagiário", "Lista1 B analista"], itens.Select(i => i.GetProperty("nome").GetString()));
        Assert.Equal((primeiro, JsonValueKind.Null, 30m, 0), (itens[0].GetProperty("id").GetInt32(), itens[0].GetProperty("descricao").ValueKind, itens[0].GetProperty("cargaSemanalHoras").GetDecimal(), itens[0].GetProperty("quantidadePessoas").GetInt32()));
        Assert.Equal((segundo, "Contas a pagar", 44m, 3), (itens[1].GetProperty("id").GetInt32(), itens[1].GetProperty("descricao").GetString(), itens[1].GetProperty("cargaSemanalHoras").GetDecimal(), itens[1].GetProperty("quantidadePessoas").GetInt32()));
    }

    [Fact]
    public async Task Busca_procura_no_nome_e_na_descricao_e_trata_percentual_como_literal()
    {
        var api = await ApiAsync();
        await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Busca2 Alfa", "reajuste de 100% no piso", 44m);
        await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Busca2 Beta", "reajuste de 1000 reais", 44m);
        await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Busca2 Gama", "trabalha com zebras", 44m);
        var cliente = ComoRh(api);

        async Task<List<string>> NomesAsync(string busca) =>
            [.. (await ItensAsync(await cliente.GetAsync($"{Rota}?busca={Codificar(busca)}&tamanhoPagina=100"))).Select(i => i.GetProperty("nome").GetString()!)];

        Assert.Equal(["Busca2 Alfa"], await NomesAsync("100%")); // o % é o caractere, não um curinga: "1000" não casa
        Assert.Equal(["Busca2 Gama"], await NomesAsync("zebras")); // na descrição
        Assert.Equal(["Busca2 Alfa"], await NomesAsync("BUSCA2 ALFA")); // no nome, sem diferenciar maiúsculas
        Assert.Equal(["Busca2 Alfa", "Busca2 Beta", "Busca2 Gama"], await NomesAsync("busca2"));
    }

    [Fact]
    public async Task Total_serve_ao_numero_do_menu_com_tamanhoPagina_1_e_tamanhoPagina_100_e_aceito_e_101_devolve_400()
    {
        var api = await ApiAsync();
        await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Total3 um", null, 44m);
        await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Total3 dois", null, 44m);
        var cliente = ComoRh(api);

        var umaLinha = await CorpoAsync(await cliente.GetAsync($"{Rota}?tamanhoPagina=1"));

        Assert.Single(umaLinha.GetProperty("itens").EnumerateArray());
        Assert.Equal(await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM CARGOS"), umaLinha.GetProperty("total").GetInt64());
        Assert.Equal(200, (int)(await cliente.GetAsync($"{Rota}?tamanhoPagina=100")).StatusCode);
        Assert.True((await ErrosAsync(await cliente.GetAsync($"{Rota}?tamanhoPagina=101"))).TryGetProperty("tamanhoPagina", out _));
    }

    // ---------------------------------------------------------------- criar

    [Fact]
    public async Task Criar_devolve_201_com_quantidadePessoas_0_e_grava_Criou_na_area_Cadastros_com_ator_e_ip()
    {
        var api = await ApiAsync();

        var resposta = await ComoRh(api).PostAsJsonAsync(Rota, Corpo("Criar4 Analista de compras", "Cotações e pedidos", 44m));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.Equal(("Criar4 Analista de compras", "Cotações e pedidos", 44m, 0), (corpo.GetProperty("nome").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("cargaSemanalHoras").GetDecimal(), corpo.GetProperty("quantidadePessoas").GetInt32()));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "CARGOS", id));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Criou, "Cargo Criar4 Analista de compras", api.Elenco.Rh.FuncionarioId, TipoDeAtor.Pessoa, "127.0.0.1"), (evento.Area, evento.Acao, evento.Descricao, evento.FuncionarioAtorId, evento.AtorTipo, evento.Ip));
    }

    [Fact]
    public async Task Criar_com_nome_repetido_sem_diferenciar_maiusculas_nem_acentos_devolve_409_NOME_JA_CADASTRADO_com_o_texto_do_contrato()
    {
        var api = await ApiAsync();
        await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Criar5 Estagiário", null, 30m);
        var cliente = ComoRh(api);

        foreach (var repetido in new[] { "Criar5 Estagiário", "criar5 ESTAGIÁRIO", "criar5 estagiario" })
        {
            var resposta = await cliente.PostAsJsonAsync(Rota, Corpo(repetido));
            Assert.Equal((409, "NOME_JA_CADASTRADO", NomeRepetido), await ProblemaAsync(resposta));
        }

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM CARGOS WHERE NOME_CARGO LIKE 'Criar5%'"));
    }

    [Fact]
    public async Task Vinte_criacoes_simultaneas_do_mesmo_nome_dao_um_201_e_dezenove_409_e_nenhum_500()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);

        var respostas = await EmSimultaneoAsync(20, _ => cliente.PostAsJsonAsync(Rota, Corpo("Corrida6 Cargo disputado")));

        Assert.Equal(1, respostas.Count(r => (int)r.StatusCode == 201));
        var recusadas = respostas.Where(r => (int)r.StatusCode != 201).ToList();
        Assert.Equal(19, recusadas.Count);
        foreach (var recusada in recusadas)
        {
            Assert.Equal((409, "NOME_JA_CADASTRADO", NomeRepetido), await ProblemaAsync(recusada));
        }

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM CARGOS WHERE NOME_CARGO = 'Corrida6 Cargo disputado'"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("168.01")]
    [InlineData("44.555")]
    public async Task Carga_zero_negativa_acima_de_168_ou_com_tres_casas_devolve_400_em_cargaSemanalHoras(string carga)
    {
        var api = await ApiAsync();
        var nome = $"Carga7 invalida {carga}";

        var resposta = await ComoRh(api).PostAsync(Rota, Json($$"""{"nome":"{{nome}}","descricao":null,"cargaSemanalHoras":{{carga}}}"""));

        Assert.True((await ErrosAsync(resposta)).TryGetProperty("cargaSemanalHoras", out _));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM CARGOS WHERE NOME_CARGO = '{nome}'"));
    }

    [Fact]
    public async Task Carga_168_e_44_5_sao_aceitas_e_voltam_como_foram_enviadas()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);

        var limite = await CorpoAsync(await cliente.PostAsJsonAsync(Rota, Corpo("Carga7 limite 168", carga: 168m)));
        var quebrada = await CorpoAsync(await cliente.PostAsJsonAsync(Rota, Corpo("Carga7 quebrada 44,5", carga: 44.5m)));

        Assert.Equal((168m, 44.5m), (limite.GetProperty("cargaSemanalHoras").GetDecimal(), quebrada.GetProperty("cargaSemanalHoras").GetDecimal()));
    }

    [Fact]
    public async Task Nome_vazio_so_espacos_ou_acima_de_100_devolve_400_em_nome_e_descricao_acima_de_255_em_descricao()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);

        foreach (var nome in new[] { string.Empty, "   ", new string('n', 101) })
        {
            Assert.True((await ErrosAsync(await cliente.PostAsJsonAsync(Rota, Corpo(nome)))).TryGetProperty("nome", out _), $"nome \"{nome}\"");
        }

        var comDescricaoLonga = await ErrosAsync(await cliente.PostAsJsonAsync(Rota, Corpo("Nome8 valido", new string('d', 256))));
        Assert.True(comDescricaoLonga.TryGetProperty("descricao", out _));
        Assert.False(comDescricaoLonga.TryGetProperty("nome", out _)); // só o campo errado é acusado
        Assert.Equal(201, (int)(await cliente.PostAsJsonAsync(Rota, Corpo(new string('N', 100), new string('d', 255)))).StatusCode); // 100 e 255 são o limite
    }

    [Theory]
    [InlineData("""{"descricao":null,"cargaSemanalHoras":44}""", "nome")]
    [InlineData("""{"nome":"Obrigatorio9 sem carga","descricao":null}""", "cargaSemanalHoras")]
    public async Task Corpo_sem_campo_obrigatorio_do_contrato_devolve_400(string corpo, string campo)
    {
        var api = await ApiAsync();

        var resposta = await ComoRh(api).PostAsync(Rota, Json(corpo));

        Assert.True((await ErrosAsync(resposta)).TryGetProperty(campo, out _));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM CARGOS WHERE NOME_CARGO LIKE 'Obrigatorio9%'"));
    }

    [Fact]
    public async Task Descricao_com_marcacao_html_e_guardada_e_devolvida_como_texto_literal()
    {
        var api = await ApiAsync();

        var criado = await CorpoAsync(await ComoRh(api).PostAsJsonAsync(Rota, Corpo("Html10 cargo", "<b>negrito</b> & <script>x</script>")));

        Assert.Equal("<b>negrito</b> & <script>x</script>", criado.GetProperty("descricao").GetString());
        Assert.Equal("<b>negrito</b> & <script>x</script>", await Sql.EscalarAsync(api.Conexao, $"SELECT DESCRICAO FROM CARGOS WHERE ID_CARGO = {criado.GetProperty("id").GetInt32()}"));
    }

    // ---------------------------------------------------------------- editar

    [Fact]
    public async Task Editar_substitui_os_tres_campos_e_grava_Alterou_com_antes_e_depois()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Editar11 antes", "descrição antiga", 44m);
        await PessoaAsync(api, "Editar11 pessoa", id);

        var resposta = await ComoRh(api).PutAsJsonAsync($"{Rota}/{id}", Corpo("Editar11 depois", "descrição nova", 40m));

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((id, "Editar11 depois", "descrição nova", 40m, 1), (corpo.GetProperty("id").GetInt32(), corpo.GetProperty("nome").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("cargaSemanalHoras").GetDecimal(), corpo.GetProperty("quantidadePessoas").GetInt32()));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "CARGOS", id));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Alterou, api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.FuncionarioAtorId));
        Assert.Contains(evento.Mudancas, m => m is { Campo: "Nome", ValorAntes: "Editar11 antes", ValorDepois: "Editar11 depois" });
        Assert.Contains(evento.Mudancas, m => m is { Campo: "Descrição", ValorAntes: "descrição antiga", ValorDepois: "descrição nova" });
        var carga = Assert.Single(evento.Mudancas, m => m.Campo == "Carga semanal");
        Assert.NotEqual(carga.ValorAntes, carga.ValorDepois);
    }

    [Fact]
    public async Task Editar_cargo_inexistente_devolve_404_nome_de_outro_cargo_409_e_o_proprio_nome_nao_da_409()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var a = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Editar12 A", null, 44m);
        var b = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Editar12 B", null, 44m);

        Assert.Equal(404, (int)(await cliente.PutAsJsonAsync($"{Rota}/{int.MaxValue}", Corpo("Editar12 qualquer"))).StatusCode);
        Assert.Equal(404, (int)(await cliente.PutAsJsonAsync($"{Rota}/{int.MaxValue}", Corpo("Editar12 qualquer", carga: 0m))).StatusCode); // a ordem é 404, 400, 409
        Assert.Equal((409, "NOME_JA_CADASTRADO", NomeRepetido), await ProblemaAsync(await cliente.PutAsJsonAsync($"{Rota}/{b}", Corpo("editar12 a"))));
        Assert.Equal(200, (int)(await cliente.PutAsJsonAsync($"{Rota}/{a}", Corpo("Editar12 A", "só a descrição mudou"))).StatusCode);
        Assert.Equal(200, (int)(await cliente.PutAsJsonAsync($"{Rota}/{a}", Corpo("EDITAR12 a"))).StatusCode); // o próprio nome, com outra caixa
        Assert.Equal("EDITAR12 a", await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_CARGO FROM CARGOS WHERE ID_CARGO = {a}"));
    }

    [Fact]
    public async Task Cargo_sem_carga_no_banco_sai_com_carga_0_e_a_lista_nao_quebra()
    {
        var api = await ApiAsync();
        var logs = new ColetorDeLogs();
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = ComoRh(api).DefaultRequestHeaders.Authorization;
        var id = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "SemCarga13 cargo", null, null); // só por SQL: o contrato não admite cargo sem carga

        var itens = await ItensAsync(await cliente.GetAsync($"{Rota}?busca=SemCarga13"));

        var item = Assert.Single(itens);
        Assert.Equal((id, 0m), (item.GetProperty("id").GetInt32(), item.GetProperty("cargaSemanalHoras").GetDecimal()));
        Assert.Contains(logs.Mensagens, m => m.StartsWith("[Warning]", StringComparison.Ordinal) && m.Contains(id.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- permissões e contrato

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Permissao14 cargo", "intacto", 44m);
        var rotas = new (HttpMethod Metodo, string Caminho)[] { (HttpMethod.Get, Rota), (HttpMethod.Post, Rota), (HttpMethod.Put, $"{Rota}/{id}") };

        foreach (var (metodo, caminho) in rotas)
        {
            foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
            {
                var pedido = new HttpRequestMessage(metodo, caminho) { Content = metodo == HttpMethod.Get ? null : JsonContent.Create(Corpo("Permissao14 invasor")) };
                Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).SendAsync(pedido)).StatusCode);
            }

            Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().SendAsync(new HttpRequestMessage(metodo, caminho))).StatusCode);
        }

        Assert.Equal(("Permissao14 cargo", "intacto"), (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_CARGO FROM CARGOS WHERE ID_CARGO = {id}"), await Sql.EscalarAsync(api.Conexao, $"SELECT DESCRICAO FROM CARGOS WHERE ID_CARGO = {id}")));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM CARGOS WHERE NOME_CARGO = 'Permissao14 invasor'"));
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var id = await ApoioDeTeste.CriarCargoAsync(api.Conexao, "Schema15 cargo", "descrição", 44m);

        await (await cliente.GetAsync($"{Rota}?busca=Schema15")).AfirmarConformeAoContratoAsync("listarCargos");
        await (await cliente.GetAsync($"{Rota}?tamanhoPagina=0")).AfirmarConformeAoContratoAsync("listarCargos"); // o 400
        await (await cliente.PostAsJsonAsync(Rota, Corpo("Schema15 criado"))).AfirmarConformeAoContratoAsync("criarCargo");
        await (await cliente.PostAsJsonAsync(Rota, Corpo("Schema15 criado"))).AfirmarConformeAoContratoAsync("criarCargo"); // o 409
        await (await cliente.PutAsJsonAsync($"{Rota}/{id}", Corpo("Schema15 editado", null, 40m))).AfirmarConformeAoContratoAsync("editarCargo");
        await (await cliente.PutAsJsonAsync($"{Rota}/{int.MaxValue}", Corpo("Schema15 nenhum"))).AfirmarConformeAoContratoAsync("editarCargo"); // o 404
    }
}
