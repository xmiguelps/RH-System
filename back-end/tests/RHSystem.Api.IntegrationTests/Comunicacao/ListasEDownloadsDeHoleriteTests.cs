using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// As listas e os downloads de holerite, no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 no horário local). O RH lista os holerites de uma pessoa (a versão atual de cada competência e tipo, ou todas as versões; com os agendados) e baixa
/// qualquer versão; a pessoa lista só os PUBLICADOS e só a maior versão publicada de cada competência e tipo, e baixa só esses. Cada download grava o evento <c>Baixou</c> ANTES de existir o link. Cada teste cria a própria pessoa (admitida em 2026-01-02), então
/// as listas e as contagens não dependem dos outros testes.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ListasEDownloadsDeHoleriteTests(MySqlFixture mysql)
{
    private const string RotaDoRh = "/api/rh/funcionarios";
    private const string RotaDaPessoa = "/api/eu/holerites";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ListasEDownloadsDeHoleriteTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static Task<PessoaDoTeste> NovaAsync(ApiDeAcesso api, bool ativa = true) => api.NovaPessoaAsync("holerite", ativa, login: TipoDeLogin.SenhaDefinida);

    /// <summary>Um holerite direto no banco (a data de publicação é LOCAL, como o banco guarda). Devolve o id.</summary>
    private static async Task<int> SemearAsync(ApiDeAcesso api, int funcionarioId, int ano, int mes, string tipo, int versao, string publicacaoLocal)
    {
        var caminho = CaminhosDeArquivo.Novo("holerites", ".pdf");
        await Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO HOLERITES (ID_FUNCIONARIO, MES, ANO, DATA_PUBLICACAO, CAMINHO_ARQUIVO, TIPO_HOLERITE, VERSAO) VALUES ({funcionarioId}, {mes}, {ano}, '{publicacaoLocal}', '{caminho}', '{tipo}', {versao})");
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_HOLERITE FROM HOLERITES WHERE CAMINHO_ARQUIVO = '{caminho}'");
    }

    private static async Task<string> CaminhoAsync(ApiDeAcesso api, int id) => (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM HOLERITES WHERE ID_HOLERITE = {id}"))!;

    /// <summary>
    /// O cenário das listas do RH (a pessoa admitida em 2026-01-02): agosto e setembro de 2026, com a ordem de inserção igual à ordem dos ids. <c>M1</c> foi substituído por <c>M2</c> (setembro mensal); <c>A2</c> (agosto mensal, versão 2) está AGENDADO para 20/10.
    /// </summary>
    private sealed record Cenario(int A1, int M1, int F1, int M2, int A2);

    private static async Task<Cenario> SemearCenarioAsync(ApiDeAcesso api, int pessoa) =>
        new(
            await SemearAsync(api, pessoa, 2026, 8, "MENSAL", 1, "2026-09-04 10:00:00"),
            await SemearAsync(api, pessoa, 2026, 9, "MENSAL", 1, "2026-10-05 11:30:00"),
            await SemearAsync(api, pessoa, 2026, 9, "FERIAS", 1, "2026-10-05 12:00:00"),
            await SemearAsync(api, pessoa, 2026, 9, "MENSAL", 2, "2026-10-06 08:00:00"),
            await SemearAsync(api, pessoa, 2026, 8, "MENSAL", 2, "2026-10-20 00:00:00"));

    private static async Task<HttpResponseMessage> ComoRhAsync(ApiDeAcesso api, string rota) => await (await api.ClienteDoRhAgoraAsync()).GetAsync(rota);

    private static async Task<HttpResponseMessage> ComoAsync(ApiDeAcesso api, PessoaDoTeste pessoa, string rota) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId!.Value)).GetAsync(rota);

    private static async Task<HttpResponseMessage> ComoAsync(ApiDeAcesso api, PessoaDeTeste pessoa, string rota) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(rota);

    private static async Task<JsonElement> ListaDoRhAsync(ApiDeAcesso api, int pessoa, string consulta = "")
    {
        var resposta = await ComoRhAsync(api, $"{RotaDoRh}/{pessoa}/holerites{(consulta.Length == 0 ? string.Empty : "?" + consulta)}");
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    private static async Task<JsonElement> ListaDaPessoaAsync(ApiDeAcesso api, PessoaDoTeste pessoa, string consulta = "")
    {
        var resposta = await ComoAsync(api, pessoa, consulta.Length == 0 ? RotaDaPessoa : $"{RotaDaPessoa}?{consulta}");
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    private static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(h => h.GetProperty("id").GetInt32())];

    private static string Item(JsonElement h) =>
        $"{h.GetProperty("tipo").GetString()} {h.GetProperty("mes").GetInt32()}/{h.GetProperty("ano").GetInt32()} v{h.GetProperty("versao").GetInt32()} atual={h.GetProperty("versaoAtual").GetBoolean()} {h.GetProperty("situacao").GetString()} {h.GetProperty("publicacaoEmUtc").GetString()}";

    private static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int holeriteId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "HOLERITES", holeriteId);

    [Fact]
    public async Task A_versao_atual_e_de_cada_tipo_e_de_cada_ano_e_o_empate_de_publicacao_vai_para_o_mais_recentemente_cadastrado()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var mensal = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-05 11:30:00");
        var feriasV2 = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "FERIAS", 2, "2026-10-05 11:30:00"); // o mesmo instante e o mesmo mês: o id maior vem primeiro
        var feriasDeOutroAno = await SemearAsync(api, pessoa.FuncionarioId, 2027, 9, "FERIAS", 3, "2026-10-06 11:30:00"); // versão 3 do mesmo mês e tipo, em OUTRO ano: não esconde a versão 2 de 2026
        var feriasDe2026 = await SemearAsync(api, pessoa.FuncionarioId, 2026, 8, "FERIAS", 1, "2026-10-06 11:30:00");

        var de2026 = await ListaDaPessoaAsync(api, pessoa, "ano=2026");
        var de2027 = await ListaDaPessoaAsync(api, pessoa, "ano=2027");

        // o mensal v1 continua visível ao lado das férias v2 (versão atual DE CADA TIPO); as férias de 2026 e as de 2027 não se escondem (versão atual DE CADA ANO)
        Assert.Equal([feriasV2, mensal, feriasDe2026], Ids(de2026));
        Assert.Equal([feriasDeOutroAno], Ids(de2027));
    }

    [Fact]
    public async Task O_ultimo_publicado_inclui_o_holerite_publicado_no_instante_exato_do_relogio()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var id = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-07 09:42:00"); // 12:42Z, o instante do relógio

        var lista = await ListaDoRhAsync(api, pessoa.FuncionarioId);

        Assert.Equal(id, lista.GetProperty("ultimoPublicado").GetProperty("id").GetInt32());
    }

    // ---------------------------------------------------------------- a lista do RH

    [Fact]
    public async Task A_lista_do_RH_mostra_a_versao_atual_de_cada_competencia_com_os_agendados_e_com_todasAsVersoes_o_historico()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var c = await SemearCenarioAsync(api, pessoa.FuncionarioId);

        var atuais = await ListaDoRhAsync(api, pessoa.FuncionarioId);
        var todas = await ListaDoRhAsync(api, pessoa.FuncionarioId, "todasAsVersoes=true");
        var explicitamenteSo = await ListaDoRhAsync(api, pessoa.FuncionarioId, "todasAsVersoes=false");

        // ano desc, mês desc, versão desc, id desc: em setembro, a versão 2 antes das de versão 1 (a mais nova cadastrada primeiro)
        Assert.Equal([c.M2, c.F1, c.A2], Ids(atuais));
        Assert.Equal([c.M2, c.F1, c.A2], Ids(explicitamenteSo));
        Assert.Equal([c.M2, c.F1, c.M1, c.A2, c.A1], Ids(todas));
        Assert.Equal(
            [
                "MENSAL 9/2026 v2 atual=True PUBLICADO 2026-10-06T11:00:00Z",
                "FERIAS 9/2026 v1 atual=True PUBLICADO 2026-10-05T15:00:00Z",
                "MENSAL 8/2026 v2 atual=True AGENDADO 2026-10-20T03:00:00Z",
            ],
            [.. atuais.GetProperty("itens").EnumerateArray().Select(Item)]);
        Assert.Equal(
            [
                "MENSAL 9/2026 v2 atual=True PUBLICADO 2026-10-06T11:00:00Z",
                "FERIAS 9/2026 v1 atual=True PUBLICADO 2026-10-05T15:00:00Z",
                "MENSAL 9/2026 v1 atual=False PUBLICADO 2026-10-05T14:30:00Z",
                "MENSAL 8/2026 v2 atual=True AGENDADO 2026-10-20T03:00:00Z",
                "MENSAL 8/2026 v1 atual=False PUBLICADO 2026-09-04T13:00:00Z",
            ],
            [.. todas.GetProperty("itens").EnumerateArray().Select(Item)]);
        Assert.Equal((1, 20, 3, 1), (atuais.GetProperty("pagina").GetInt32(), atuais.GetProperty("tamanhoPagina").GetInt32(), atuais.GetProperty("total").GetInt32(), atuais.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal(5, todas.GetProperty("total").GetInt32());
        Assert.All(atuais.GetProperty("itens").EnumerateArray(), h => Assert.Equal(pessoa.FuncionarioId, h.GetProperty("funcionarioId").GetInt32()));
    }

    [Fact]
    public async Task A_lista_do_RH_nunca_leva_o_caminho_do_arquivo()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        _ = await SemearCenarioAsync(api, pessoa.FuncionarioId);

        var resposta = await ComoRhAsync(api, $"{RotaDoRh}/{pessoa.FuncionarioId}/holerites?todasAsVersoes=true");
        var texto = await resposta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("holerites/", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(".pdf", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("caminho", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["id", "funcionarioId", "tipo", "mes", "ano", "versao", "versaoAtual", "situacao", "publicacaoEmUtc"], [.. (await CorpoAsync(resposta)).GetProperty("itens")[0].EnumerateObject().Select(p => p.Name)]);
    }

    [Fact]
    public async Task O_ultimo_publicado_e_o_ja_publicado_mais_recentemente_e_ignora_os_filtros_e_a_pagina()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var c = await SemearCenarioAsync(api, pessoa.FuncionarioId);

        var semFiltro = await ListaDoRhAsync(api, pessoa.FuncionarioId);
        var filtrada = await ListaDoRhAsync(api, pessoa.FuncionarioId, "tipo=FERIAS&pagina=2&tamanhoPagina=1");
        var outroAno = await ListaDoRhAsync(api, pessoa.FuncionarioId, "ano=2027");

        // o agendado (A2, 20/10) não conta; entre os publicados, o de 06/10 (M2) é o mais recente
        foreach (var lista in new[] { semFiltro, filtrada, outroAno })
        {
            var ultimo = lista.GetProperty("ultimoPublicado");
            Assert.Equal(c.M2, ultimo.GetProperty("id").GetInt32());
            Assert.Equal("MENSAL 9/2026 v2 atual=True PUBLICADO 2026-10-06T11:00:00Z", Item(ultimo));
        }

        Assert.Empty(Ids(filtrada)); // a página 2 de uma lista de um item
        Assert.Equal((1, 1), (filtrada.GetProperty("total").GetInt32(), filtrada.GetProperty("totalPaginas").GetInt32()));
        Assert.Empty(Ids(outroAno));
    }

    [Fact]
    public async Task Sem_nenhum_publicado_o_ultimo_publicado_e_null_e_quem_nunca_recebeu_tem_a_lista_vazia()
    {
        var api = await ApiAsync();
        var soAgendado = await NovaAsync(api);
        await SemearAsync(api, soAgendado.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-20 00:00:00");
        var semNada = await NovaAsync(api);

        var agendado = await ListaDoRhAsync(api, soAgendado.FuncionarioId);
        var vazia = await ListaDoRhAsync(api, semNada.FuncionarioId);

        Assert.Equal(JsonValueKind.Null, agendado.GetProperty("ultimoPublicado").ValueKind);
        Assert.Single(Ids(agendado));
        Assert.Equal(JsonValueKind.Null, vazia.GetProperty("ultimoPublicado").ValueKind);
        Assert.Equal((0, 0), (vazia.GetProperty("total").GetInt32(), vazia.GetProperty("totalPaginas").GetInt32()));
        Assert.Empty(Ids(vazia));
    }

    [Fact]
    public async Task Os_filtros_ano_e_tipo_da_lista_do_RH_valem_para_a_competencia_e_nao_mudam_qual_versao_e_a_atual()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var c = await SemearCenarioAsync(api, pessoa.FuncionarioId);
        var ferias2027 = await SemearAsync(api, pessoa.FuncionarioId, 2027, 1, "FERIAS", 1, "2026-10-01 09:00:00");

        Assert.Equal([ferias2027], Ids(await ListaDoRhAsync(api, pessoa.FuncionarioId, "ano=2027")));
        Assert.Equal([c.M2, c.F1, c.A2], Ids(await ListaDoRhAsync(api, pessoa.FuncionarioId, "ano=2026")));
        Assert.Equal([c.M2, c.A2], Ids(await ListaDoRhAsync(api, pessoa.FuncionarioId, "ano=2026&tipo=MENSAL")));
        Assert.Equal([ferias2027, c.F1], Ids(await ListaDoRhAsync(api, pessoa.FuncionarioId, "tipo=FERIAS")));
        Assert.Empty(Ids(await ListaDoRhAsync(api, pessoa.FuncionarioId, "tipo=DECIMO_TERCEIRO")));
        Assert.Equal([c.M2, c.M1, c.A2, c.A1], Ids(await ListaDoRhAsync(api, pessoa.FuncionarioId, "tipo=MENSAL&todasAsVersoes=true")));
    }

    [Fact]
    public async Task A_lista_do_RH_pagina_pela_ordem_do_contrato()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var c = await SemearCenarioAsync(api, pessoa.FuncionarioId);

        var primeira = await ListaDoRhAsync(api, pessoa.FuncionarioId, "todasAsVersoes=true&pagina=1&tamanhoPagina=2");
        var ultima = await ListaDoRhAsync(api, pessoa.FuncionarioId, "todasAsVersoes=true&pagina=3&tamanhoPagina=2");

        Assert.Equal([c.M2, c.F1], Ids(primeira));
        Assert.Equal([c.A1], Ids(ultima));
        Assert.Equal((5, 3), (primeira.GetProperty("total").GetInt32(), primeira.GetProperty("totalPaginas").GetInt32()));
    }

    [Theory]
    [InlineData("ano=1999", "ano")]
    [InlineData("ano=2101", "ano")]
    [InlineData("tipo=ABC", "tipo")]
    [InlineData("tipo=mensal", "tipo")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("tamanhoPagina=101", "tamanhoPagina")]
    public async Task Parametro_invalido_da_lista_do_RH_e_400_com_o_erro_no_campo(string consulta, string campo)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);

        var resposta = await ComoRhAsync(api, $"{RotaDoRh}/{pessoa.FuncionarioId}/holerites?{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2100)]
    public async Task Os_extremos_do_ano_do_contrato_valem_na_lista_do_RH(int ano)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);

        var resposta = await ComoRhAsync(api, $"{RotaDoRh}/{pessoa.FuncionarioId}/holerites?ano={ano}");

        Assert.Equal(200, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task A_lista_do_RH_funciona_para_pessoa_inativa_e_a_pessoa_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        var inativa = await NovaAsync(api, ativa: false);
        var id = await SemearAsync(api, inativa.FuncionarioId, 2026, 8, "MENSAL", 1, "2026-09-04 10:00:00");

        var daInativa = await ListaDoRhAsync(api, inativa.FuncionarioId);
        var inexistente = await ComoRhAsync(api, $"{RotaDoRh}/{int.MaxValue}/holerites");

        Assert.Equal([id], Ids(daInativa));
        Assert.Equal(404, (int)inexistente.StatusCode);
    }

    [Fact]
    public async Task As_rotas_do_RH_de_holerite_so_atendem_o_RH()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var id = await SemearAsync(api, pessoa.FuncionarioId, 2026, 8, "MENSAL", 1, "2026-09-04 10:00:00");

        foreach (var rota in new[] { $"{RotaDoRh}/{pessoa.FuncionarioId}/holerites", $"/api/rh/holerites/{id}/download" })
        {
            Assert.Equal(403, (int)(await ComoAsync(api, api.Elenco.MembroA, rota)).StatusCode);
            Assert.Equal(403, (int)(await ComoAsync(api, api.Elenco.Gestor, rota)).StatusCode);
            Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(rota)).StatusCode);
        }
    }

    // ---------------------------------------------------------------- a lista da própria pessoa

    [Fact]
    public async Task A_pessoa_so_ve_os_publicados_e_so_a_maior_versao_publicada_e_nunca_o_de_outra_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var outra = api.Elenco.MembroA;
        var setembroV1 = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-05 11:30:00");
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 2, "2026-10-20 00:00:00"); // agendada: a v1 continua visível
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 8, "MENSAL", 1, "2026-09-04 10:00:00"); // substituída
        var agostoV2 = await SemearAsync(api, pessoa.FuncionarioId, 2026, 8, "MENSAL", 2, "2026-10-06 08:00:00");
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 7, "MENSAL", 1, "2026-10-20 00:00:00"); // só agendado: nada a mostrar
        var setembroFerias = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "FERIAS", 1, "2026-10-06 09:00:00");
        await SemearAsync(api, outra.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-05 11:30:00");

        var lista = await ListaDaPessoaAsync(api, pessoa);

        // ano, mês desc, e no mesmo mês a publicação mais recente primeiro (as férias, de 06/10, antes do mensal, de 05/10)
        Assert.Equal([setembroFerias, setembroV1, agostoV2], Ids(lista));
        Assert.Equal(
            ["FERIAS 9/2026 2026-10-06T12:00:00Z", "MENSAL 9/2026 2026-10-05T14:30:00Z", "MENSAL 8/2026 2026-10-06T11:00:00Z"],
            [.. lista.GetProperty("itens").EnumerateArray().Select(h => $"{h.GetProperty("tipo").GetString()} {h.GetProperty("mes").GetInt32()}/{h.GetProperty("ano").GetInt32()} {h.GetProperty("publicadoEmUtc").GetString()}")]);
        Assert.Equal(["id", "tipo", "mes", "ano", "publicadoEmUtc"], [.. lista.GetProperty("itens")[0].EnumerateObject().Select(p => p.Name)]);
        Assert.DoesNotContain("holerites/", lista.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(3, lista.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task As_contagens_e_os_anos_do_seletor_valem_para_o_ano_pedido_e_nao_dependem_do_tipo()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var p = pessoa.FuncionarioId;
        await SemearAsync(api, p, 2026, 7, "MENSAL", 1, "2026-08-05 10:00:00");
        await SemearAsync(api, p, 2026, 8, "MENSAL", 1, "2026-09-04 10:00:00");
        await SemearAsync(api, p, 2026, 8, "MENSAL", 2, "2026-09-05 10:00:00"); // a v1 e a v2 contam como UM holerite
        var ferias2026 = await SemearAsync(api, p, 2026, 9, "FERIAS", 1, "2026-10-01 10:00:00");
        await SemearAsync(api, p, 2026, 6, "DECIMO_TERCEIRO", 1, "2026-07-05 10:00:00");
        await SemearAsync(api, p, 2026, 9, "MENSAL", 1, "2026-10-20 00:00:00"); // agendado: fora das contagens
        var ferias2027 = await SemearAsync(api, p, 2027, 1, "FERIAS", 1, "2026-10-02 10:00:00");
        await SemearAsync(api, p, 2028, 2, "FERIAS", 1, "2026-10-03 10:00:00");
        await SemearAsync(api, p, 2029, 3, "FERIAS", 1, "2026-10-20 00:00:00"); // só agendado: o ano não aparece

        var soFerias = await ListaDaPessoaAsync(api, pessoa, "tipo=FERIAS");
        var de2027 = await ListaDaPessoaAsync(api, pessoa, "ano=2027");
        var de2029 = await ListaDaPessoaAsync(api, pessoa, "ano=2029");

        Assert.Equal([ferias2026], Ids(soFerias));
        Assert.Equal(2026, soFerias.GetProperty("ano").GetInt32());
        Assert.Equal([2028, 2027, 2026], [.. soFerias.GetProperty("anosDisponiveis").EnumerateArray().Select(a => a.GetInt32())]);
        Assert.Equal("todos=4 mensais=2 ferias=1 decimoTerceiro=1", Contagens(soFerias));
        Assert.Equal([ferias2027], Ids(de2027));
        Assert.Equal((2027, "todos=1 mensais=0 ferias=1 decimoTerceiro=0"), (de2027.GetProperty("ano").GetInt32(), Contagens(de2027)));
        Assert.Empty(Ids(de2029));
        Assert.Equal((2029, "todos=0 mensais=0 ferias=0 decimoTerceiro=0"), (de2029.GetProperty("ano").GetInt32(), Contagens(de2029)));
        Assert.Equal([2028, 2027, 2026], [.. de2029.GetProperty("anosDisponiveis").EnumerateArray().Select(a => a.GetInt32())]);
    }

    private static string Contagens(JsonElement lista)
    {
        var c = lista.GetProperty("contagens");
        return $"todos={c.GetProperty("todos").GetInt32()} mensais={c.GetProperty("mensais").GetInt32()} ferias={c.GetProperty("ferias").GetInt32()} decimoTerceiro={c.GetProperty("decimoTerceiro").GetInt32()}";
    }

    [Fact]
    public async Task Sem_ano_vale_o_ano_atual_no_horario_do_Brasil()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);

        api.Api.Relogio.Fixar(new DateTime(2027, 1, 1, 2, 59, 0, DateTimeKind.Utc)); // 23:59 de 31/12/2026 no Brasil
        var viradaDoAnoAindaNoBrasil = (await ListaDaPessoaAsync(api, pessoa)).GetProperty("ano").GetInt32();
        api.Api.Relogio.Fixar(new DateTime(2027, 1, 1, 3, 0, 0, DateTimeKind.Utc)); // 00:00 de 01/01/2027
        var virou = (await ListaDaPessoaAsync(api, pessoa)).GetProperty("ano").GetInt32();

        Assert.Equal((2026, 2027), (viradaDoAnoAindaNoBrasil, virou));
    }

    [Fact]
    public async Task Quem_nao_tem_holerite_publicado_recebe_a_lista_vazia_com_as_contagens_zeradas()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-20 00:00:00");

        var lista = await ListaDaPessoaAsync(api, pessoa);

        Assert.Empty(Ids(lista));
        Assert.Empty(lista.GetProperty("anosDisponiveis").EnumerateArray());
        Assert.Equal((2026, 0, 0, "todos=0 mensais=0 ferias=0 decimoTerceiro=0"), (lista.GetProperty("ano").GetInt32(), lista.GetProperty("total").GetInt32(), lista.GetProperty("totalPaginas").GetInt32(), Contagens(lista)));
    }

    [Fact]
    public async Task A_lista_da_pessoa_pagina_e_as_contagens_continuam_as_do_ano_inteiro()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var janeiro = await SemearAsync(api, pessoa.FuncionarioId, 2026, 1, "MENSAL", 1, "2026-02-05 10:00:00");
        var fevereiro = await SemearAsync(api, pessoa.FuncionarioId, 2026, 2, "MENSAL", 1, "2026-03-05 10:00:00");
        var marco = await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "MENSAL", 1, "2026-04-05 10:00:00");

        var primeira = await ListaDaPessoaAsync(api, pessoa, "tamanhoPagina=2");
        var segunda = await ListaDaPessoaAsync(api, pessoa, "tamanhoPagina=2&pagina=2");

        Assert.Equal([marco, fevereiro], Ids(primeira));
        Assert.Equal([janeiro], Ids(segunda));
        Assert.Equal((3, 2, "todos=3 mensais=3 ferias=0 decimoTerceiro=0"), (primeira.GetProperty("total").GetInt32(), primeira.GetProperty("totalPaginas").GetInt32(), Contagens(segunda)));
    }

    [Theory]
    [InlineData("ano=1999", "ano")]
    [InlineData("ano=2101", "ano")]
    [InlineData("tipo=ABC", "tipo")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("tamanhoPagina=0", "tamanhoPagina")]
    public async Task Parametro_invalido_da_lista_da_pessoa_e_400_com_o_erro_no_campo(string consulta, string campo)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);

        var resposta = await ComoAsync(api, pessoa, $"{RotaDaPessoa}?{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Qualquer_perfil_logado_lista_os_proprios_holerites_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var gestor = api.Elenco.Gestor;
        await SemearAsync(api, gestor.FuncionarioId, 2027, 4, "FERIAS", 1, "2026-10-01 10:00:00");
        await SemearAsync(api, api.Elenco.Rh.FuncionarioId, 2027, 5, "FERIAS", 1, "2026-10-01 10:00:00");
        await SemearAsync(api, api.Elenco.MembroA.FuncionarioId, 2027, 6, "FERIAS", 1, "2026-10-01 10:00:00");

        foreach (var (quem, mes) in new[] { (gestor, 4), (api.Elenco.Rh, 5), (api.Elenco.MembroA, 6) })
        {
            var lista = await CorpoAsync(await ComoAsync(api, quem, $"{RotaDaPessoa}?ano=2027"));
            Assert.Equal([mes], [.. lista.GetProperty("itens").EnumerateArray().Select(h => h.GetProperty("mes").GetInt32())]);
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(RotaDaPessoa)).StatusCode);
    }

    // ---------------------------------------------------------------- o download do RH

    [Fact]
    public async Task O_RH_baixa_qualquer_versao_publicada_ou_agendada_e_cada_link_deixa_o_evento_Baixou_com_o_nome_da_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var c = await SemearCenarioAsync(api, pessoa.FuncionarioId);

        foreach (var (id, nome) in new[] { (c.M1, "holerite-2026-09-mensal-v1.pdf"), (c.M2, "holerite-2026-09-mensal-v2.pdf"), (c.A2, "holerite-2026-08-mensal-v2.pdf"), (c.F1, "holerite-2026-09-ferias-v1.pdf") })
        {
            var resposta = await ComoRhAsync(api, $"/api/rh/holerites/{id}/download");

            Assert.Equal(200, (int)resposta.StatusCode);
            var link = await CorpoAsync(resposta);
            Assert.Equal(($"https://arquivos.exemplo.test/{await CaminhoAsync(api, id)}?nome={nome}", "2026-10-07T12:47:00Z"), (link.GetProperty("url").GetString(), link.GetProperty("expiraEmUtc").GetString()));
        }

        var primeiro = await ComoRhAsync(api, $"/api/rh/holerites/{c.M1}/download");
        Assert.Equal(200, (int)primeiro.StatusCode);
        var eventos = await EventosAsync(api, c.M1);
        Assert.Equal(2, eventos.Count); // um por link gerado
        var evento = eventos[0];
        Assert.Equal(
            (AuditoriaArea.Holerites, AuditoriaAcao.Baixou, $"Baixou o holerite mensal de setembro de 2026 (versão 1) de {pessoa.Nome}.", $"Holerite de {pessoa.Nome}", api.Elenco.Rh.FuncionarioId),
            (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
        Assert.DoesNotContain(".pdf", evento.Descricao, StringComparison.Ordinal);
        Assert.Equal($"Baixou o holerite de férias de setembro de 2026 (versão 1) de {pessoa.Nome}.", (await EventosAsync(api, c.F1)).Single().Descricao);
    }

    [Fact]
    public async Task Se_o_registro_do_download_do_RH_falha_nao_ha_link_nem_evento()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var id = await SemearAsync(api, pessoa.FuncionarioId, 2026, 8, "MENSAL", 1, "2026-09-04 10:00:00");
        var cliente = AtestadosDeTeste.ComAuditorQueFalhaNoDownload(api.Api).CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;

        var resposta = await cliente.GetAsync($"/api/rh/holerites/{id}/download");

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain("arquivos.exemplo.test", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await EventosAsync(api, id));
    }

    [Fact]
    public async Task Holerite_que_nao_existe_e_404_para_o_RH()
    {
        var api = await ApiAsync();

        var resposta = await ComoRhAsync(api, $"/api/rh/holerites/{int.MaxValue}/download");

        Assert.Equal(404, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task O_RH_baixa_o_holerite_de_pessoa_inativa()
    {
        var api = await ApiAsync();
        var inativa = await NovaAsync(api, ativa: false);
        var id = await SemearAsync(api, inativa.FuncionarioId, 2026, 8, "MENSAL", 1, "2026-09-04 10:00:00");

        var resposta = await ComoRhAsync(api, $"/api/rh/holerites/{id}/download");

        Assert.Equal(200, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task Holerite_sem_arquivo_guardado_nao_baixa_nem_para_o_RH_nem_para_a_pessoa_mas_continua_na_lista()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO HOLERITES (ID_FUNCIONARIO, MES, ANO, DATA_PUBLICACAO, TIPO_HOLERITE, VERSAO) VALUES ({pessoa.FuncionarioId}, 8, 2026, '2026-09-04 10:00:00', 'MENSAL', 1)");
        var id = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_HOLERITE FROM HOLERITES WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");

        var doRh = await ComoRhAsync(api, $"/api/rh/holerites/{id}/download");
        var daPessoa = await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{id}/download");

        Assert.Equal((404, 404), ((int)doRh.StatusCode, (int)daPessoa.StatusCode));
        Assert.Empty(await EventosAsync(api, id));
        Assert.Equal([id], Ids(await ListaDoRhAsync(api, pessoa.FuncionarioId)));
    }

    // ---------------------------------------------------------------- o download da própria pessoa

    [Fact]
    public async Task A_pessoa_baixa_o_proprio_holerite_publicado_na_versao_atual_e_o_evento_Baixou_vem_com_ela_como_autora()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var id = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 2, "2026-10-06 08:00:00");
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-05 08:00:00");

        var resposta = await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{id}/download");

        Assert.Equal(200, (int)resposta.StatusCode);
        var link = await CorpoAsync(resposta);
        Assert.Equal(($"https://arquivos.exemplo.test/{await CaminhoAsync(api, id)}?nome=holerite-2026-09-mensal.pdf", "2026-10-07T12:47:00Z"), (link.GetProperty("url").GetString(), link.GetProperty("expiraEmUtc").GetString()));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal(
            (AuditoriaArea.Holerites, AuditoriaAcao.Baixou, $"Baixou o holerite mensal de setembro de 2026 (versão 2) de {pessoa.Nome}.", $"Holerite de {pessoa.Nome}", pessoa.FuncionarioId),
            (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
    }

    [Fact]
    public async Task Com_a_versao_2_agendada_a_versao_1_continua_visivel_e_baixavel_e_com_a_2_publicada_a_1_some()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var v1 = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-05 08:00:00");
        var v2 = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 2, "2026-10-20 00:00:00");

        var v1Antes = await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{v1}/download");
        var v2Agendada = await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{v2}/download");
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc)); // 00:00 do dia agendado
        var v1Depois = await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{v1}/download");
        var v2Publicada = await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{v2}/download");
        var lista = await ListaDaPessoaAsync(api, pessoa);

        Assert.Equal((200, 404, 404, 200), ((int)v1Antes.StatusCode, (int)v2Agendada.StatusCode, (int)v1Depois.StatusCode, (int)v2Publicada.StatusCode));
        Assert.Equal([v2], Ids(lista));
        Assert.Equal((1, 1), ((await EventosAsync(api, v1)).Count, (await EventosAsync(api, v2)).Count)); // só os downloads que deram link deixam evento
    }

    [Fact]
    public async Task Holerite_de_outra_pessoa_ou_agendado_ou_inexistente_e_404_inclusive_para_o_gestor_e_para_o_RH()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var doMembro = await SemearAsync(api, api.Elenco.MembroB.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-05 08:00:00");
        var agendado = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "FERIAS", 1, "2026-10-20 00:00:00");

        // a pessoa não vê o do colega nem o agendado dela; o gestor (da equipe do colega) e o RH também não pegam o do colega pela rota da pessoa
        Assert.Equal(404, (int)(await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{doMembro}/download")).StatusCode);
        Assert.Equal(404, (int)(await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{agendado}/download")).StatusCode);
        Assert.Equal(404, (int)(await ComoAsync(api, pessoa, $"{RotaDaPessoa}/{int.MaxValue}/download")).StatusCode);
        Assert.Equal(404, (int)(await ComoAsync(api, api.Elenco.Gestor, $"{RotaDaPessoa}/{doMembro}/download")).StatusCode);
        Assert.Equal(404, (int)(await ComoAsync(api, api.Elenco.Rh, $"{RotaDaPessoa}/{doMembro}/download")).StatusCode);
        Assert.Equal(404, (int)(await ComoAsync(api, api.Elenco.Rh, $"{RotaDaPessoa}/{agendado}/download")).StatusCode);
        Assert.Empty(await EventosAsync(api, doMembro));
        Assert.Empty(await EventosAsync(api, agendado));
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync($"{RotaDaPessoa}/{doMembro}/download")).StatusCode);
    }

    [Fact]
    public async Task Se_o_registro_do_download_da_pessoa_falha_nao_ha_link_nem_evento()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var id = await SemearAsync(api, pessoa.FuncionarioId, 2026, 9, "MENSAL", 1, "2026-10-05 08:00:00");
        var cliente = AtestadosDeTeste.ComAuditorQueFalhaNoDownload(api.Api).CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(pessoa.UsuarioId!.Value)).DefaultRequestHeaders.Authorization;

        var resposta = await cliente.GetAsync($"{RotaDaPessoa}/{id}/download");

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain("arquivos.exemplo.test", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await EventosAsync(api, id));
    }

    // ---------------------------------------------------------------- o contrato

    [Fact]
    public async Task As_quatro_rotas_respondem_como_o_contrato_manda()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var vazia = await NovaAsync(api);
        var c = await SemearCenarioAsync(api, pessoa.FuncionarioId);
        var rh = await api.ClienteDoRhAgoraAsync();
        var daPessoa = await api.ClienteAgoraAsync(pessoa.UsuarioId!.Value);

        await (await rh.GetAsync($"{RotaDoRh}/{pessoa.FuncionarioId}/holerites?todasAsVersoes=true")).AfirmarConformeAoContratoAsync("listarHoleritesDoFuncionario");
        await (await rh.GetAsync($"{RotaDoRh}/{vazia.FuncionarioId}/holerites")).AfirmarConformeAoContratoAsync("listarHoleritesDoFuncionario"); // ultimoPublicado null
        await (await rh.GetAsync($"{RotaDoRh}/{pessoa.FuncionarioId}/holerites?ano=1999")).AfirmarConformeAoContratoAsync("listarHoleritesDoFuncionario"); // 400
        await (await rh.GetAsync($"{RotaDoRh}/{int.MaxValue}/holerites")).AfirmarConformeAoContratoAsync("listarHoleritesDoFuncionario"); // 404
        await (await rh.GetAsync($"/api/rh/holerites/{c.M1}/download")).AfirmarConformeAoContratoAsync("obterLinkDeDownloadDoHolerite");
        await (await rh.GetAsync($"/api/rh/holerites/{int.MaxValue}/download")).AfirmarConformeAoContratoAsync("obterLinkDeDownloadDoHolerite"); // 404
        await (await daPessoa.GetAsync(RotaDaPessoa)).AfirmarConformeAoContratoAsync("listarMeusHolerites");
        await (await daPessoa.GetAsync($"{RotaDaPessoa}?ano=2101")).AfirmarConformeAoContratoAsync("listarMeusHolerites"); // 400
        await (await daPessoa.GetAsync($"{RotaDaPessoa}/{c.M2}/download")).AfirmarConformeAoContratoAsync("obterLinkDeDownloadDoMeuHolerite");
        await (await daPessoa.GetAsync($"{RotaDaPessoa}/{c.M1}/download")).AfirmarConformeAoContratoAsync("obterLinkDeDownloadDoMeuHolerite"); // 404 (substituído)
    }
}
