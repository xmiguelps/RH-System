using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// <c>GET /api/rh/funcionarios</c> (tela 01), no banco REAL com o relógio fixo em 2026-10-07T12:42Z. Cada teste cria o próprio setor e as próprias pessoas e filtra por <c>setorId</c> (ou por um
/// nome com marcador), para o elenco e os outros testes não entrarem na conta.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ListaDeFuncionariosTests(MySqlFixture mysql)
{
    private const string Lista = "/api/rh/funcionarios";

    private static readonly DateOnly Antes = new(2026, 1, 2);
    private static readonly DateOnly Futuro = new(2026, 10, 13);

    private static int _contador;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(ListaDeFuncionariosTests));

    private static string Email() => $"lista.{Interlocked.Increment(ref _contador)}@lista.com.br";

    private static Task<int> CriarAsync(ApiDeAcesso api, string nome, int? setorId, DateOnly? admissao = null, bool ativo = true, string? tipoContrato = "CLT", string? matricula = null) =>
        SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, Email(), admissao ?? Antes, ativo, setorId: setorId, tipoContrato: tipoContrato, matricula: matricula);

    private static async Task<JsonElement> ListarAsync(HttpClient cliente, string consulta)
    {
        var resposta = await cliente.GetAsync($"{Lista}?{consulta}");
        Assert.True(resposta.IsSuccessStatusCode, $"{consulta} => {(int)resposta.StatusCode}: {await resposta.Content.ReadAsStringAsync()}");
        return await CorpoAsync(resposta);
    }

    private static string[] Nomes(JsonElement corpo) => [.. corpo.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("nome").GetString()!)];

    private static int[] Ids(JsonElement corpo) => [.. corpo.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32())];

    [Fact]
    public async Task Lista_padrao_e_ATIVO_ordenada_por_nome_com_pagina_1_e_tamanho_20_e_o_total_respeita_os_filtros()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 1");
        await CriarAsync(api, "Carla lista1", setor);
        await CriarAsync(api, "Ana lista1", setor);
        await CriarAsync(api, "Bia lista1", setor);
        await CriarAsync(api, "Eva lista1", setor, Futuro);
        await CriarAsync(api, "Ivo lista1", setor, ativo: false);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var padrao = await ListarAsync(cliente, $"setorId={setor}");

        Assert.Equal(["Ana lista1", "Bia lista1", "Carla lista1"], Nomes(padrao)); // a aba padrão é ATIVO, por nome
        Assert.Equal((1, 20, 3, 1), (padrao.GetProperty("pagina").GetInt32(), padrao.GetProperty("tamanhoPagina").GetInt32(), padrao.GetProperty("total").GetInt32(), padrao.GetProperty("totalPaginas").GetInt32()));
        Assert.All(padrao.GetProperty("itens").EnumerateArray(), i => Assert.Equal("ATIVO", i.GetProperty("situacao").GetString()));
        Assert.Equal(["Eva lista1"], Nomes(await ListarAsync(cliente, $"setorId={setor}&situacao=EM_ADMISSAO")));
        Assert.Equal(["Ivo lista1"], Nomes(await ListarAsync(cliente, $"setorId={setor}&situacao=INATIVO")));

        // O total acompanha os filtros e a paginação corta a lista.
        var segunda = await ListarAsync(cliente, $"setorId={setor}&tamanhoPagina=2&pagina=2");
        Assert.Equal(["Carla lista1"], Nomes(segunda));
        Assert.Equal((3, 2), (segunda.GetProperty("total").GetInt32(), segunda.GetProperty("totalPaginas").GetInt32()));
        var item = padrao.GetProperty("itens")[0];
        Assert.Equal(("Ana lista1", "2026-01-02"), (item.GetProperty("nome").GetString(), item.GetProperty("dataAdmissao").GetString()));
    }

    [Fact]
    public async Task Totais_ignoram_todos_os_filtros_e_contam_ativos_em_admissao_e_inativos_da_empresa_inteira()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 2");
        await CriarAsync(api, "Total A lista2", setor);
        await CriarAsync(api, "Total B lista2", setor, Futuro);
        await CriarAsync(api, "Total C lista2", setor, ativo: false);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);
        var esperado = (
            Ativos: await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = TRUE AND DATA_ADMISSAO <= '2026-10-07'"),
            EmAdmissao: await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = TRUE AND DATA_ADMISSAO > '2026-10-07'"),
            Inativos: await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = FALSE"));

        foreach (var consulta in new[] { string.Empty, $"setorId={setor}", $"setorId={setor}&situacao=INATIVO", "busca=Total%20A", "tipoContrato=ESTAGIO&situacao=EM_ADMISSAO" })
        {
            var totais = (await ListarAsync(cliente, consulta)).GetProperty("totais");
            Assert.Equal(esperado, (totais.GetProperty("ativos").GetInt64(), totais.GetProperty("emAdmissao").GetInt64(), totais.GetProperty("inativos").GetInt64()));
        }
    }

    [Fact]
    public async Task Situacao_EM_ADMISSAO_chega_com_sublinhado_e_valor_desconhecido_devolve_400()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var emAdmissao = await ListarAsync(cliente, "situacao=EM_ADMISSAO&tamanhoPagina=100");

        Assert.All(emAdmissao.GetProperty("itens").EnumerateArray(), i => Assert.Equal("EM_ADMISSAO", i.GetProperty("situacao").GetString()));
        Assert.Contains("Rafael Torres", Nomes(emAdmissao)); // o do elenco, que começa em 2026-10-13
        foreach (var invalido in new[] { "XYZ", "EmAdmissao", "em_admissao" }) // o nome do C# e a grafia errada não valem
        {
            var resposta = await cliente.GetAsync($"{Lista}?situacao={invalido}");
            Assert.Equal(400, (int)resposta.StatusCode);
            Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty("situacao", out _), invalido);
        }
    }

    [Fact]
    public async Task Quem_tem_desligamento_so_agendado_continua_ATIVO_e_inativo_aparece_so_na_aba_INATIVO()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 4");
        var agendado = await CriarAsync(api, "Agendado lista4", setor);
        await SementeDoCadastro.DesligamentoAgendadoAsync(api.Conexao, agendado, new DateOnly(2026, 10, 30));
        var inativo = await CriarAsync(api, "Inativo lista4", setor, ativo: false);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        Assert.Equal([agendado], Ids(await ListarAsync(cliente, $"setorId={setor}")));
        Assert.Equal([inativo], Ids(await ListarAsync(cliente, $"setorId={setor}&situacao=INATIVO")));
        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setor}&situacao=EM_ADMISSAO")));
    }

    [Fact]
    public async Task Admissao_em_08_10_vista_as_22h_de_07_10_em_Brasilia_e_EM_ADMISSAO_e_vira_ATIVO_as_00h_de_08_10()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 5");
        var pessoa = await CriarAsync(api, "Virada lista5", setor, new DateOnly(2026, 10, 8));

        try
        {
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 1, 0, 0, DateTimeKind.Utc)); // 22:00 de 07/10 em Brasília
            var antes = await ListarAsync(await api.ClienteDoRhAgoraAsync(), $"setorId={setor}&situacao=EM_ADMISSAO");
            Assert.Equal([pessoa], Ids(antes));

            api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc)); // 00:00 de 08/10
            var depois = await ListarAsync(await api.ClienteDoRhAgoraAsync(), $"setorId={setor}");
            Assert.Equal([pessoa], Ids(depois));
            Assert.Empty(Ids(await ListarAsync(await api.ClienteDoRhAgoraAsync(), $"setorId={setor}&situacao=EM_ADMISSAO")));
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Busca_por_nome_ignora_maiusculas_e_acentos_e_trata_percentual_como_literal()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 6");
        var pessoa = await CriarAsync(api, "José Álvaro Tésté lista6", setor);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        foreach (var busca in new[] { "jose alvaro", "ALVARO", "teste lista6", "José" })
        {
            Assert.Equal([pessoa], Ids(await ListarAsync(cliente, $"setorId={setor}&busca={Uri.EscapeDataString(busca)}")));
        }

        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setor}&busca=%25"))); // "%" procura o caractere, não "tudo"
        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setor}&busca=_")));
    }

    [Fact]
    public async Task Busca_por_CPF_com_ou_sem_mascara_e_por_matricula_com_ou_sem_zeros_a_esquerda()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 7");
        var pessoa = await CriarAsync(api, "Documentos lista7", setor, matricula: "00482");
        await SementeDoCadastro.DetalhesAsync(api.Conexao, pessoa, "52998224725");
        var outra = await CriarAsync(api, "Outra lista7", setor, matricula: "00999");
        await SementeDoCadastro.DetalhesAsync(api.Conexao, outra, "11144477735");
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        foreach (var busca in new[] { "529.982.247-25", "52998224725", "529 982 247 25", "00482", "482" })
        {
            Assert.Equal([pessoa], Ids(await ListarAsync(cliente, $"setorId={setor}&busca={Uri.EscapeDataString(busca)}")));
        }

        Assert.Equal([outra], Ids(await ListarAsync(cliente, $"setorId={setor}&busca=111.444.777-35")));
        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setor}&busca=00000")));
    }

    [Fact]
    public async Task Busca_com_letras_e_numeros_procura_so_no_nome()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 8");
        var comMatricula = await CriarAsync(api, "Sem numero no nome lista8", setor, matricula: "10482");
        var comNumeroNoNome = await CriarAsync(api, "Equipe 482 lista8", setor);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        // Só dígitos: procura nome, CPF e matrícula (acha os dois). Com letra no termo: só o nome.
        Assert.Equal(new[] { comMatricula, comNumeroNoNome }.Order(), Ids(await ListarAsync(cliente, $"setorId={setor}&busca=482")).Order());
        Assert.Equal([comNumeroNoNome], Ids(await ListarAsync(cliente, $"setorId={setor}&busca=equipe%20482")));
        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setor}&busca=lista8%2010482"))); // "10482" aqui é parte de um termo com letras: não vai à matrícula
    }

    [Fact]
    public async Task Filtra_por_setorId_e_tipoContrato_e_combina_com_a_busca_e_a_situacao()
    {
        var api = await ApiAsync();
        var setorA = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 9A");
        var setorB = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 9B");
        var clt = await CriarAsync(api, "Clt lista9", setorA, tipoContrato: "CLT");
        var estagio = await CriarAsync(api, "Estagio lista9", setorA, tipoContrato: "ESTAGIO");
        var emOutroSetor = await CriarAsync(api, "Clt outro lista9", setorB, tipoContrato: "CLT");
        var estagioFuturo = await CriarAsync(api, "Estagio futuro lista9", setorA, Futuro, tipoContrato: "ESTAGIO");
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        Assert.Equal(new[] { clt, estagio }.Order(), Ids(await ListarAsync(cliente, $"setorId={setorA}")).Order());
        Assert.Equal([clt], Ids(await ListarAsync(cliente, $"setorId={setorA}&tipoContrato=CLT")));
        Assert.Equal([estagio], Ids(await ListarAsync(cliente, $"setorId={setorA}&tipoContrato=ESTAGIO")));
        Assert.Equal([emOutroSetor], Ids(await ListarAsync(cliente, $"setorId={setorB}")));
        Assert.Equal([estagioFuturo], Ids(await ListarAsync(cliente, $"setorId={setorA}&tipoContrato=ESTAGIO&situacao=EM_ADMISSAO")));
        Assert.Equal([clt], Ids(await ListarAsync(cliente, $"setorId={setorA}&tipoContrato=CLT&busca=clt%20lista9")));
        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setorA}&tipoContrato=CLT&busca=estagio")));
        var invalido = await cliente.GetAsync($"{Lista}?tipoContrato=PJ");
        Assert.Equal(400, (int)invalido.StatusCode);
        Assert.True((await CorpoAsync(invalido)).GetProperty("errors").TryGetProperty("tipoContrato", out _));
    }

    [Fact]
    public async Task OrdenarPor_DATA_ADMISSAO_com_direcao_e_desempate_nao_repete_nem_pula_item_entre_paginas_e_ordenarPor_desconhecido_devolve_400()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 10");
        var mesmaData = new DateOnly(2026, 3, 1);
        var criados = new List<int>
        {
            await CriarAsync(api, "Zeta lista10", setor, mesmaData),
            await CriarAsync(api, "Alfa lista10", setor, mesmaData),
            await CriarAsync(api, "Beta lista10", setor, mesmaData),
            await CriarAsync(api, "Gama lista10", setor, new DateOnly(2026, 2, 1)),
            await CriarAsync(api, "Delta lista10", setor, new DateOnly(2026, 4, 1)),
        };
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var vistos = new List<int>();
        for (var pagina = 1; pagina <= 3; pagina++)
        {
            vistos.AddRange(Ids(await ListarAsync(cliente, $"setorId={setor}&ordenarPor=DATA_ADMISSAO&direcao=ASC&tamanhoPagina=2&pagina={pagina}")));
        }

        Assert.Equal(criados.Order(), vistos.Order()); // nenhum item repetido nem perdido entre as páginas
        Assert.Equal(5, vistos.Distinct().Count());
        var asc = Nomes(await ListarAsync(cliente, $"setorId={setor}&ordenarPor=DATA_ADMISSAO&direcao=ASC&tamanhoPagina=100"));
        Assert.Equal("Gama lista10", asc[0]); // a admissão mais antiga primeiro
        Assert.Equal("Delta lista10", asc[^1]);
        var desc = Nomes(await ListarAsync(cliente, $"setorId={setor}&ordenarPor=DATA_ADMISSAO&direcao=DESC&tamanhoPagina=100"));
        Assert.Equal("Delta lista10", desc[0]);
        Assert.Equal("Gama lista10", desc[^1]);
        Assert.Equal(["Alfa lista10", "Beta lista10", "Delta lista10", "Gama lista10", "Zeta lista10"], Nomes(await ListarAsync(cliente, $"setorId={setor}&ordenarPor=NOME&tamanhoPagina=100")));

        var invalido = await cliente.GetAsync($"{Lista}?ordenarPor=XYZ");
        Assert.Equal(400, (int)invalido.StatusCode);
        Assert.True((await CorpoAsync(invalido)).GetProperty("errors").TryGetProperty("ordenarPor", out _));
    }

    [Fact]
    public async Task SituacaoHoje_so_vem_para_ATIVO_e_null_para_EM_ADMISSAO_e_INATIVO()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 11");
        await CriarAsync(api, "Ativo lista11", setor);
        await CriarAsync(api, "Futuro lista11", setor, Futuro);
        await CriarAsync(api, "Inativo lista11", setor, ativo: false);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var ativo = (await ListarAsync(cliente, $"setorId={setor}")).GetProperty("itens")[0];
        var futuro = (await ListarAsync(cliente, $"setorId={setor}&situacao=EM_ADMISSAO")).GetProperty("itens")[0];
        var inativo = (await ListarAsync(cliente, $"setorId={setor}&situacao=INATIVO")).GetProperty("itens")[0];

        Assert.Equal("AINDA_NAO_COMECOU", ativo.GetProperty("situacaoHoje").GetString()); // quarta, sem ponto nem horário configurado (decisão 1)
        Assert.Equal(JsonValueKind.Null, futuro.GetProperty("situacaoHoje").ValueKind);
        Assert.Equal(JsonValueKind.Null, inativo.GetProperty("situacaoHoje").ValueKind);
    }

    [Fact]
    public async Task Tipo_de_contrato_fora_de_CLT_e_ESTAGIO_no_banco_sai_null_e_nao_quebra_a_lista()
    {
        var api = await ApiAsync();
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor lista 12");
        var pj = await CriarAsync(api, "Pj lista12", setor, tipoContrato: "PJ");
        var semTipo = await CriarAsync(api, "Sem tipo lista12", setor, tipoContrato: null);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        var todos = await ListarAsync(cliente, $"setorId={setor}");

        Assert.Equal(new[] { pj, semTipo }.Order(), Ids(todos).Order()); // a leitura não cai
        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setor}&tipoContrato=CLT"))); // e nenhum dos dois é CLT nem ESTAGIO
        Assert.Empty(Ids(await ListarAsync(cliente, $"setorId={setor}&tipoContrato=ESTAGIO")));
    }
}
