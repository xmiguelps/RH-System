using System.Net;
using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;
using static RHSystem.Api.IntegrationTests.Autorizacao.ApiDeAutorizacao;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// As três rotas do log de auditoria (Task 9): lista, detalhe e exportação. Uma API e um banco para a classe, com o relógio em
/// <c>2026-10-07T12:42:00Z</c> (dia 07/10 em Brasília). Os eventos entram direto no banco com o <c>Quando</c> que o teste quer; cada teste usa um dia
/// (ou um termo de busca) só dele, porque as outras rotas gravam eventos próprios (o 403, a exportação) e a contagem de um teste não pode depender de outro.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AuditoriaConsultaTests(MySqlFixture mysql)
{
    private const string Lista = "/api/rh/auditoria";
    private const string Exportacao = "/api/rh/auditoria/exportacao";
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static (ApiFactory Api, ElencoDeTeste Elenco)? _pronta;

    private async Task<(ApiFactory Api, ElencoDeTeste Elenco)> ApiAsync()
    {
        await Trava.WaitAsync();
        try
        {
            if (_pronta is null)
            {
                var api = await ApiFactory.CriarAsync(mysql);
                _pronta = (api, await api.SemearElencoAsync());
            }

            return _pronta.Value;
        }
        finally
        {
            Trava.Release();
        }
    }

    private static DateTime Utc(int ano, int mes, int dia, int hora = 12, int minuto = 0, int segundo = 0) => new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);

    /// <summary>Grava um evento com o <c>Quando</c> dado (sem interceptor). Sem <paramref name="ator"/> o evento é do Sistema.</summary>
    private static async Task<long> EventoAsync(
        string conexao,
        DateTime quandoUtc,
        string descricao,
        AuditoriaArea area = AuditoriaArea.Colaboradores,
        AuditoriaAcao acao = AuditoriaAcao.Alterou,
        PessoaDeTeste? ator = null,
        Perfil? perfilDoEvento = null,
        string registro = "Registro de teste",
        string? ip = "10.0.4.23",
        params (int Ordem, string Campo, string? Antes, string? Depois, bool Oculto)[] mudancas)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var evento = ator is null
            ? EventoDeAuditoria.Criar(quandoUtc, TipoDeAtor.Sistema, null, "Sistema", null, area, acao, descricao, registro, null, null, CanalDeOrigem.Sistema, null)
            : EventoDeAuditoria.Criar(quandoUtc, TipoDeAtor.Pessoa, ator.FuncionarioId, ator.Nome, perfilDoEvento ?? ator.Perfil, area, acao, descricao, registro, null, null, CanalDeOrigem.Portal, ip);
        foreach (var m in mudancas)
        {
            evento.AdicionarMudanca(m.Ordem, m.Campo, m.Antes, m.Depois, m.Oculto);
        }

        db.EventosDeAuditoria.Add(evento);
        await db.SaveChangesAsync();
        return evento.Id;
    }

    private static async Task<JsonElement> ObterJsonAsync(HttpClient cliente, string url)
    {
        var resposta = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await LerAsync(resposta);
    }

    private static List<string> Descricoes(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("descricao").GetString()!)];

    [Fact]
    public async Task Lista_devolve_do_mais_recente_ao_mais_antigo_com_pagina_tamanho_total_e_totalPaginas()
    {
        var (api, elenco) = await ApiAsync();
        await EventoAsync(api.Conexao, Utc(2026, 3, 10, 9), "ORDEM-1 a");
        await EventoAsync(api.Conexao, Utc(2026, 3, 10, 10), "ORDEM-1 b");
        await EventoAsync(api.Conexao, Utc(2026, 3, 10, 11), "ORDEM-1 c");
        await EventoAsync(api.Conexao, Utc(2026, 3, 10, 11), "ORDEM-1 d"); // mesmo instante de c: desempata pelo id, o mais novo primeiro
        await EventoAsync(api.Conexao, Utc(2026, 3, 10, 12), "ORDEM-1 e");
        var cliente = api.ClienteComo(elenco.Rh);
        const string filtro = "de=2026-03-10&ate=2026-03-10&busca=ORDEM-1&tamanhoPagina=2";

        var pagina1 = await ObterJsonAsync(cliente, $"{Lista}?{filtro}&pagina=1");
        var pagina2 = await ObterJsonAsync(cliente, $"{Lista}?{filtro}&pagina=2");
        var pagina3 = await ObterJsonAsync(cliente, $"{Lista}?{filtro}&pagina=3");

        Assert.Equal(["ORDEM-1 e", "ORDEM-1 d"], Descricoes(pagina1));
        Assert.Equal(["ORDEM-1 c", "ORDEM-1 b"], Descricoes(pagina2));
        Assert.Equal(["ORDEM-1 a"], Descricoes(pagina3));
        Assert.Equal((2, 2, 5, 3), (pagina2.GetProperty("pagina").GetInt32(), pagina2.GetProperty("tamanhoPagina").GetInt32(), pagina2.GetProperty("total").GetInt32(), pagina2.GetProperty("totalPaginas").GetInt32()));
    }

    [Fact]
    public async Task Periodo_padrao_sao_os_ultimos_7_dias_ate_hoje_em_Brasilia()
    {
        var (api, elenco) = await ApiAsync();
        await EventoAsync(api.Conexao, Utc(2026, 10, 7, 12), "PADRAO dentro hoje");
        await EventoAsync(api.Conexao, Utc(2026, 10, 1, 3), "PADRAO dentro limite"); // 00:00 de 01/10 em Brasília: o primeiro instante de "hoje menos 6 dias"
        await EventoAsync(api.Conexao, Utc(2026, 10, 1, 2, 59, 59), "PADRAO fora vespera"); // 23:59:59 de 30/09 em Brasília
        await EventoAsync(api.Conexao, Utc(2026, 10, 8, 3), "PADRAO fora amanha"); // 00:00 de 08/10 em Brasília
        var cliente = api.ClienteComo(elenco.Rh);

        var padrao = await ObterJsonAsync(cliente, $"{Lista}?busca=PADRAO");
        var soAte = await ObterJsonAsync(cliente, $"{Lista}?busca=PADRAO&ate=2026-10-03"); // sem "de", vale ate menos 6 dias: 27/09 a 03/10

        Assert.Equal(["PADRAO dentro hoje", "PADRAO dentro limite"], Descricoes(padrao));
        Assert.Equal(["PADRAO dentro limite", "PADRAO fora vespera"], Descricoes(soAte));
    }

    [Fact]
    public async Task Evento_as_23h30_de_07_10_em_Brasilia_entra_no_filtro_do_dia_07_e_nao_no_dia_08()
    {
        // Review Focus 3: 23:30 em Brasília é 02:30 do dia seguinte em UTC. O dia do filtro é o dia de Brasília.
        var (api, elenco) = await ApiAsync();
        await EventoAsync(api.Conexao, Utc(2026, 10, 8, 2, 30), "TARDE-1 as 23h30");
        var cliente = api.ClienteComo(elenco.Rh);

        var dia07 = await ObterJsonAsync(cliente, $"{Lista}?busca=TARDE-1&de=2026-10-07&ate=2026-10-07");
        var dia08 = await ObterJsonAsync(cliente, $"{Lista}?busca=TARDE-1&de=2026-10-08&ate=2026-10-08");

        Assert.Equal(["TARDE-1 as 23h30"], Descricoes(dia07));
        Assert.Empty(Descricoes(dia08));
    }

    [Fact]
    public async Task De_maior_que_Ate_devolve_400_com_errors_de()
    {
        var (api, elenco) = await ApiAsync();

        var resposta = await api.ClienteComo(elenco.Rh).GetAsync($"{Lista}?de=2026-10-07&ate=2026-10-01");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var erros = (await LerAsync(resposta)).GetProperty("errors");
        Assert.Single(erros.EnumerateObject());
        Assert.NotEmpty(erros.GetProperty("de").EnumerateArray());
    }

    [Fact]
    public async Task Datas_fora_do_intervalo_aceito_devolvem_400_e_nao_500()
    {
        // Input hostil: 0001-01-01 e 9999-12-31 estourariam a conversão de fuso (e o "ate mais um dia") e virariam 500.
        var (api, elenco) = await ApiAsync();
        var cliente = api.ClienteComo(elenco.Rh);

        foreach (var url in new[] { $"{Lista}?de=0001-01-01&ate=2026-10-07", $"{Lista}?de=2026-10-01&ate=9999-12-31", $"{Exportacao}?de=0001-01-01", $"{Exportacao}?ate=9999-12-31" })
        {
            var resposta = await cliente.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            var erros = (await LerAsync(resposta)).GetProperty("errors");
            Assert.True(erros.TryGetProperty("de", out _) || erros.TryGetProperty("ate", out _), $"sem erro de data em {url}");
        }
    }

    [Fact]
    public async Task Filtra_por_area_e_por_acao_pelo_nome_do_contrato_e_combina_os_filtros()
    {
        var (api, elenco) = await ApiAsync();
        const string dia = "de=2026-03-12&ate=2026-03-12";
        await EventoAsync(api.Conexao, Utc(2026, 3, 12, 9), "FILTRO-1 acesso negado", AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado);
        await EventoAsync(api.Conexao, Utc(2026, 3, 12, 10), "FILTRO-1 entrou", AuditoriaArea.Acesso, AuditoriaAcao.Entrou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 12, 11), "FILTRO-1 colaborador alterado", AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 12, 12), "FILTRO-1 ponto negado", AuditoriaArea.Ponto, AuditoriaAcao.AcessoNegado);
        var cliente = api.ClienteComo(elenco.Rh);

        var soArea = await ObterJsonAsync(cliente, $"{Lista}?{dia}&area=ACESSO");
        var soAcao = await ObterJsonAsync(cliente, $"{Lista}?{dia}&acao=ACESSO_NEGADO");
        var combinado = await ObterJsonAsync(cliente, $"{Lista}?{dia}&area=ACESSO&acao=ACESSO_NEGADO");
        var semFiltro = await ObterJsonAsync(cliente, $"{Lista}?{dia}");

        Assert.Equal(["FILTRO-1 entrou", "FILTRO-1 acesso negado"], Descricoes(soArea));
        Assert.Equal(["FILTRO-1 ponto negado", "FILTRO-1 acesso negado"], Descricoes(soAcao));
        Assert.Equal(["FILTRO-1 acesso negado"], Descricoes(combinado));
        Assert.Equal(4, semFiltro.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Area_ou_acao_desconhecida_ou_com_o_nome_do_CSharp_devolve_400_com_errors_area_ou_acao()
    {
        var (api, elenco) = await ApiAsync();
        var cliente = api.ClienteComo(elenco.Rh);

        var os2 = await cliente.GetAsync($"{Lista}?area=XYZ&acao=AcessoNegado"); // area desconhecida e acao com o nome do C#
        var doCSharp = await cliente.GetAsync($"{Lista}?area=Colaboradores"); // o nome do C#, não o do contrato (COLABORADORES)
        var tudoJunto = await cliente.GetAsync($"{Exportacao}?area=XYZ&acao=ZZZ&de=2026-10-07&ate=2026-10-01");

        Assert.Equal(HttpStatusCode.BadRequest, os2.StatusCode);
        Assert.Equal(["acao", "area"], (await LerAsync(os2)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToList()); // os dois campos viram dois erros no MESMO 400
        Assert.Equal(HttpStatusCode.BadRequest, doCSharp.StatusCode);
        Assert.Equal(["area"], (await LerAsync(doCSharp)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal(HttpStatusCode.BadRequest, tudoJunto.StatusCode);
        Assert.Equal(["acao", "area", "de"], (await LerAsync(tudoJunto)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToList());
    }

    [Fact]
    public async Task Pagina_e_tamanho_invalidos_juntos_com_area_invalida_viram_erros_no_mesmo_400()
    {
        var (api, elenco) = await ApiAsync();

        var resposta = await api.ClienteComo(elenco.Rh).GetAsync($"{Lista}?pagina=0&tamanhoPagina=101&area=XYZ");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["area", "pagina", "tamanhoPagina"], (await LerAsync(resposta)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToList());
    }

    [Fact]
    public async Task Lista_so_declara_os_parametros_do_contrato()
    {
        // O documento gerado pela API tem, para listarEventosDeAuditoria, exatamente estes parâmetros: sem ordenarPor e direcao (a ordem é fixa).
        // A divergência já acusa; este teste nomeia o motivo.
        var (api, _) = await ApiAsync();

        var documento = await CarregadorDeContrato.GeradoAsync(api.CreateClient());

        var parametros = documento["paths"]![Lista]!["get"]!["parameters"]!.AsArray().Select(p => p!["name"]!.GetValue<string>()).Order().ToList();
        Assert.Equal(["acao", "apenasAlteracoes", "area", "ate", "busca", "de", "pagina", "tamanhoPagina"], parametros);
        var daExportacao = documento["paths"]![Exportacao]!["get"]!["parameters"]!.AsArray().Select(p => p!["name"]!.GetValue<string>()).Order().ToList();
        Assert.Equal(["acao", "area", "ate", "busca", "de"], daExportacao); // sem paginação e sem apenasAlteracoes
    }

    [Fact]
    public async Task ApenasAlteracoes_tira_Entrou_AcessoNegado_Baixou_e_Ponto_Registrou()
    {
        var (api, elenco) = await ApiAsync();
        const string dia = "de=2026-03-14&ate=2026-03-14";
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 8), "ALTERACOES-1 entrou", AuditoriaArea.Acesso, AuditoriaAcao.Entrou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 9), "ALTERACOES-1 negado", AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado);
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 10), "ALTERACOES-1 baixou", AuditoriaArea.Documentos, AuditoriaAcao.Baixou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 11), "ALTERACOES-1 marcou o ponto", AuditoriaArea.Ponto, AuditoriaAcao.Registrou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 12), "ALTERACOES-1 registrou em outra area", AuditoriaArea.Ferias, AuditoriaAcao.Registrou); // só Ponto+Registrou sai
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 13), "ALTERACOES-1 alterou", AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 14), "ALTERACOES-1 criou", AuditoriaArea.Cadastros, AuditoriaAcao.Criou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 14, 15), "ALTERACOES-1 enviou e-mail", AuditoriaArea.Avisos, AuditoriaAcao.EnviouEmail);
        var cliente = api.ClienteComo(elenco.Rh);

        var soAlteracoes = await ObterJsonAsync(cliente, $"{Lista}?{dia}&apenasAlteracoes=true");
        var tudo = await ObterJsonAsync(cliente, $"{Lista}?{dia}");
        var combinado = await ObterJsonAsync(cliente, $"{Lista}?{dia}&apenasAlteracoes=true&area=COLABORADORES");

        Assert.Equal(["ALTERACOES-1 enviou e-mail", "ALTERACOES-1 criou", "ALTERACOES-1 alterou", "ALTERACOES-1 registrou em outra area"], Descricoes(soAlteracoes));
        Assert.Equal(8, tudo.GetProperty("total").GetInt32());
        Assert.Equal(["ALTERACOES-1 alterou"], Descricoes(combinado)); // combina com os demais filtros
    }

    [Fact]
    public async Task Busca_procura_no_nome_do_ator_e_na_descricao_e_trata_percentual_como_literal()
    {
        var (api, elenco) = await ApiAsync();
        const string dia = "de=2026-03-15&ate=2026-03-15";
        await EventoAsync(api.Conexao, Utc(2026, 3, 15, 8), "Reajuste de 100% do beneficio");
        await EventoAsync(api.Conexao, Utc(2026, 3, 15, 9), "Reajuste de 1000 do beneficio");
        await EventoAsync(api.Conexao, Utc(2026, 3, 15, 10), "Campo a_b alterado");
        await EventoAsync(api.Conexao, Utc(2026, 3, 15, 11), "Campo axb alterado");
        await EventoAsync(api.Conexao, Utc(2026, 3, 15, 12), "Evento feito por um membro", ator: elenco.MembroA);
        var cliente = api.ClienteComo(elenco.Rh);

        var porPercentual = await ObterJsonAsync(cliente, $"{Lista}?{dia}&busca={Uri.EscapeDataString("100%")}");
        var porUnderline = await ObterJsonAsync(cliente, $"{Lista}?{dia}&busca=a_b");
        var porNomeDoAtor = await ObterJsonAsync(cliente, $"{Lista}?{dia}&busca={Uri.EscapeDataString(elenco.MembroA.Nome.ToUpperInvariant())}"); // sem diferenciar maiúsculas
        var porDescricao = await ObterJsonAsync(cliente, $"{Lista}?{dia}&busca=reajuste");

        Assert.Equal(["Reajuste de 100% do beneficio"], Descricoes(porPercentual)); // "100%" é o texto literal: não casa "1000"
        Assert.Equal(["Campo a_b alterado"], Descricoes(porUnderline)); // "_" também é literal: não casa "axb"
        Assert.Equal(["Evento feito por um membro"], Descricoes(porNomeDoAtor));
        Assert.Equal(2, porDescricao.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Ator_do_sistema_sai_como_Sistema_sem_funcionarioId_e_desligado_vem_da_ficha_de_hoje()
    {
        var (api, elenco) = await ApiAsync();
        Assert.NotEqual(Perfil.RH, elenco.Inativo.Perfil); // premissa do teste: o perfil gravado no evento (RH) difere do de hoje
        await EventoAsync(api.Conexao, Utc(2026, 3, 16, 9), "ATOR-1 do sistema");
        await EventoAsync(api.Conexao, Utc(2026, 3, 16, 10), "ATOR-1 de quem saiu", ator: elenco.Inativo, perfilDoEvento: Perfil.RH);
        await EventoAsync(api.Conexao, Utc(2026, 3, 16, 11), "ATOR-1 de quem esta ativo", ator: elenco.Gestor);

        var lista = await ObterJsonAsync(api.ClienteComo(elenco.Rh), $"{Lista}?busca=ATOR-1&de=2026-03-16&ate=2026-03-16");

        var itens = lista.GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("descricao").GetString()!, i => i.GetProperty("ator"));
        var sistema = itens["ATOR-1 do sistema"];
        Assert.Equal(JsonValueKind.Null, sistema.GetProperty("funcionarioId").ValueKind);
        Assert.Equal(("Sistema", "SISTEMA", false), (sistema.GetProperty("nome").GetString(), sistema.GetProperty("tipo").GetString(), sistema.GetProperty("desligado").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, sistema.GetProperty("perfil").ValueKind);
        var quemSaiu = itens["ATOR-1 de quem saiu"];
        Assert.Equal((elenco.Inativo.FuncionarioId, "PESSOA", "RH", true), (quemSaiu.GetProperty("funcionarioId").GetInt32(), quemSaiu.GetProperty("tipo").GetString(), quemSaiu.GetProperty("perfil").GetString(), quemSaiu.GetProperty("desligado").GetBoolean()));
        var ativo = itens["ATOR-1 de quem esta ativo"];
        Assert.Equal((elenco.Gestor.FuncionarioId, "Gestor", false), (ativo.GetProperty("funcionarioId").GetInt32(), ativo.GetProperty("perfil").GetString(), ativo.GetProperty("desligado").GetBoolean()));
    }

    [Fact]
    public async Task Detalhe_traz_registroAfetado_origem_e_mudancas_em_ordem_com_valorOculto_sem_antes_nem_depois()
    {
        var (api, elenco) = await ApiAsync();
        var id = await EventoAsync(
            api.Conexao,
            Utc(2026, 3, 17, 12, 38, 12),
            "Cargo de Bruno Lima",
            ator: elenco.Rh,
            registro: "Colaborador Bruno Lima, matrícula 00482",
            ip: "10.0.4.23",
            mudancas: [(3, "Data de admissão", "01/01/2026", "02/01/2026", false), (1, "Cargo", "Analista", "Analista pleno", false), (2, "Senha", null, null, true)]);
        var idDoSistema = await EventoAsync(api.Conexao, Utc(2026, 3, 17, 13), "Tarefa automatica");
        var cliente = api.ClienteComo(elenco.Rh);

        var detalhe = await ObterJsonAsync(cliente, $"{Lista}/{id}");
        var doSistema = await ObterJsonAsync(cliente, $"{Lista}/{idDoSistema}");

        Assert.Equal(("Colaborador Bruno Lima, matrícula 00482", "2026-03-17T12:38:12Z", "COLABORADORES", "ALTEROU"), (detalhe.GetProperty("registroAfetado").GetString(), detalhe.GetProperty("quandoUtc").GetString(), detalhe.GetProperty("area").GetString(), detalhe.GetProperty("acao").GetString()));
        Assert.Equal(("PORTAL", "10.0.4.23"), (detalhe.GetProperty("origem").GetProperty("canal").GetString(), detalhe.GetProperty("origem").GetProperty("ip").GetString()));
        var mudancas = detalhe.GetProperty("mudancas").EnumerateArray().ToList();
        Assert.Equal(["Cargo", "Senha", "Data de admissão"], mudancas.Select(m => m.GetProperty("campo").GetString())); // pela Ordem gravada, não pela de inserção
        Assert.Equal(("Analista", "Analista pleno", false), (mudancas[0].GetProperty("antes").GetString(), mudancas[0].GetProperty("depois").GetString(), mudancas[0].GetProperty("valorOculto").GetBoolean()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, true), (mudancas[1].GetProperty("antes").ValueKind, mudancas[1].GetProperty("depois").ValueKind, mudancas[1].GetProperty("valorOculto").GetBoolean()));
        Assert.Equal(("SISTEMA", JsonValueKind.Null), (doSistema.GetProperty("origem").GetProperty("canal").GetString(), doSistema.GetProperty("origem").GetProperty("ip").ValueKind));
        Assert.Empty(doSistema.GetProperty("mudancas").EnumerateArray()); // sem mudanças: lista vazia, não ausente
    }

    [Fact]
    public async Task Detalhe_de_id_inexistente_devolve_404()
    {
        // Sem a rota, o roteamento também devolve 404: por isso o teste exige antes um 200 num id que existe.
        var (api, elenco) = await ApiAsync();
        var existente = await EventoAsync(api.Conexao, Utc(2026, 3, 23, 12), "DETALHE-404 existente");
        var cliente = api.ClienteComo(elenco.Rh);

        var achou = await cliente.GetAsync($"{Lista}/{existente}");
        var resposta = await cliente.GetAsync($"{Lista}/987654321");

        Assert.Equal(HttpStatusCode.OK, achou.StatusCode);
        await AssertProblemaAsync(resposta, 404, "Não encontrado.", "O registro não foi encontrado.");
    }

    private static async Task<(HttpResponseMessage Resposta, string Texto, byte[] Bytes)> BaixarAsync(HttpClient cliente, string url)
    {
        var resposta = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        return (resposta, new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3), bytes);
    }

    [Fact]
    public async Task Exportacao_devolve_csv_com_BOM_titulos_rotulos_da_tela_e_attachment_com_a_data_de_hoje()
    {
        var (api, elenco) = await ApiAsync();
        await EventoAsync(api.Conexao, Utc(2026, 3, 18, 12, 38, 12), "Cargo de Bruno Lima", AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou, elenco.Rh, registro: "Colaborador Bruno Lima, matrícula 00482", ip: "10.0.4.23");
        await EventoAsync(api.Conexao, Utc(2026, 3, 18, 3), "Contagem de rotina", AuditoriaArea.Ferias, AuditoriaAcao.PediuAjuste, registro: "Pedido");

        var (resposta, texto, bytes) = await BaixarAsync(api.ClienteComo(elenco.Rh), $"{Exportacao}?de=2026-03-18&ate=2026-03-18");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Equal("text/csv", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment; filename=\"auditoria-2026-10-07.csv\"", Assert.Single(resposta.Content.Headers.GetValues("Content-Disposition")));
        Assert.Equal(
            [
                "Quando;Quem;Perfil;Área;Ação;O que aconteceu;Registro afetado;Origem;IP",
                $"18/03/2026 09:38:12;{elenco.Rh.Nome};RH;Colaboradores;Alterou;Cargo de Bruno Lima;Colaborador Bruno Lima, matrícula 00482;Portal no navegador;10.0.4.23",
                "18/03/2026 00:00:00;Sistema;Automático;Férias;Pediu ajuste;Contagem de rotina;Pedido;Automático;",
                string.Empty,
            ],
            texto.Split("\r\n"));
    }

    [Fact]
    public async Task Exportacao_neutraliza_celula_que_comeca_com_igual()
    {
        var (api, elenco) = await ApiAsync();
        await EventoAsync(api.Conexao, Utc(2026, 3, 20, 9), "=1+1", registro: "=SUM(A1)");
        await EventoAsync(api.Conexao, Utc(2026, 3, 20, 10), "+cmd");
        await EventoAsync(api.Conexao, Utc(2026, 3, 20, 11), "-2+3");
        await EventoAsync(api.Conexao, Utc(2026, 3, 20, 12), "@soma");

        var (_, texto, _) = await BaixarAsync(api.ClienteComo(elenco.Rh), $"{Exportacao}?de=2026-03-20&ate=2026-03-20");

        var linhas = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        Assert.Equal(4, linhas.Count);
        Assert.Contains(linhas, l => l.Contains(";'=1+1;'=SUM(A1);", StringComparison.Ordinal)); // descrição e registro afetado: toda célula de texto passa pelo CsvSeguro
        Assert.Contains(linhas, l => l.Contains(";'+cmd;", StringComparison.Ordinal));
        Assert.Contains(linhas, l => l.Contains(";'-2+3;", StringComparison.Ordinal));
        Assert.Contains(linhas, l => l.Contains(";'@soma;", StringComparison.Ordinal));
        Assert.DoesNotContain(linhas, l => l.Contains(";=1+1;", StringComparison.Ordinal) || l.Contains(";+cmd;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exportacao_nao_pagina_e_respeita_os_mesmos_filtros_da_lista()
    {
        var (api, elenco) = await ApiAsync();
        for (var i = 1; i <= 25; i++)
        {
            await EventoAsync(api.Conexao, Utc(2026, 3, 19, 6).AddMinutes(i), $"EXPORTACAO-1 n{i:00}", AuditoriaArea.Ponto, AuditoriaAcao.Registrou);
        }

        await EventoAsync(api.Conexao, Utc(2026, 3, 19, 20), "EXPORTACAO-1 de outra area", AuditoriaArea.Avisos, AuditoriaAcao.Criou);
        await EventoAsync(api.Conexao, Utc(2026, 3, 22, 12), "EXPORTACAO-1 de outro dia", AuditoriaArea.Ponto, AuditoriaAcao.Registrou);
        var cliente = api.ClienteComo(elenco.Rh);

        var (_, todos, _) = await BaixarAsync(cliente, $"{Exportacao}?de=2026-03-19&ate=2026-03-19&busca=EXPORTACAO-1&pagina=1&tamanhoPagina=5"); // pagina e tamanho nem existem aqui
        var (_, soPonto, _) = await BaixarAsync(cliente, $"{Exportacao}?de=2026-03-19&ate=2026-03-19&busca=EXPORTACAO-1&area=PONTO&acao=REGISTROU");
        var (_, apenasAlteracoes, _) = await BaixarAsync(cliente, $"{Exportacao}?de=2026-03-19&ate=2026-03-19&busca=EXPORTACAO-1&apenasAlteracoes=true"); // não é parâmetro da exportação: não filtra

        Assert.Equal(1 + 26, todos.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length); // título + 25 + 1 de outra área; fora o de outro dia
        Assert.Equal(1 + 25, soPonto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(1 + 26, apenasAlteracoes.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("de outro dia", todos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exportacao_entra_no_log_como_Acesso_Baixou_e_nao_aparece_no_proprio_arquivo()
    {
        var (api, elenco) = await ApiAsync();
        const string descricao = "Exportou o log de auditoria em CSV";
        var antes = (await EventosDoAtorAsync(api.Conexao, elenco.Rh.FuncionarioId)).Count(e => e.Descricao == descricao);

        var (_, texto, _) = await BaixarAsync(api.ClienteComo(elenco.Rh), $"{Exportacao}?de=2026-10-01&ate=2026-10-07"); // o período padrão inclui hoje

        var eventos = (await EventosDoAtorAsync(api.Conexao, elenco.Rh.FuncionarioId)).Where(e => e.Descricao == descricao).ToList();
        Assert.Equal(antes + 1, eventos.Count);
        var novo = eventos[^1];
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Baixou, TipoDeAtor.Pessoa, "127.0.0.1", api.Relogio.AgoraUtc), (novo.Area, novo.Acao, novo.AtorTipo, novo.Ip, novo.Quando));
        var noArquivo = texto.Split(descricao).Length - 1;
        Assert.Equal(antes, noArquivo); // só as exportações ANTERIORES: a de agora foi gravada depois de gerar o arquivo, então não entrou nele
    }

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_nas_tres_rotas_e_anonimo_401()
    {
        var (api, elenco) = await ApiAsync();
        string[] rotas = [Lista, Exportacao, $"{Lista}/1"];

        foreach (var cliente in new[] { api.ClienteComo(elenco.Gestor), api.ClienteComo(elenco.MembroA) })
        {
            foreach (var rota in rotas)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(rota)).StatusCode);
            }
        }

        foreach (var rota in rotas)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClienteAnonimo().GetAsync(rota)).StatusCode);
        }
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var (api, elenco) = await ApiAsync();
        var id = await EventoAsync(api.Conexao, Utc(2026, 3, 21, 12), "SCHEMA-1", ator: elenco.Rh, mudancas: [(1, "Cargo", "A", "B", false), (2, "Senha", null, null, true)]);
        var cliente = api.ClienteComo(elenco.Rh);

        await (await cliente.GetAsync($"{Lista}?busca=SCHEMA-1&de=2026-03-21&ate=2026-03-21")).AfirmarConformeAoContratoAsync("listarEventosDeAuditoria");
        await (await cliente.GetAsync($"{Lista}/{id}")).AfirmarConformeAoContratoAsync("obterEventoDeAuditoria");
        await (await cliente.GetAsync($"{Lista}/987654321")).AfirmarConformeAoContratoAsync("obterEventoDeAuditoria"); // o 404 também
        await (await cliente.GetAsync($"{Lista}?de=2026-10-07&ate=2026-10-01")).AfirmarConformeAoContratoAsync("listarEventosDeAuditoria"); // e o 400
    }
}
