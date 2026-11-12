using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Comunicacao.AdvertenciasDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// As advertências de quem está logado (<c>/api/eu/advertencias</c>, qualquer perfil), no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 no horário local). A lista só tem as da própria pessoa, as pendentes primeiro; a ciência e a contestação saem só de PENDENTE (um UPDATE
/// condicional: dois cliques ao mesmo tempo têm um efeito só, e a primeira contestação nunca é sobrescrita); advertência de outra pessoa dá 404; nenhum e-mail sai.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MinhasAdvertenciasTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/advertencias";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MinhasAdvertenciasTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(int Id, string Nome, HttpClient Cliente)> PessoaAsync(ApiDeAcesso api, SetorDeAdvertencia setor)
    {
        var (id, nome) = await NovoMembroAsync(api, setor);
        return (id, nome, await ClienteDeAsync(api, id));
    }

    private static async Task<HttpResponseMessage> CienciaAsync(HttpClient cliente, int id) => await PostAsync(cliente, $"{Rota}/{id}/ciencia", new { });

    private static async Task<HttpResponseMessage> ContestarAsync(HttpClient cliente, int id, string texto) => await PostAsync(cliente, $"{Rota}/{id}/contestacao", new { texto });

    private const string TextoOk = "Eu estava de folga naquele dia.";

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_e_so_da_pessoa_com_as_pendentes_primeiro_e_depois_a_data_do_ocorrido_mais_recente()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _, cliente) = await PessoaAsync(api, setor);
        var (outra, _) = await NovoMembroAsync(api, setor);
        var antigaCiente = await SemearAsync(api, pessoa, "2026-01-10", situacao: "APLICADA", cienciaLocal: "2026-01-11 10:00:00");
        var pendenteAntiga = await SemearAsync(api, pessoa, "2026-02-10");
        var recenteCiente = await SemearAsync(api, pessoa, "2026-09-10", situacao: "APLICADA", cienciaLocal: "2026-09-11 10:00:00");
        var pendenteRecente = await SemearAsync(api, pessoa, "2026-08-10");
        var deOutroAno = await SemearAsync(api, pessoa, "2024-08-10", situacao: "CANCELADA", textoContestacao: "Contestei.", contestacaoLocal: "2024-08-11 10:00:00");
        await SemearAsync(api, outra, "2026-09-12"); // de outra pessoa: nunca aparece

        var lista = await CorpoAsync(await cliente.GetAsync(Rota));

        Assert.Equal([pendenteRecente, pendenteAntiga, recenteCiente, antigaCiente, deOutroAno], Ids(lista));
        Assert.Equal((5, 1), (lista.GetProperty("total").GetInt32(), lista.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal(["id", "data", "tipo", "motivo", "descricao", "aplicadaPor", "situacao", "dataCienciaUtc", "textoContestacao", "dataContestacaoUtc", "desfechoDaContestacao"], [.. lista.GetProperty("itens")[0].EnumerateObject().Select(p => p.Name)]);
        var ciente = lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == recenteCiente);
        Assert.Equal(("2026-09-11T13:00:00Z", JsonValueKind.Null), (ciente.GetProperty("dataCienciaUtc").GetString(), ciente.GetProperty("desfechoDaContestacao").ValueKind));
        var cancelada = lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == deOutroAno);
        Assert.Equal(("CANCELADA", "ACATADA", "Contestei."), (cancelada.GetProperty("situacao").GetString(), cancelada.GetProperty("desfechoDaContestacao").GetString(), cancelada.GetProperty("textoContestacao").GetString()));
        Assert.Equal([pendenteAntiga, pendenteRecente], Ids(await CorpoAsync(await cliente.GetAsync($"{Rota}?situacao=PENDENTE"))).Order().ToList());
        Assert.Equal([pendenteAntiga], Ids(await CorpoAsync(await cliente.GetAsync($"{Rota}?tamanhoPagina=1&pagina=2"))));
    }

    [Theory]
    [InlineData("situacao=ABC", "situacao")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("tamanhoPagina=101", "tamanhoPagina")]
    public async Task Parametro_invalido_da_lista_e_400_com_o_erro_no_campo(string consulta, string campo)
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (_, _, cliente) = await PessoaAsync(api, setor);

        var resposta = await cliente.GetAsync($"{Rota}?{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task O_gestor_e_o_RH_tambem_recebem_advertencia_e_listam_so_as_proprias_pela_mesma_rota()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var doGestor = await SemearAsync(api, setor.GestorFuncionarioId, "2026-09-01");
        var doRh = await SemearAsync(api, api.Elenco.Rh.FuncionarioId, "2026-09-02");

        var gestor = await CorpoAsync(await (await ClienteDeAsync(api, setor.GestorFuncionarioId)).GetAsync(Rota));
        var rh = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota));

        Assert.Equal([doGestor], Ids(gestor));
        Assert.Contains(doRh, Ids(rh));
        Assert.DoesNotContain(doGestor, Ids(rh));
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(Rota)).StatusCode);
    }

    // ---------------------------------------------------------------- a ciência

    [Fact]
    public async Task A_ciencia_passa_de_PENDENTE_para_APLICADA_com_a_data_do_relogio_o_evento_sem_texto_e_nenhum_e_mail()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, nome, cliente) = await PessoaAsync(api, setor);
        var id = await SemearAsync(api, pessoa, "2026-10-05", descricao: "descricao-sigilosa-9");
        var emailsAntes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");

        var resposta = await CienciaAsync(cliente, id);

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("APLICADA", "2026-10-07T12:42:00Z", JsonValueKind.Null, "descricao-sigilosa-9"), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("dataCienciaUtc").GetString(), corpo.GetProperty("desfechoDaContestacao").ValueKind, corpo.GetProperty("descricao").GetString()));
        Assert.Equal(("APLICADA", "2026-10-07 09:42:00"), (await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CIENCIA, '%Y-%m-%d %H:%i:%s') FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}")));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
        Assert.Equal((AuditoriaArea.Advertencias, AuditoriaAcao.Alterou, "Confirmou a ciência da advertência de 05/10/2026.", $"Advertência de {nome}", pessoa), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId ?? 0));
        Assert.DoesNotContain("sigilosa", evento.Descricao, StringComparison.Ordinal);
        Assert.Equal(("situacao", "PENDENTE", "APLICADA"), (evento.Mudancas[0].Campo, evento.Mudancas[0].ValorAntes, evento.Mudancas[0].ValorDepois));
        Assert.Equal(emailsAntes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));
    }

    [Fact]
    public async Task A_ciencia_repetida_ou_depois_de_contestar_e_422_e_nada_muda()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _, cliente) = await PessoaAsync(api, setor);
        var ciente = await SemearAsync(api, pessoa, "2026-10-01");
        var contestada = await SemearAsync(api, pessoa, "2026-10-02");

        var primeira = await CienciaAsync(cliente, ciente);
        var repetida = await CienciaAsync(cliente, ciente);
        await ContestarAsync(cliente, contestada, TextoOk);
        var depoisDeContestar = await CienciaAsync(cliente, contestada);

        Assert.Equal(200, (int)primeira.StatusCode);
        foreach (var resposta in new[] { repetida, depoisDeContestar })
        {
            Assert.Equal(422, (int)resposta.StatusCode);
            var erro = await CorpoAsync(resposta);
            Assert.Equal(("ADVERTENCIA_NAO_ESTA_PENDENTE", "Esta advertência já teve ciência ou contestação."), (erro.GetProperty("codigo").GetString(), erro.GetProperty("detail").GetString()));
        }

        Assert.Equal(("APLICADA", "CONTESTADA"), (await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {ciente}"), await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {contestada}")));
        Assert.Null(await TextoAsync(api, $"SELECT DATA_CIENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {contestada}"));
        Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", ciente));
    }

    [Fact]
    public async Task Ciencia_de_advertencia_de_outra_pessoa_ou_inexistente_e_404_e_nao_muda_nada()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (_, _, cliente) = await PessoaAsync(api, setor);
        var (outra, _) = await NovoMembroAsync(api, setor);
        var deOutra = await SemearAsync(api, outra, "2026-10-01");

        Assert.Equal(404, (int)(await CienciaAsync(cliente, deOutra)).StatusCode);
        Assert.Equal(404, (int)(await CienciaAsync(cliente, int.MaxValue)).StatusCode);
        Assert.Equal(404, (int)(await ContestarAsync(cliente, deOutra, TextoOk)).StatusCode);
        Assert.Equal("PENDENTE", await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {deOutra}"));
        Assert.Empty(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", deOutra));
    }

    // ---------------------------------------------------------------- a contestação

    [Fact]
    public async Task A_contestacao_passa_de_PENDENTE_para_CONTESTADA_com_o_texto_aparado_a_data_do_relogio_sem_e_mail_e_o_evento_sem_o_texto()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, nome, cliente) = await PessoaAsync(api, setor);
        var id = await SemearAsync(api, pessoa, "2026-10-05");
        var emailsAntes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");

        var resposta = await ContestarAsync(cliente, id, "   texto-da-contestacao-privado   ");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("CONTESTADA", "texto-da-contestacao-privado", "2026-10-07T12:42:00Z", JsonValueKind.Null, JsonValueKind.Null), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("textoContestacao").GetString(), corpo.GetProperty("dataContestacaoUtc").GetString(), corpo.GetProperty("desfechoDaContestacao").ValueKind, corpo.GetProperty("dataCienciaUtc").ValueKind));
        Assert.Equal(("CONTESTADA", "texto-da-contestacao-privado", "2026-10-07 09:42:00"), (await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"), await TextoAsync(api, $"SELECT TEXTO_CONTESTACAO FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CONTESTACAO, '%Y-%m-%d %H:%i:%s') FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}")));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
        Assert.Equal((AuditoriaArea.Advertencias, AuditoriaAcao.Alterou, "Contestou a advertência de 05/10/2026.", $"Advertência de {nome}"), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado));
        Assert.DoesNotContain("privado", JsonSerializer.Serialize(evento), StringComparison.Ordinal);
        Assert.Equal(("situacao", "PENDENTE", "CONTESTADA"), (evento.Mudancas[0].Campo, evento.Mudancas[0].ValorAntes, evento.Mudancas[0].ValorDepois));
        Assert.Equal(emailsAntes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS")); // não há e-mail ao RH na contestação
    }

    [Fact]
    public async Task Contestar_de_novo_ou_depois_da_ciencia_e_422_e_a_primeira_contestacao_nunca_e_sobrescrita()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _, cliente) = await PessoaAsync(api, setor);
        var contestada = await SemearAsync(api, pessoa, "2026-10-01");
        var ciente = await SemearAsync(api, pessoa, "2026-10-02");

        var primeira = await ContestarAsync(cliente, contestada, "Primeira contestação válida.");
        var segunda = await ContestarAsync(cliente, contestada, "Segunda tentativa que não vale.");
        await CienciaAsync(cliente, ciente);
        var depoisDaCiencia = await ContestarAsync(cliente, ciente, TextoOk);

        Assert.Equal((200, 422, 422), ((int)primeira.StatusCode, (int)segunda.StatusCode, (int)depoisDaCiencia.StatusCode));
        Assert.Equal("ADVERTENCIA_NAO_ESTA_PENDENTE", (await CorpoAsync(segunda)).GetProperty("codigo").GetString());
        Assert.Equal("Primeira contestação válida.", await TextoAsync(api, $"SELECT TEXTO_CONTESTACAO FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {contestada}"));
        Assert.Null(await TextoAsync(api, $"SELECT TEXTO_CONTESTACAO FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {ciente}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("         ")]
    [InlineData("curto 9ch")]
    public async Task Texto_curto_demais_e_400_com_o_erro_no_campo_texto(string texto)
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _, cliente) = await PessoaAsync(api, setor);
        var id = await SemearAsync(api, pessoa, "2026-10-01");

        var resposta = await ContestarAsync(cliente, id, texto);

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty("texto", out _));
        Assert.Equal("PENDENTE", await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
    }

    [Fact]
    public async Task O_texto_aceita_10_e_2000_caracteres_e_recusa_2001()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _, cliente) = await PessoaAsync(api, setor);
        var a = await SemearAsync(api, pessoa, "2026-10-01");
        var b = await SemearAsync(api, pessoa, "2026-10-02");
        var c = await SemearAsync(api, pessoa, "2026-10-03");

        var longa = await ContestarAsync(cliente, a, new string('t', 2001));
        var minimo = await ContestarAsync(cliente, b, new string('t', 10));
        var maximo = await ContestarAsync(cliente, c, new string('t', 2000));

        Assert.Equal((400, 200, 200), ((int)longa.StatusCode, (int)minimo.StatusCode, (int)maximo.StatusCode));
    }

    [Fact]
    public async Task Ciencia_e_contestacao_ao_mesmo_tempo_tem_um_efeito_so_e_a_outra_e_422()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _, cliente) = await PessoaAsync(api, setor);
        var id = await SemearAsync(api, pessoa, "2026-10-01");

        var respostas = await Task.WhenAll(CienciaAsync(cliente, id), ContestarAsync(cliente, id, TextoOk));

        Assert.Equal([200, 422], respostas.Select(r => (int)r.StatusCode).Order().ToArray());
        Assert.Contains(await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"), new[] { "APLICADA", "CONTESTADA" });
        Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
    }

    // ---------------------------------------------------------------- o contrato

    [Fact]
    public async Task As_tres_rotas_respondem_como_o_contrato_manda()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _, cliente) = await PessoaAsync(api, setor);
        var a = await SemearAsync(api, pessoa, "2026-10-01");
        var b = await SemearAsync(api, pessoa, "2026-10-02");

        await (await cliente.GetAsync(Rota)).AfirmarConformeAoContratoAsync("listarMinhasAdvertencias");
        await (await cliente.GetAsync($"{Rota}?situacao=ABC")).AfirmarConformeAoContratoAsync("listarMinhasAdvertencias"); // 400
        await (await CienciaAsync(cliente, a)).AfirmarConformeAoContratoAsync("confirmarCienciaDeAdvertencia");
        await (await CienciaAsync(cliente, a)).AfirmarConformeAoContratoAsync("confirmarCienciaDeAdvertencia"); // 422
        await (await CienciaAsync(cliente, int.MaxValue)).AfirmarConformeAoContratoAsync("confirmarCienciaDeAdvertencia"); // 404
        await (await ContestarAsync(cliente, b, TextoOk)).AfirmarConformeAoContratoAsync("contestarAdvertencia");
        await (await ContestarAsync(cliente, b, TextoOk)).AfirmarConformeAoContratoAsync("contestarAdvertencia"); // 422
        await (await ContestarAsync(cliente, b, "curto")).AfirmarConformeAoContratoAsync("contestarAdvertencia"); // 400
        await (await ContestarAsync(cliente, int.MaxValue, TextoOk)).AfirmarConformeAoContratoAsync("contestarAdvertencia"); // 404
    }
}
