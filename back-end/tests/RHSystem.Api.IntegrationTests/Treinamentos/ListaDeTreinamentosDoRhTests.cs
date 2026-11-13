using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>listarTreinamentos</c> e <c>obterIndicadoresDeTreinamentos</c>, no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 local). A situação sai das datas (encerrado vale mais que tudo; rascunho não está publicado; data de início depois de hoje = agendado; hoje ou sem data = em andamento) e a MESMA conta serve ao filtro, às contagens e à linha; as contagens ignoram a aba e respeitam a busca;
/// cada teste isola as suas linhas por um prefixo de título (a busca), porque os testes da classe dividem o banco. Os indicadores se conferem por diferença antes e depois.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ListaDeTreinamentosDoRhTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/treinamentos";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ListaDeTreinamentosDoRhTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> ListarAsync(ApiDeAcesso api, string consulta) => await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?{consulta}");

    private static async Task<JsonElement> IndicadoresAsync(ApiDeAcesso api) => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/resumo"));

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento}, {pessoa}, '{status}')");

    private static async Task DataAsync(ApiDeAcesso api, int treinamento, string dia) => await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = '{dia}' WHERE ID_TREINAMENTO = {treinamento}");

    private static int[] Ids(JsonElement corpo) => [.. corpo.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32())];

    [Fact]
    public async Task A_situacao_na_fronteira_da_data_e_a_mesma_no_filtro_nas_contagens_e_na_linha_e_a_busca_isola_as_linhas()
    {
        var api = await ApiAsync();
        var p = Titulo("ZLis1");
        var rascunho = await SemearAsync(api, $"{p} rascunho");
        await DataAsync(api, rascunho, "2026-10-09"); // rascunho com data continua rascunho
        var amanha = await SemearAsync(api, $"{p} amanha", "EM_ANDAMENTO");
        await DataAsync(api, amanha, "2026-10-08");
        var hoje = await SemearAsync(api, $"{p} hoje", "EM_ANDAMENTO");
        await DataAsync(api, hoje, "2026-10-07");
        var semData = await SemearAsync(api, $"{p} sem data", "EM_ANDAMENTO");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = NULL WHERE ID_TREINAMENTO = {semData}");
        var encerrado = await SemearAsync(api, $"{p} encerrado", "ENCERRADO");
        _ = await SemearAsync(api, Titulo("ZOutro"), "EM_ANDAMENTO"); // fora da busca

        var todos = await ListarAsync(api, $"busca={p}");
        var andamento = await ListarAsync(api, $"busca={p}&situacao=EM_ANDAMENTO");
        var agendados = await ListarAsync(api, $"busca={p}&situacao=AGENDADO");
        var rascunhos = await ListarAsync(api, $"busca={p}&situacao=RASCUNHO");
        var encerrados = await ListarAsync(api, $"busca={p}&situacao=ENCERRADO");

        Assert.Equal([200, 200, 200, 200, 200], new[] { todos, andamento, agendados, rascunhos, encerrados }.Select(r => (int)r.StatusCode));
        await todos.AfirmarConformeAoContratoAsync("listarTreinamentos");
        var corpo = await CorpoAsync(todos);
        var contagens = corpo.GetProperty("contagens");
        Assert.Equal((5, 1, 1, 2, 1), (contagens.GetProperty("todos").GetInt32(), contagens.GetProperty("rascunhos").GetInt32(), contagens.GetProperty("agendados").GetInt32(), contagens.GetProperty("emAndamento").GetInt32(), contagens.GetProperty("encerrados").GetInt32()));
        Assert.Equal(5, corpo.GetProperty("total").GetInt32());
        var situacoes = corpo.GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetInt32(), i => i.GetProperty("situacao").GetString());
        Assert.Equal(("RASCUNHO", "AGENDADO", "EM_ANDAMENTO", "EM_ANDAMENTO", "ENCERRADO"), (situacoes[rascunho], situacoes[amanha], situacoes[hoje], situacoes[semData], situacoes[encerrado]));
        Assert.Equal([hoje, semData], Ids(await CorpoAsync(andamento)).Order()); // o filtro usa a mesma conta da linha
        Assert.Equal([amanha], Ids(await CorpoAsync(agendados)));
        Assert.Equal([rascunho], Ids(await CorpoAsync(rascunhos)));
        Assert.Equal([encerrado], Ids(await CorpoAsync(encerrados)));
        var contagemComAba = (await CorpoAsync(andamento)).GetProperty("contagens"); // as contagens ignoram a aba
        Assert.Equal((5, 1, 1, 2, 1), (contagemComAba.GetProperty("todos").GetInt32(), contagemComAba.GetProperty("rascunhos").GetInt32(), contagemComAba.GetProperty("agendados").GetInt32(), contagemComAba.GetProperty("emAndamento").GetInt32(), contagemComAba.GetProperty("encerrados").GetInt32()));
        Assert.Equal("2026-10-01T03:00:00Z", (await CorpoAsync(encerrados)).GetProperty("itens")[0].GetProperty("encerradoEmUtc").GetString()); // o banco guarda -03:00; a resposta é UTC
        Assert.Equal(JsonValueKind.Null, (await CorpoAsync(rascunhos)).GetProperty("itens")[0].GetProperty("encerradoEmUtc").ValueKind);
    }

    [Fact]
    public async Task Cada_linha_traz_modulos_participantes_concluidos_e_certificados_emitidos_so_de_quem_emite()
    {
        var api = await ApiAsync();
        var p = Titulo("ZLis2");
        var emite = await SemearAsync(api, $"{p} emite", "EM_ANDAMENTO", obrigatorio: true, publico: "SETORES", emite: true);
        var naoEmite = await SemearAsync(api, $"{p} nao emite", "EM_ANDAMENTO", obrigatorio: false, publico: "PESSOAS", emite: false, onboarding: true);
        var rascunho = await SemearAsync(api, $"{p} rascunho", publico: null);
        _ = await ModuloAsync(api, emite, 1);
        _ = await ModuloAsync(api, emite, 2);
        _ = await ModuloAsync(api, rascunho, 1);
        foreach (var status in new[] { "CONCLUIDO", "CONCLUIDO", "INSCRITO", "EM_ANDAMENTO" })
        {
            await LinhaAsync(api, emite, await PessoaAsync(api, "ZLis2 pessoa"), status);
        }

        await LinhaAsync(api, naoEmite, await PessoaAsync(api, "ZLis2 pessoa"), "CONCLUIDO");

        var corpo = await CorpoAsync(await ListarAsync(api, $"busca={p}"));
        var porId = corpo.GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetInt32());

        (int, int, int, int) Numeros(int id) => (porId[id].GetProperty("totalModulos").GetInt32(), porId[id].GetProperty("totalParticipantes").GetInt32(), porId[id].GetProperty("concluidos").GetInt32(), porId[id].GetProperty("certificadosEmitidos").GetInt32());
        Assert.Equal((2, 4, 2, 2), Numeros(emite));
        Assert.Equal((0, 1, 1, 0), Numeros(naoEmite)); // concluiu, mas não emite: concluidos 1 e certificados 0
        Assert.Equal((1, 0, 0, 0), Numeros(rascunho));
        Assert.Equal(("SETORES", "PESSOAS", JsonValueKind.Null), (porId[emite].GetProperty("publico").GetString(), porId[naoEmite].GetProperty("publico").GetString(), porId[rascunho].GetProperty("publico").ValueKind));
        Assert.Equal((true, false, true, false), (porId[emite].GetProperty("obrigatorio").GetBoolean(), porId[naoEmite].GetProperty("obrigatorio").GetBoolean(), porId[naoEmite].GetProperty("onboarding").GetBoolean(), porId[naoEmite].GetProperty("emiteCertificado").GetBoolean()));
    }

    [Fact]
    public async Task A_ordem_padrao_e_pela_data_de_inicio_decrescente_com_desempate_pelo_id_e_o_titulo_e_a_direcao_mudam_a_ordem()
    {
        var api = await ApiAsync();
        var p = Titulo("ZLis3");
        var b = await SemearAsync(api, $"{p} B", "EM_ANDAMENTO");
        await DataAsync(api, b, "2026-08-01");
        var a = await SemearAsync(api, $"{p} A", "EM_ANDAMENTO");
        await DataAsync(api, a, "2026-09-01");
        var c = await SemearAsync(api, $"{p} C", "EM_ANDAMENTO");
        await DataAsync(api, c, "2026-09-01");

        var padrao = Ids(await CorpoAsync(await ListarAsync(api, $"busca={p}")));
        var porDataAsc = Ids(await CorpoAsync(await ListarAsync(api, $"busca={p}&ordenarPor=COMECA_EM&direcao=ASC")));
        var porTituloPadrao = Ids(await CorpoAsync(await ListarAsync(api, $"busca={p}&ordenarPor=TITULO")));
        var porTituloAsc = Ids(await CorpoAsync(await ListarAsync(api, $"busca={p}&ordenarPor=TITULO&direcao=ASC")));
        var soDirecao = Ids(await CorpoAsync(await ListarAsync(api, $"busca={p}&direcao=ASC")));

        Assert.Equal([a, c, b], padrao); // 01/09 (A e C, desempate pelo id) e depois 01/08
        Assert.Equal([b, a, c], porDataAsc);
        Assert.Equal([c, b, a], porTituloPadrao); // a direção padrão é DESC com qualquer coluna
        Assert.Equal([a, b, c], porTituloAsc);
        Assert.Equal([b, a, c], soDirecao);
    }

    [Fact]
    public async Task A_pagina_traz_o_total_e_o_total_de_paginas_e_a_busca_nao_diferencia_caixa()
    {
        var api = await ApiAsync();
        var p = Titulo("ZLis4");
        for (var i = 1; i <= 5; i++)
        {
            _ = await SemearAsync(api, $"{p} item {i}", "EM_ANDAMENTO");
        }

        var segunda = await CorpoAsync(await ListarAsync(api, $"busca={p.ToLowerInvariant()}&tamanhoPagina=2&pagina=2&ordenarPor=TITULO&direcao=ASC"));
        var alem = await CorpoAsync(await ListarAsync(api, $"busca={p}&tamanhoPagina=2&pagina=9"));

        Assert.Equal((5, 3, 2, 2), (segunda.GetProperty("total").GetInt32(), segunda.GetProperty("totalPaginas").GetInt32(), segunda.GetProperty("pagina").GetInt32(), segunda.GetProperty("tamanhoPagina").GetInt32()));
        Assert.Equal([$"{p} item 3", $"{p} item 4"], segunda.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()!));
        Assert.Equal((0, 5), (alem.GetProperty("itens").GetArrayLength(), alem.GetProperty("total").GetInt32()));
    }

    [Fact]
    public async Task Parametro_invalido_devolve_400_com_todos_os_campos_juntos()
    {
        var api = await ApiAsync();

        var resposta = await ListarAsync(api, "situacao=XPTO&ordenarPor=NOME&direcao=PARA_O_LADO&pagina=0");
        var erros = (await CorpoAsync(resposta)).GetProperty("errors");

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarTreinamentos");
        Assert.Equal(["direcao", "ordenarPor", "pagina", "situacao"], erros.EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Valor inválido. Use um destes: TITULO, COMECA_EM.", erros.GetProperty("ordenarPor")[0].GetString());
    }

    [Fact]
    public async Task Os_indicadores_contam_em_andamento_obrigatorios_publicados_certificados_do_ano_e_pessoas_ativas_pendentes()
    {
        var api = await ApiAsync();
        var antes = await IndicadoresAsync(api);
        var p = Titulo("ZInd");

        // em andamento (+1 só o em andamento); obrigatórios publicados em qualquer situação (+3: agendado, em andamento, encerrado); o rascunho e o opcional não contam
        var obrigatorio = await SemearAsync(api, $"{p} obrigatorio", "EM_ANDAMENTO", obrigatorio: true, emite: true);
        var agendado = await SemearAsync(api, $"{p} agendado", "AGENDADO", obrigatorio: true);
        var encerrado = await SemearAsync(api, $"{p} encerrado", "ENCERRADO", obrigatorio: true, emite: true);
        _ = await SemearAsync(api, $"{p} rascunho obrigatorio", obrigatorio: true);
        _ = await SemearAsync(api, $"{p} opcional", "EM_ANDAMENTO", obrigatorio: false);
        var semEmitir = await SemearAsync(api, $"{p} sem emitir", "EM_ANDAMENTO", obrigatorio: false, emite: false);

        // pendência: a pessoa X em dois obrigatórios em andamento conta UMA vez; inativa, concluída, e pendente só em agendado, encerrado ou opcional não contam
        var obrigatorio2 = await SemearAsync(api, $"{p} obrigatorio 2", "EM_ANDAMENTO", obrigatorio: true);
        var x = await PessoaAsync(api, "ZInd X");
        await LinhaAsync(api, obrigatorio, x, "INSCRITO");
        await LinhaAsync(api, obrigatorio2, x, "EM_ANDAMENTO");
        var inativa = await PessoaAsync(api, "ZInd inativa");
        await LinhaAsync(api, obrigatorio, inativa, "INSCRITO");
        await AdvertenciasDeTeste.InativarAsync(api, inativa);
        var concluida = await PessoaAsync(api, "ZInd concluida");
        await LinhaAsync(api, obrigatorio, concluida, "CONCLUIDO");
        var emOutros = await PessoaAsync(api, "ZInd em outros");
        await LinhaAsync(api, agendado, emOutros, "INSCRITO");
        await LinhaAsync(api, encerrado, emOutros, "INSCRITO");

        // certificados do ano: os três CONCLUIDO acima criaram certificado pelo trigger com a data REAL do banco; fixo as datas para o teste não depender do dia
        var certificadoDoAno = await PessoaAsync(api, "ZInd cert");
        await LinhaAsync(api, encerrado, certificadoDoAno, "CONCLUIDO");
        var certificadoAnoPassado = await PessoaAsync(api, "ZInd cert antigo");
        await LinhaAsync(api, encerrado, certificadoAnoPassado, "CONCLUIDO");
        var certificadoProximoAno = await PessoaAsync(api, "ZInd cert proximo ano");
        await LinhaAsync(api, encerrado, certificadoProximoAno, "CONCLUIDO");
        var certificadoSemEmitir = await PessoaAsync(api, "ZInd cert sem emitir");
        await LinhaAsync(api, semEmitir, certificadoSemEmitir, "CONCLUIDO");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET DATA_EMISSAO = '2026-03-01' WHERE ID_TREINAMENTO IN ({obrigatorio}, {encerrado}, {semEmitir})");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET DATA_EMISSAO = '2025-12-31' WHERE ID_TREINAMENTO = {encerrado} AND ID_FUNCIONARIO = {certificadoAnoPassado}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET DATA_EMISSAO = '2027-01-01' WHERE ID_TREINAMENTO = {encerrado} AND ID_FUNCIONARIO = {certificadoProximoAno}"); // do ano seguinte também não conta

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/resumo");
        var depois = await CorpoAsync(resposta);

        await resposta.AfirmarConformeAoContratoAsync("obterIndicadoresDeTreinamentos");
        int Delta(string campo) => depois.GetProperty(campo).GetInt32() - antes.GetProperty(campo).GetInt32();
        Assert.Equal(2026, depois.GetProperty("ano").GetInt32());
        Assert.Equal(4, Delta("emAndamento")); // obrigatorio, obrigatorio 2, opcional e sem emitir
        Assert.Equal(4, Delta("obrigatorios")); // obrigatorio, agendado, encerrado e obrigatorio 2
        Assert.Equal(2, Delta("certificadosEmitidosNoAno")); // concluida (obrigatorio) e certificadoDoAno (encerrado), os dois de março; fora: 2025, 2027 e o de quem não emite
        Assert.Equal(1, Delta("pessoasComObrigatorioPendente")); // só X, uma vez
    }

    [Fact]
    public async Task Sem_login_401_e_gestor_ou_funcionario_403_nas_duas_rotas()
    {
        var api = await ApiAsync();

        var respostas = new[]
        {
            await api.Api.ClienteAnonimo().GetAsync(Rota), await api.Api.ClienteAnonimo().GetAsync($"{Rota}/resumo"),
            await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota), await api.Api.ClienteComo(api.Elenco.MembroA).GetAsync($"{Rota}/resumo"),
        };

        Assert.Equal([401, 401, 403, 403], respostas.Select(r => (int)r.StatusCode));
    }
}
