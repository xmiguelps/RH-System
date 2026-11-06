using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Matriz;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Equipe;

/// <summary>
/// <c>GET /api/gestor/equipe/{id}</c> e <c>.../indicadores</c>: a ficha do gestor é um DTO PRÓPRIO (sem CPF, RG, e-mail pessoal, endereço, matrícula, benefícios nem o telefone inteiro) e tudo o que está fora da equipe
/// dá 404 (nunca 403 nem 200 vazio), o mesmo 404 de um id que não existe.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FichaDaEquipeTests(MySqlFixture mysql)
{
    private const string TelefoneDoMembroA = "11987657783";
    private static int _cpf = 700_000;
    private static int _cargo;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(FichaDaEquipeTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static Task DarTelefoneAsync(ApiDeAcesso api, int funcionarioId, string telefone) =>
        Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO FUNCIONARIOS_DETALHES (ID_FUNCIONARIO, CPF, TELEFONE) VALUES ({funcionarioId}, '{CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf))}', '{telefone}') ON DUPLICATE KEY UPDATE TELEFONE = VALUES(TELEFONE)");

    private static Task<HttpResponseMessage> FichaAsync(HttpClient cliente, int id) => cliente.GetAsync($"/api/gestor/equipe/{id}");

    private static Task<HttpResponseMessage> IndicadoresAsync(HttpClient cliente, int id) => cliente.GetAsync($"/api/gestor/equipe/{id}/indicadores");

    [Fact]
    public async Task Ficha_da_equipe_traz_so_os_campos_do_gestor_e_o_telefone_mascarado()
    {
        var api = await ApiAsync();
        await DarTelefoneAsync(api, api.Elenco.MembroA.FuncionarioId, TelefoneDoMembroA);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo da ficha {Interlocked.Increment(ref _cargo)}", 36m);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo}, TIPO_CONTRATO = 'ESTAGIO' WHERE ID_FUNCIONARIO = {api.Elenco.MembroA.FuncionarioId}");

        var resposta = await FichaAsync(api.Api.ClienteComo(api.Elenco.Gestor), api.Elenco.MembroA.FuncionarioId);

        Assert.Equal(200, (int)resposta.StatusCode);
        var ficha = await CorpoAsync(resposta);
        Assert.Equal(
            ["ativo", "cargo", "dataAdmissao", "emailCorporativo", "id", "jornadaSemanalHoras", "nome", "setor", "situacaoHoje", "telefoneMascarado", "tipoContrato"],
            ficha.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)); // exatamente os campos do gestor
        Assert.Equal("(11) 9****-7783", ficha.GetProperty("telefoneMascarado").GetString());
        Assert.Equal((api.Elenco.MembroA.FuncionarioId, api.Elenco.MembroA.Nome, true, "AINDA_NAO_COMECOU"), (ficha.GetProperty("id").GetInt32(), ficha.GetProperty("nome").GetString(), ficha.GetProperty("ativo").GetBoolean(), ficha.GetProperty("situacaoHoje").GetString()));
        Assert.Equal("Comercial", ficha.GetProperty("setor").GetString());
        Assert.Equal("2026-01-02", ficha.GetProperty("dataAdmissao").GetString());
        Assert.Equal(($"Cargo da ficha {_cargo}", "ESTAGIO", 36m), (ficha.GetProperty("cargo").GetString(), ficha.GetProperty("tipoContrato").GetString(), ficha.GetProperty("jornadaSemanalHoras").GetDecimal()));
        Assert.Equal("diego.farias@empresa.com.br", ficha.GetProperty("emailCorporativo").GetString());
        await resposta.AfirmarConformeAoContratoAsync("obterFichaDaEquipe");
    }

    [Fact]
    public async Task Ficha_da_equipe_nao_tem_nenhum_campo_do_RH_em_nenhuma_profundidade()
    {
        var api = await ApiAsync();
        await DarTelefoneAsync(api, api.Elenco.MembroA.FuncionarioId, TelefoneDoMembroA);
        var gestor = api.Api.ClienteComo(api.Elenco.Gestor);
        var id = api.Elenco.MembroA.FuncionarioId;

        // Cada resposta tem de ser a 200 de verdade: varrer o corpo de um 404 não provaria nada.
        foreach (var resposta in new[] { await gestor.GetAsync("/api/gestor/equipe"), await FichaAsync(gestor, id), await IndicadoresAsync(gestor, id) })
        {
            Assert.Equal(200, (int)resposta.StatusCode);
            CamposDoRh.AfirmarSemCamposDoRh(await CorpoAsync(resposta));
        }
    }

    [Fact]
    public async Task RH_ve_o_telefone_completo_e_o_gestor_ve_o_mascarado_da_mesma_pessoa()
    {
        var api = await ApiAsync();
        var id = api.Elenco.MembroA.FuncionarioId;
        await DarTelefoneAsync(api, id, TelefoneDoMembroA);

        var doRh = await CorpoAsync(await api.Api.ClienteComo(api.Elenco.Rh).GetAsync($"/api/rh/funcionarios/{id}"));
        var doGestor = await CorpoAsync(await FichaAsync(api.Api.ClienteComo(api.Elenco.Gestor), id));

        Assert.Equal(TelefoneDoMembroA, doRh.GetProperty("telefone").GetString()); // o RH: o número inteiro, sem máscara
        Assert.Equal("(11) 9****-7783", doGestor.GetProperty("telefoneMascarado").GetString()); // o gestor: DDD, primeiro dígito e os quatro últimos
        Assert.False(doGestor.TryGetProperty("telefone", out _));
    }

    [Fact]
    public async Task Pessoa_de_outro_setor_o_proprio_gestor_quem_esta_em_admissao_e_quem_esta_inativo_devolvem_404_na_ficha_e_nos_indicadores()
    {
        var api = await ApiAsync();
        var gestor = api.Api.ClienteComo(api.Elenco.Gestor);
        Assert.Equal(200, (int)(await FichaAsync(gestor, api.Elenco.MembroA.FuncionarioId)).StatusCode); // o controle: quem é da equipe existe
        Assert.Equal(200, (int)(await IndicadoresAsync(gestor, api.Elenco.MembroA.FuncionarioId)).StatusCode);
        var foraDaEquipe = new[] { api.Elenco.DeOutroSetor.FuncionarioId, api.Elenco.Gestor.FuncionarioId, api.Elenco.EmAdmissao.FuncionarioId, api.Elenco.Inativo.FuncionarioId };

        foreach (var id in foraDaEquipe)
        {
            Assert.Equal(404, (int)(await FichaAsync(gestor, id)).StatusCode); // nunca 403 nem 200 vazio
            Assert.Equal(404, (int)(await IndicadoresAsync(gestor, id)).StatusCode);
        }

        // O gestor sem setor não tem equipe: qualquer id é 404 (e a lista dele é a vazia, não erro).
        var semSetor = api.Api.ClienteComo(api.Elenco.GestorSemSetor);
        Assert.Equal(404, (int)(await FichaAsync(semSetor, api.Elenco.MembroA.FuncionarioId)).StatusCode);
        Assert.Equal(404, (int)(await IndicadoresAsync(semSetor, api.Elenco.MembroA.FuncionarioId)).StatusCode);
    }

    [Fact]
    public async Task Id_inexistente_devolve_exatamente_o_mesmo_404_de_fora_do_escopo()
    {
        var api = await ApiAsync();
        var gestor = api.Api.ClienteComo(api.Elenco.Gestor);

        foreach (var rota in new Func<HttpClient, int, Task<HttpResponseMessage>>[] { FichaAsync, IndicadoresAsync })
        {
            Assert.Equal(200, (int)(await rota(gestor, api.Elenco.MembroA.FuncionarioId)).StatusCode); // o controle: a rota existe e quem é da equipe responde
            var inexistente = await rota(gestor, 987_654);
            var fora = await rota(gestor, api.Elenco.DeOutroSetor.FuncionarioId);

            Assert.Equal(404, (int)inexistente.StatusCode);
            Assert.Equal(await SemTrace(inexistente), await SemTrace(fora)); // quem pede não descobre se o id existe
        }
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var gestor = api.Api.ClienteComo(api.Elenco.Gestor);
        var id = api.Elenco.MembroA.FuncionarioId;

        await (await FichaAsync(gestor, id)).AfirmarConformeAoContratoAsync("obterFichaDaEquipe"); // o 200
        await (await FichaAsync(gestor, api.Elenco.DeOutroSetor.FuncionarioId)).AfirmarConformeAoContratoAsync("obterFichaDaEquipe"); // o 404
        await (await FichaAsync(api.Api.ClienteComo(api.Elenco.Rh), id)).AfirmarConformeAoContratoAsync("obterFichaDaEquipe"); // o 403
        await (await FichaAsync(api.Api.ClienteAnonimo(), id)).AfirmarConformeAoContratoAsync("obterFichaDaEquipe"); // o 401
    }

    /// <summary>O corpo do problema sem o <c>traceId</c> (que muda a cada resposta).</summary>
    private static async Task<string> SemTrace(HttpResponseMessage resposta)
    {
        var corpo = await CorpoAsync(resposta);
        return string.Join("|", corpo.EnumerateObject().Where(p => p.Name != "traceId").OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => $"{p.Name}={p.Value.GetRawText()}"));
    }
}
