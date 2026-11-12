using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// <c>GET /api/gestor/ponto/atestados</c> e <c>GET /api/gestor/ponto/atestados/{id}/download</c>, no banco REAL. O gestor <b>vê</b> os atestados da equipe dele (Diego e Júlia de Carlos; sem o próprio gestor, sem quem é de outro setor, sem o inativo e sem quem ainda não entrou) e baixa o arquivo; validar e recusar
/// são só do RH. Atestado de fora da equipe dá o mesmo 404 do inexistente; pessoa de outra equipe no filtro devolve a lista vazia.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AtestadosDaEquipeTests(MySqlFixture mysql)
{
    private const string Rota = "/api/gestor/ponto/atestados";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AtestadosDaEquipeTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> ComoAsync(ApiDeAcesso api, PessoaDeTeste pessoa, string caminho) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(caminho);

    private static List<int> Ids(JsonElement pagina) => [.. pagina.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("id").GetInt32())];

    private static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int atestadoId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS", atestadoId);

    private static async Task<string> CaminhoAsync(ApiDeAcesso api, int atestadoId) => (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM ATESTADOS WHERE ID_ATESTADO = {atestadoId}"))!;

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_e_so_da_equipe_do_gestor_do_mais_recente_ao_mais_antigo()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var rh = e.Rh.FuncionarioId;
        var diego = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-09-29", "2026-09-30", "VALIDADO", rh, "2026-09-29 12:10:00", "atestado-diego.pdf");
        var julia = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroB.FuncionarioId, "2026-10-05", "2026-10-05", enviadoEm: "2026-10-05 14:00:00");
        var doGestor = await AtestadosDeTeste.SemearAsync(api.Conexao, e.Gestor.FuncionarioId, "2026-10-01", "2026-10-01");
        var deBruno = await AtestadosDeTeste.SemearAsync(api.Conexao, e.DeOutroSetor.FuncionarioId, "2026-10-01", "2026-10-01");
        var deHenrique = await AtestadosDeTeste.SemearAsync(api.Conexao, e.Inativo.FuncionarioId, "2026-10-01", "2026-10-01");
        var deRafael = await AtestadosDeTeste.SemearAsync(api.Conexao, e.EmAdmissao.FuncionarioId, "2026-10-01", "2026-10-01");

        var pagina = await CorpoAsync(await ComoAsync(api, e.Gestor, $"{Rota}?tamanhoPagina=100"));

        Assert.Equal([julia, diego], Ids(pagina).Where(id => new[] { diego, julia, doGestor, deBruno, deHenrique, deRafael }.Contains(id))); // Diego e Júlia, e só eles
        Assert.All(pagina.GetProperty("itens").EnumerateArray(), a => Assert.Contains(a.GetProperty("funcionario").GetProperty("id").GetInt32(), new[] { e.MembroA.FuncionarioId, e.MembroB.FuncionarioId }));
        Assert.DoesNotContain(doGestor, Ids(pagina));
        Assert.DoesNotContain(deBruno, Ids(pagina));
        Assert.DoesNotContain(deHenrique, Ids(pagina));
        Assert.DoesNotContain(deRafael, Ids(pagina));
        var item = pagina.GetProperty("itens").EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == diego);
        Assert.Equal(("Diego Farias", "2026-09-29", "2026-09-30", "VALIDADO", "atestado-diego.pdf", "2026-09-29T15:10:00Z", "2026-10-06T13:00:00Z", "Ana Souza"),
            (item.GetProperty("funcionario").GetProperty("nome").GetString(), item.GetProperty("dataInicio").GetString(), item.GetProperty("dataFim").GetString(), item.GetProperty("situacao").GetString(), item.GetProperty("nomeArquivo").GetString(), item.GetProperty("enviadoEmUtc").GetString(), item.GetProperty("decididoEmUtc").GetString(), item.GetProperty("decididoPorNome").GetString()));
    }

    [Fact]
    public async Task O_filtro_por_pessoa_e_por_situacao_vale_dentro_da_equipe_e_pessoa_de_outra_equipe_devolve_lista_vazia()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var diego = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-08-10", "2026-08-10", enviadoEm: "2026-08-10 09:00:00");
        var julia = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroB.FuncionarioId, "2026-08-11", "2026-08-11", "RECUSADO", e.Rh.FuncionarioId, "2026-08-11 09:00:00");
        await AtestadosDeTeste.SemearAsync(api.Conexao, e.DeOutroSetor.FuncionarioId, "2026-08-12", "2026-08-12", enviadoEm: "2026-08-12 09:00:00");

        var deDiego = await CorpoAsync(await ComoAsync(api, e.Gestor, $"{Rota}?funcionarioId={e.MembroA.FuncionarioId}"));
        var recusados = await CorpoAsync(await ComoAsync(api, e.Gestor, $"{Rota}?situacao=RECUSADO"));
        var deBruno = await ComoAsync(api, e.Gestor, $"{Rota}?funcionarioId={e.DeOutroSetor.FuncionarioId}");
        var doProprioGestor = await ComoAsync(api, e.Gestor, $"{Rota}?funcionarioId={e.Gestor.FuncionarioId}");

        Assert.Contains(diego, Ids(deDiego));
        Assert.DoesNotContain(julia, Ids(deDiego));
        Assert.All(deDiego.GetProperty("itens").EnumerateArray(), a => Assert.Equal(e.MembroA.FuncionarioId, a.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Contains(julia, Ids(recusados));
        Assert.DoesNotContain(diego, Ids(recusados));
        Assert.Equal(200, (int)deBruno.StatusCode);
        Assert.Equal(0, (await CorpoAsync(deBruno)).GetProperty("itens").GetArrayLength());
        Assert.Equal(0, (await CorpoAsync(doProprioGestor)).GetProperty("itens").GetArrayLength());
    }

    [Fact]
    public async Task Gestor_sem_setor_recebe_a_lista_vazia_sem_erro()
    {
        var api = await ApiAsync();

        var resposta = await ComoAsync(api, api.Elenco.GestorSemSetor, Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        var pagina = await CorpoAsync(resposta);
        Assert.Equal((0, 0, 0), (pagina.GetProperty("itens").GetArrayLength(), pagina.GetProperty("total").GetInt32(), pagina.GetProperty("totalPaginas").GetInt32()));
    }

    [Theory]
    [InlineData("?situacao=PENDENTE", "situacao")]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?tamanhoPagina=101", "tamanhoPagina")]
    public async Task Filtro_invalido_e_400_com_o_nome_do_campo(string consulta, string campo)
    {
        var api = await ApiAsync();

        var resposta = await ComoAsync(api, api.Elenco.Gestor, $"{Rota}{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    // ---------------------------------------------------------------- o download

    [Fact]
    public async Task Baixar_atestado_da_equipe_devolve_o_link_e_grava_Baixou_antes_sem_o_nome_do_arquivo()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-10-01", "2026-10-02", nome: "exame de sangue.pdf");
        var caminho = await CaminhoAsync(api, id);

        var resposta = await ComoAsync(api, e.Gestor, $"{Rota}/{id}/download");

        Assert.Equal(200, (int)resposta.StatusCode);
        var link = await CorpoAsync(resposta);
        Assert.Equal(($"https://arquivos.exemplo.test/{caminho}?nome=exame%20de%20sangue.pdf", "2026-10-07T12:47:00Z"), (link.GetProperty("url").GetString(), link.GetProperty("expiraEmUtc").GetString()));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Atestados, AuditoriaAcao.Baixou, "Baixou o atestado de Diego Farias (01/10/2026 a 02/10/2026).", "Atestado de Diego Farias", e.Gestor.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
        Assert.DoesNotContain("exame de sangue", evento.Descricao + evento.RegistroAfetado, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Atestado_de_fora_da_equipe_do_proprio_gestor_ou_inexistente_e_o_mesmo_404_e_nao_grava_evento()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var deBruno = await AtestadosDeTeste.SemearAsync(api.Conexao, e.DeOutroSetor.FuncionarioId, "2026-10-01", "2026-10-01");
        var doGestor = await AtestadosDeTeste.SemearAsync(api.Conexao, e.Gestor.FuncionarioId, "2026-10-01", "2026-10-01");
        var deHenrique = await AtestadosDeTeste.SemearAsync(api.Conexao, e.Inativo.FuncionarioId, "2026-10-01", "2026-10-01");
        var daEquipe = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-10-01", "2026-10-01");
        var inexistente = await ComoAsync(api, e.Gestor, $"{Rota}/{int.MaxValue}/download");

        Assert.Equal(200, (int)(await ComoAsync(api, e.Gestor, $"{Rota}/{daEquipe}/download")).StatusCode); // o controle: o da equipe sai, os de fora não (sem ele, o 404 de uma rota que não existe passaria)
        foreach (var id in new[] { deBruno, doGestor, deHenrique })
        {
            var resposta = await ComoAsync(api, e.Gestor, $"{Rota}/{id}/download");
            Assert.Equal(404, (int)resposta.StatusCode);
            var (a, b) = (await CorpoAsync(resposta), await CorpoAsync(inexistente));
            Assert.Equal((b.GetProperty("title").GetString(), b.GetProperty("detail").GetString()), (a.GetProperty("title").GetString(), a.GetProperty("detail").GetString())); // nada diz que o atestado existe
            Assert.Empty(await EventosAsync(api, id));
        }
    }

    [Fact]
    public async Task Falha_ao_gravar_o_evento_de_download_nao_devolve_link()
    {
        var api = await ApiAsync();
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, "2026-10-01", "2026-10-01");
        var fabrica = AtestadosDeTeste.ComAuditorQueFalhaNoDownload(api.Api);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).DefaultRequestHeaders.Authorization;

        var resposta = await cliente.GetAsync($"{Rota}/{id}/download");

        Assert.Equal(500, (int)resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("arquivos.exemplo.test", texto, StringComparison.Ordinal); // sem registro, sem link
        Assert.Empty(await EventosAsync(api, id));
    }

    // ---------------------------------------------------------------- perfis e contrato

    [Fact]
    public async Task So_o_gestor_entra_nas_duas_rotas_e_as_respostas_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-09-01", "2026-09-01", "RECUSADO", e.Rh.FuncionarioId, "2026-09-01 09:00:00");

        foreach (var intruso in new[] { e.MembroA, e.Rh })
        {
            Assert.Equal((403, 403), ((int)(await ComoAsync(api, intruso, Rota)).StatusCode, (int)(await ComoAsync(api, intruso, $"{Rota}/{id}/download")).StatusCode));
        }

        Assert.Equal((401, 401), ((int)(await api.Api.ClienteAnonimo().GetAsync(Rota)).StatusCode, (int)(await api.Api.ClienteAnonimo().GetAsync($"{Rota}/{id}/download")).StatusCode));
        await (await ComoAsync(api, e.Gestor, Rota)).AfirmarConformeAoContratoAsync("listarAtestadosDaEquipe");
        await (await ComoAsync(api, e.Gestor, $"{Rota}?situacao=NADA")).AfirmarConformeAoContratoAsync("listarAtestadosDaEquipe"); // 400
        await (await ComoAsync(api, e.Gestor, $"{Rota}/{id}/download")).AfirmarConformeAoContratoAsync("baixarAtestadoDaEquipe");
        await (await ComoAsync(api, e.Gestor, $"{Rota}/{int.MaxValue}/download")).AfirmarConformeAoContratoAsync("baixarAtestadoDaEquipe"); // 404
    }
}
