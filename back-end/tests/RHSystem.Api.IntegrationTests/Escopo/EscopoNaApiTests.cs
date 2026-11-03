using System.Net;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Autorizacao.ApiDeAutorizacao;

namespace RHSystem.Api.IntegrationTests.Escopo;

/// <summary>
/// O padrão que as fatias seguintes copiam: a consulta do gestor parte de <c>DaEquipe</c> e termina em <c>OuNaoEncontrado</c>, então "não existe" e
/// "é de outro setor" dão a mesma resposta (rotas de teste <c>/api/gestor/__teste/equipe</c> e <c>/api/gestor/__teste/equipe/{id}</c>).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EscopoNaApiTests(MySqlFixture mysql)
{
    private const string TituloNaoEncontrado = "Não encontrado.";
    private const string DetalheNaoEncontrado = "O registro não foi encontrado.";

    [Fact]
    public async Task Gestor_lista_so_a_propria_equipe()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        var resposta = await api.ClienteComo(elenco.Gestor).GetAsync("/api/gestor/__teste/equipe");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var itens = (await LerAsync(resposta)).EnumerateArray().ToList();
        Assert.Equal(["Diego Farias", "Júlia Nunes"], itens.Select(i => i.GetProperty("nome").GetString()));
        Assert.Equal([elenco.MembroA.FuncionarioId, elenco.MembroB.FuncionarioId], itens.Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Gestor_pede_pessoa_da_equipe_e_recebe_200()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        var resposta = await api.ClienteComo(elenco.Gestor).GetAsync($"/api/gestor/__teste/equipe/{elenco.MembroA.FuncionarioId}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Diego Farias", (await LerAsync(resposta)).GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Gestor_pede_pessoa_de_outro_setor_recebe_404_problem_json_do_contrato()
    {
        // Review Focus 1.
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        var resposta = await api.ClienteComo(elenco.Gestor).GetAsync($"/api/gestor/__teste/equipe/{elenco.DeOutroSetor.FuncionarioId}");

        await AssertProblemaAsync(resposta, 404, TituloNaoEncontrado, DetalheNaoEncontrado);
    }

    [Fact]
    public async Task Gestor_pede_a_si_mesmo_quem_esta_em_admissao_e_quem_esta_inativo_e_recebe_404()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);
        var cliente = api.ClienteComo(elenco.Gestor);

        foreach (var id in new[] { elenco.Gestor.FuncionarioId, elenco.EmAdmissao.FuncionarioId, elenco.Inativo.FuncionarioId })
        {
            await AssertProblemaAsync(await cliente.GetAsync($"/api/gestor/__teste/equipe/{id}"), 404, TituloNaoEncontrado, DetalheNaoEncontrado);
        }
    }

    [Fact]
    public async Task Gestor_pede_id_inexistente_recebe_exatamente_o_mesmo_404_de_fora_do_escopo()
    {
        // Não revela existência: o corpo (menos o traceId) de "não existe" e de "existe, mas é de outro setor" é idêntico.
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);
        var cliente = api.ClienteComo(elenco.Gestor);

        var inexistente = await cliente.GetAsync("/api/gestor/__teste/equipe/999999");
        var foraDoEscopo = await cliente.GetAsync($"/api/gestor/__teste/equipe/{elenco.DeOutroSetor.FuncionarioId}");

        Assert.Equal(await SemTraceIdAsync(foraDoEscopo), await SemTraceIdAsync(inexistente));
        await AssertProblemaAsync(inexistente, 404, TituloNaoEncontrado, DetalheNaoEncontrado);
    }

    [Fact]
    public async Task Gestor_sem_setor_lista_equipe_vazia_e_recebe_404_ao_pedir_alguem()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);
        var cliente = api.ClienteComo(elenco.GestorSemSetor);

        var lista = await cliente.GetAsync("/api/gestor/__teste/equipe");

        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        Assert.Empty((await LerAsync(lista)).EnumerateArray());
        await AssertProblemaAsync(await cliente.GetAsync($"/api/gestor/__teste/equipe/{elenco.MembroA.FuncionarioId}"), 404, TituloNaoEncontrado, DetalheNaoEncontrado);
    }

    [Fact]
    public async Task Rh_na_rota_do_gestor_recebe_403_e_nao_404()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        var resposta = await api.ClienteComo(elenco.Rh).GetAsync($"/api/gestor/__teste/equipe/{elenco.MembroA.FuncionarioId}");

        await AssertProblemaAsync(resposta, 403, "Sem permissão.", "Você não tem permissão para esta ação.");
    }

    private static async Task<string> SemTraceIdAsync(HttpResponseMessage resposta)
    {
        var corpo = await LerAsync(resposta);
        var campos = corpo.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}");
        return string.Join("|", campos);
    }
}
