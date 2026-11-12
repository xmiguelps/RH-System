using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// Os avisos de quem está logado (<c>/api/eu/avisos</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. Qualquer perfil vê os mesmos avisos <b>vigentes</b> (publicados, já no ar e sem expirar): rascunho, agendado e expirado nunca aparecem. Abrir o aviso
/// (<c>GET</c>) não marca como lido: quem abre chama também <c>POST .../leitura</c>, idempotente, que mantém a data da primeira leitura e cria a linha de quem entrou depois. As contagens (e a bolinha do menu) não dependem de busca nem do filtro.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MeusAvisosTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/avisos";
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MeusAvisosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Titulo(string prefixo) => $"{prefixo} {Interlocked.Increment(ref _sequencia)}";

    private static async Task LimparAsync(ApiDeAcesso api)
    {
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS_VISUALIZACOES");
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS");
    }

    /// <summary>Um aviso direto no banco (datas LOCAIS). O trigger cria a linha de leitura de cada pessoa ativa agora.</summary>
    private static async Task<int> SemearAsync(ApiDeAcesso api, string titulo, bool enviado, string? publicacaoLocal, string? expiracaoLocal = null, bool fixado = false, string conteudo = "Conteúdo do aviso.", string tipo = "SIMPLES")
    {
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO AVISOS (ID_FUNCIONARIO, TITULO, CONTEUDO, DATA_PUBLICACAO, DATA_EXPIRACAO, TIPO_AVISO, FIXADO, ENVIADO) VALUES ({api.Elenco.Rh.FuncionarioId}, '{titulo}', '{conteudo}', '{publicacaoLocal ?? "2000-01-01 00:00:00"}', {(expiracaoLocal is null ? "NULL" : $"'{expiracaoLocal}'")}, '{tipo}', {(fixado ? 1 : 0)}, {(enviado ? 1 : 0)})");
        var id = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_AVISO FROM AVISOS WHERE TITULO = '{titulo}'");
        if (publicacaoLocal is null)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS SET DATA_PUBLICACAO = NULL WHERE ID_AVISO = {id}");
        }

        return id;
    }

    private static async Task<HttpClient> ComoAsync(ApiDeAcesso api, PessoaDeTeste pessoa) => await api.ClienteAgoraAsync(pessoa.UsuarioId);

    private static async Task<JsonElement> ListarAsync(ApiDeAcesso api, PessoaDeTeste pessoa, string consulta = "") =>
        await CorpoAsync(await (await ComoAsync(api, pessoa)).GetAsync(consulta.Length == 0 ? Rota : $"{Rota}?{consulta}"));

    private static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("id").GetInt32())];

    private static async Task<HttpResponseMessage> MarcarAsync(ApiDeAcesso api, PessoaDeTeste pessoa, int id) => await (await ComoAsync(api, pessoa)).PostAsJsonAsync($"{Rota}/{id}/leitura", new { });

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_traz_so_os_vigentes_do_mais_recente_para_o_mais_antigo_com_o_lido_de_cada_pessoa_e_as_contagens_independentes_do_filtro()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var antigo = await SemearAsync(api, "Vigente antigo", true, "2026-10-01 08:00:00", fixado: true);
        var meio = await SemearAsync(api, "Vigente do meio", true, "2026-10-03 08:00:00", conteudo: "Café na copa.");
        var recente = await SemearAsync(api, "Vigente recente", true, "2026-10-05 08:00:00", "2026-10-30 23:59:59");
        await SemearAsync(api, "Expirado", true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        await SemearAsync(api, "Agendado", true, "2026-10-09 08:00:00");
        await SemearAsync(api, "Rascunho", false, null);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS_VISUALIZACOES SET VISUALIZADO = TRUE, DATA_VISUALIZACAO = '2026-10-06 10:00:00' WHERE ID_AVISO = {meio} AND ID_FUNCIONARIO = {api.Elenco.MembroA.FuncionarioId}");

        var todos = await ListarAsync(api, api.Elenco.MembroA);
        var fixadosPrimeiro = await ListarAsync(api, api.Elenco.MembroA, "fixadosPrimeiro=true");
        var naoLidos = await ListarAsync(api, api.Elenco.MembroA, "apenasNaoLidos=true");
        var buscando = await ListarAsync(api, api.Elenco.MembroA, "busca=caf%C3%A9");
        var daOutraPessoa = await ListarAsync(api, api.Elenco.MembroB);

        Assert.Equal([recente, meio, antigo], Ids(todos)); // sem expirado, agendado nem rascunho
        Assert.Equal([antigo, recente, meio], Ids(fixadosPrimeiro)); // o fixado antes, e cada grupo do mais recente para o mais antigo
        Assert.Equal([recente, antigo], Ids(naoLidos));
        Assert.Equal([meio], Ids(buscando));
        foreach (var lista in new[] { todos, fixadosPrimeiro, naoLidos, buscando })
        {
            Assert.Equal((2, 3), (lista.GetProperty("contagens").GetProperty("naoLidos").GetInt32(), lista.GetProperty("contagens").GetProperty("total").GetInt32())); // não dependem de busca nem do filtro
        }

        Assert.Equal((3, 1, 3, 1), (todos.GetProperty("total").GetInt32(), todos.GetProperty("pagina").GetInt32(), todos.GetProperty("tamanhoPagina").GetInt32() == 20 ? 3 : 0, todos.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal([false, true, false], todos.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("lido").GetBoolean()));
        Assert.Equal(3, daOutraPessoa.GetProperty("contagens").GetProperty("naoLidos").GetInt32()); // o lido é de cada pessoa
        var item = todos.GetProperty("itens")[0];
        Assert.Equal(("SIMPLES", "Vigente recente", "Conteúdo do aviso.", false, "2026-10-05T11:00:00Z", "2026-10-31T02:59:59Z", "Ana Souza"), (item.GetProperty("tipo").GetString(), item.GetProperty("titulo").GetString(), item.GetProperty("previa").GetString(), item.GetProperty("fixado").GetBoolean(), item.GetProperty("publicadoEmUtc").GetString(), item.GetProperty("expiraEmUtc").GetString(), item.GetProperty("autorNome").GetString()));
    }

    [Fact]
    public async Task Todos_os_perfis_veem_os_mesmos_avisos_e_a_pagina_da_tela_inicial_tem_dois()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var a = await SemearAsync(api, "Perfis A", true, "2026-10-01 08:00:00");
        var b = await SemearAsync(api, "Perfis B", true, "2026-10-02 08:00:00");
        var c = await SemearAsync(api, "Perfis C", true, "2026-10-03 08:00:00");

        var doGestor = await ListarAsync(api, api.Elenco.Gestor);
        var doRh = await ListarAsync(api, api.Elenco.Rh);
        var doFuncionario = await ListarAsync(api, api.Elenco.MembroA, "pagina=1&tamanhoPagina=2");
        var segunda = await ListarAsync(api, api.Elenco.MembroA, "pagina=2&tamanhoPagina=2");

        Assert.Equal([c, b, a], Ids(doGestor));
        Assert.Equal([c, b, a], Ids(doRh));
        Assert.Equal([c, b], Ids(doFuncionario));
        Assert.Equal([a], Ids(segunda));
        Assert.Equal((3, 2), (doFuncionario.GetProperty("total").GetInt32(), doFuncionario.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal(3, doFuncionario.GetProperty("contagens").GetProperty("total").GetInt32()); // a contagem é de todos, não da página
    }

    [Fact]
    public async Task Quem_foi_admitido_depois_da_publicacao_ve_o_aviso_vigente_como_nao_lido_mesmo_sem_linha_de_leitura()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var id = await SemearAsync(api, "Antes da admissão", true, "2026-10-01 08:00:00");
        var email = $"admitido.depois.{Interlocked.Increment(ref _sequencia)}@empresa.com.br";
        await api.Api.CriarUsuarioAsync(email, nivel: "RH");
        var funcionario = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
        var usuario = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionario}");
        var novo = new PessoaDeTeste(funcionario, usuario, "Admitido depois", email, RHSystem.Domain.Autenticacao.Perfil.RH);

        var lista = await ListarAsync(api, novo);

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {id} AND ID_FUNCIONARIO = {funcionario}"));
        Assert.Equal([id], Ids(lista));
        Assert.Equal((false, 1, 1), (lista.GetProperty("itens")[0].GetProperty("lido").GetBoolean(), lista.GetProperty("contagens").GetProperty("naoLidos").GetInt32(), lista.GetProperty("contagens").GetProperty("total").GetInt32()));
    }

    [Theory]
    [InlineData("pagina=0")]
    [InlineData("tamanhoPagina=0")]
    [InlineData("tamanhoPagina=101")]
    [InlineData("apenasNaoLidos=talvez")]
    [InlineData("fixadosPrimeiro=2")]
    public async Task A_lista_com_parametro_invalido_e_400(string consulta)
    {
        var api = await ApiAsync();

        Assert.Equal(400, (int)(await (await ComoAsync(api, api.Elenco.MembroA)).GetAsync($"{Rota}?{consulta}")).StatusCode);
    }

    // ---------------------------------------------------------------- obter

    [Fact]
    public async Task Obter_traz_o_conteudo_inteiro_e_nao_marca_como_lido_e_o_que_nao_esta_vigente_e_404()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var vigente = await SemearAsync(api, "Para abrir", true, "2026-10-01 08:00:00", conteudo: "## Seção\nTexto longo do aviso.", tipo: "DETALHADO");
        var expirado = await SemearAsync(api, "Expirado ao abrir", true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        var agendado = await SemearAsync(api, "Agendado ao abrir", true, "2026-10-09 08:00:00");
        var rascunho = await SemearAsync(api, "Rascunho ao abrir", false, null);
        var cliente = await ComoAsync(api, api.Elenco.MembroA);

        var resposta = await cliente.GetAsync($"{Rota}/{vigente}");
        var aviso = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("DETALHADO", "## Seção\nTexto longo do aviso.", "Texto longo do aviso.", false, JsonValueKind.Null), (aviso.GetProperty("tipo").GetString(), aviso.GetProperty("conteudo").GetString(), aviso.GetProperty("previa").GetString(), aviso.GetProperty("lido").GetBoolean(), aviso.GetProperty("lidoEmUtc").ValueKind));
        Assert.Equal("False", await TextoAsync(api, $"SELECT VISUALIZADO FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {vigente} AND ID_FUNCIONARIO = {api.Elenco.MembroA.FuncionarioId}")); // abrir não marca
        foreach (var id in new[] { expirado, agendado, rascunho, int.MaxValue })
        {
            Assert.Equal(404, (int)(await cliente.GetAsync($"{Rota}/{id}")).StatusCode);
        }
    }

    // ---------------------------------------------------------------- marcar como lido

    [Fact]
    public async Task Marcar_como_lido_grava_a_data_da_primeira_leitura_e_repetir_nao_muda_nada()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var id = await SemearAsync(api, "Para ler", true, "2026-10-01 08:00:00");
        var pessoa = api.Elenco.MembroA;

        var primeira = await MarcarAsync(api, pessoa, id);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddHours(5));
        var segunda = await MarcarAsync(api, pessoa, id);

        Assert.Equal((204, 204), ((int)primeira.StatusCode, (int)segunda.StatusCode));
        Assert.Equal(("True", "2026-10-07 09:42:00"), (await TextoAsync(api, $"SELECT VISUALIZADO FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {id} AND ID_FUNCIONARIO = {pessoa.FuncionarioId}"), Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_VISUALIZACAO FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {id} AND ID_FUNCIONARIO = {pessoa.FuncionarioId}")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {id} AND ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        var aviso = await CorpoAsync(await (await ComoAsync(api, pessoa)).GetAsync($"{Rota}/{id}"));
        Assert.Equal((true, "2026-10-07T12:42:00Z"), (aviso.GetProperty("lido").GetBoolean(), aviso.GetProperty("lidoEmUtc").GetString()));
        Assert.Equal(0, (await ListarAsync(api, pessoa)).GetProperty("contagens").GetProperty("naoLidos").GetInt32());
    }

    [Fact]
    public async Task Marcar_como_lido_cria_a_linha_de_quem_foi_admitido_depois_e_so_vale_para_aviso_vigente()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var vigente = await SemearAsync(api, "Linha nova", true, "2026-10-01 08:00:00");
        var expirado = await SemearAsync(api, "Expirado ao marcar", true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        var agendado = await SemearAsync(api, "Agendado ao marcar", true, "2026-10-09 08:00:00");
        var rascunho = await SemearAsync(api, "Rascunho ao marcar", false, null);
        var email = $"linha.nova.{Interlocked.Increment(ref _sequencia)}@empresa.com.br";
        await api.Api.CriarUsuarioAsync(email, nivel: "RH");
        var funcionario = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
        var usuario = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionario}");
        var novo = new PessoaDeTeste(funcionario, usuario, "Linha nova", email, RHSystem.Domain.Autenticacao.Perfil.RH);

        var resposta = await MarcarAsync(api, novo, vigente);

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal(("True", "True"), (await TextoAsync(api, $"SELECT VISUALIZADO FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {vigente} AND ID_FUNCIONARIO = {funcionario}"), (await TextoAsync(api, $"SELECT DATA_VISUALIZACAO IS NOT NULL FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {vigente} AND ID_FUNCIONARIO = {funcionario}")) is "1" or "True" ? "True" : "False"));
        foreach (var id in new[] { expirado, agendado, rascunho, int.MaxValue })
        {
            Assert.Equal(404, (int)(await MarcarAsync(api, api.Elenco.MembroA, id)).StatusCode);
        }

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO IN ({expirado}, {agendado}, {rascunho}) AND VISUALIZADO = TRUE"));
    }

    [Fact]
    public async Task Marcar_como_lido_em_paralelo_dos_dois_jeitos_deixa_uma_linha_so_lida_e_nenhum_500()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var id = await SemearAsync(api, "Corrida de leitura", true, "2026-10-01 08:00:00");
        var email = $"corrida.leitura.{Interlocked.Increment(ref _sequencia)}@empresa.com.br";
        await api.Api.CriarUsuarioAsync(email, nivel: "RH");
        var funcionario = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
        var usuario = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionario}");
        var novo = new PessoaDeTeste(funcionario, usuario, "Corrida", email, RHSystem.Domain.Autenticacao.Perfil.RH);
        var cliente = await ComoAsync(api, novo);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => cliente.PostAsJsonAsync($"{Rota}/{id}/leitura", new { })));

        Assert.All(respostas, r => Assert.Equal(204, (int)r.StatusCode));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {id} AND ID_FUNCIONARIO = {funcionario} AND VISUALIZADO = TRUE"));
    }

    // ---------------------------------------------------------------- a bolinha do menu

    [Fact]
    public async Task A_bolinha_do_menu_e_a_contagem_de_nao_lidos_da_lista_e_cai_quando_a_pessoa_abre_o_aviso()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var a = await SemearAsync(api, "Menu A", true, "2026-10-01 08:00:00");
        await SemearAsync(api, "Menu B", true, "2026-10-02 08:00:00");
        await SemearAsync(api, "Menu expirado", true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        var pessoa = api.Elenco.MembroA;
        var cliente = await ComoAsync(api, pessoa);

        var antes = await CorpoAsync(await cliente.GetAsync("/api/eu/contadores-do-menu"));
        await MarcarAsync(api, pessoa, a);
        var depois = await CorpoAsync(await cliente.GetAsync("/api/eu/contadores-do-menu"));

        Assert.Equal((2, 1), (antes.GetProperty("avisosNaoLidos").GetInt32(), depois.GetProperty("avisosNaoLidos").GetInt32()));
        Assert.Equal(depois.GetProperty("avisosNaoLidos").GetInt32(), (await ListarAsync(api, pessoa)).GetProperty("contagens").GetProperty("naoLidos").GetInt32());
    }

    // ---------------------------------------------------------------- quem pode e contrato

    [Fact]
    public async Task Sem_login_e_401_e_as_respostas_seguem_o_contrato()
    {
        var api = await ApiAsync();
        await LimparAsync(api);
        var id = await SemearAsync(api, "Do contrato", true, "2026-10-01 08:00:00", fixado: true);
        var rascunho = await SemearAsync(api, "Rascunho do contrato", false, null);
        var cliente = await ComoAsync(api, api.Elenco.MembroA);

        Assert.Equal(401, (int)(await api.Api.CreateClient().GetAsync(Rota)).StatusCode);
        await (await cliente.GetAsync(Rota)).AfirmarConformeAoContratoAsync("listarMeusAvisos");
        await (await cliente.GetAsync($"{Rota}?pagina=0")).AfirmarConformeAoContratoAsync("listarMeusAvisos"); // 400
        await (await cliente.GetAsync($"{Rota}/{id}")).AfirmarConformeAoContratoAsync("obterMeuAviso");
        await (await cliente.GetAsync($"{Rota}/{rascunho}")).AfirmarConformeAoContratoAsync("obterMeuAviso"); // 404
        await (await MarcarAsync(api, api.Elenco.MembroA, id)).AfirmarConformeAoContratoAsync("marcarAvisoComoLido");
        await (await MarcarAsync(api, api.Elenco.MembroA, rascunho)).AfirmarConformeAoContratoAsync("marcarAvisoComoLido"); // 404
        await (await cliente.GetAsync($"{Rota}/{id}")).AfirmarConformeAoContratoAsync("obterMeuAviso"); // já lido
    }
}
