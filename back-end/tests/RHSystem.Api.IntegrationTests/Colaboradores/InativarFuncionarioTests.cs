using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Colaboradores.DesligamentoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary><c>POST</c> e <c>DELETE /api/rh/funcionarios/{id}/desligamento</c> (tela 03, "Inativar") pela API de verdade, no banco REAL com o relógio em 2026-10-07T12:42Z.</summary>
[Collection(MySqlCollection.Nome)]
public class InativarFuncionarioTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ComecarAsync(mysql, nameof(InativarFuncionarioTests));

    private static string Rota(int id) => $"/api/rh/funcionarios/{id}/desligamento";

    private static Task<PessoaDoTeste> PessoaAsync(ApiDeAcesso api, string sufixo) => api.NovaPessoaAsync(sufixo, login: TipoDeLogin.SenhaDefinida);

    private static Task<HttpResponseMessage> InativarAsync(ApiDeAcesso api, int id, string ultimoDia) =>
        api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota(id), new { ultimoDiaDeTrabalho = ultimoDia });

    private static Task<HttpResponseMessage> CancelarAsync(ApiDeAcesso api, int id) => api.Api.ClienteComo(api.Elenco.Rh).DeleteAsync(Rota(id));

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta) => (await resposta.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    [Fact]
    public async Task Inativar_devolve_200_com_a_ficha_e_desligamento_AGENDADO_ou_EFETIVADO()
    {
        var api = await ApiAsync();
        var futura = await PessoaAsync(api, "inativar1");
        var passada = await PessoaAsync(api, "inativar2");

        var agendada = await InativarAsync(api, futura.FuncionarioId, "2026-11-13");
        var efetivada = await InativarAsync(api, passada.FuncionarioId, "2026-10-05");

        Assert.Equal(200, (int)agendada.StatusCode);
        var fichaAgendada = await LerAsync(agendada);
        Assert.Equal((futura.FuncionarioId, true), (fichaAgendada.GetProperty("id").GetInt32(), fichaAgendada.GetProperty("ativo").GetBoolean())); // ainda ativa até o dia passar
        Assert.Equal(("2026-11-13", "AGENDADO"), (fichaAgendada.GetProperty("desligamento").GetProperty("ultimoDiaDeTrabalho").GetString(), fichaAgendada.GetProperty("desligamento").GetProperty("situacao").GetString()));

        Assert.Equal(200, (int)efetivada.StatusCode);
        var fichaEfetivada = await LerAsync(efetivada);
        Assert.False(fichaEfetivada.GetProperty("ativo").GetBoolean());
        Assert.Equal(("2026-10-05", "EFETIVADO"), (fichaEfetivada.GetProperty("desligamento").GetProperty("ultimoDiaDeTrabalho").GetString(), fichaEfetivada.GetProperty("desligamento").GetProperty("situacao").GetString()));
    }

    [Fact]
    public async Task Cancelar_devolve_204_e_depois_a_ficha_volta_a_desligamento_null()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "inativar3");
        await InativarAsync(api, pessoa.FuncionarioId, "2026-11-13");

        var cancelado = await CancelarAsync(api, pessoa.FuncionarioId);

        Assert.Equal(204, (int)cancelado.StatusCode);
        var ficha = await LerAsync(await api.Api.ClienteComo(api.Elenco.Rh).GetAsync($"/api/rh/funcionarios/{pessoa.FuncionarioId}"));
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("desligamento").ValueKind);
        Assert.True(ficha.GetProperty("ativo").GetBoolean());

        var denovo = await CancelarAsync(api, pessoa.FuncionarioId); // já não há o que cancelar
        Assert.Equal(422, (int)denovo.StatusCode);
        Assert.Equal("SEM_DESLIGAMENTO_AGENDADO", (await LerAsync(denovo)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Inativar_de_quem_ja_tem_desligamento_devolve_409_com_o_codigo_e_a_data_antes_da_admissao_devolve_400_por_campo()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "inativar4"); // admitida em 2026-01-02
        await InativarAsync(api, pessoa.FuncionarioId, "2026-11-13");

        var conflito = await InativarAsync(api, pessoa.FuncionarioId, "2026-12-01");
        Assert.Equal(409, (int)conflito.StatusCode);
        Assert.Equal("DESLIGAMENTO_JA_REGISTRADO", (await LerAsync(conflito)).GetProperty("codigo").GetString());

        var outra = await PessoaAsync(api, "inativar5");
        var antes = await InativarAsync(api, outra.FuncionarioId, "2026-01-01");
        Assert.Equal(400, (int)antes.StatusCode);
        Assert.True((await LerAsync(antes)).GetProperty("errors").TryGetProperty("ultimoDiaDeTrabalho", out _));

        // Corpo sem o campo: o System.Text.Json deixa o DateOnly no padrão (0001-01-01), que é anterior à admissão: 400 no mesmo campo, nunca um desligamento.
        var semCampo = await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync(Rota(outra.FuncionarioId), new { });
        Assert.Equal(400, (int)semCampo.StatusCode);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {outra.FuncionarioId}"));
    }

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_nas_duas_rotas_anonimo_401_e_id_inexistente_404()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "inativar6");
        var corpo = new { ultimoDiaDeTrabalho = "2026-11-13" };

        foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).PostAsJsonAsync(Rota(pessoa.FuncionarioId), corpo)).StatusCode);
            Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).DeleteAsync(Rota(pessoa.FuncionarioId))).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().PostAsJsonAsync(Rota(pessoa.FuncionarioId), corpo)).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().DeleteAsync(Rota(pessoa.FuncionarioId))).StatusCode);
        Assert.Equal(404, (int)(await InativarAsync(api, 987_654, "2026-11-13")).StatusCode);
        Assert.Equal(404, (int)(await CancelarAsync(api, 987_654)).StatusCode);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}")); // nenhuma das recusadas gravou
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var futura = await PessoaAsync(api, "inativar7");
        var passada = await PessoaAsync(api, "inativar8");
        var outra = await PessoaAsync(api, "inativar9");

        await (await InativarAsync(api, futura.FuncionarioId, "2026-11-13")).AfirmarConformeAoContratoAsync("inativarFuncionario"); // o 200 AGENDADO
        await (await InativarAsync(api, passada.FuncionarioId, "2026-10-05")).AfirmarConformeAoContratoAsync("inativarFuncionario"); // o 200 EFETIVADO
        await (await InativarAsync(api, outra.FuncionarioId, "2026-01-01")).AfirmarConformeAoContratoAsync("inativarFuncionario"); // o 400
        await (await InativarAsync(api, futura.FuncionarioId, "2026-12-01")).AfirmarConformeAoContratoAsync("inativarFuncionario"); // o 409
        await (await InativarAsync(api, 987_654, "2026-11-13")).AfirmarConformeAoContratoAsync("inativarFuncionario"); // o 404
        await (await CancelarAsync(api, futura.FuncionarioId)).AfirmarConformeAoContratoAsync("cancelarDesligamentoAgendado"); // o 204
        await (await CancelarAsync(api, futura.FuncionarioId)).AfirmarConformeAoContratoAsync("cancelarDesligamentoAgendado"); // o 422
        await (await CancelarAsync(api, 987_654)).AfirmarConformeAoContratoAsync("cancelarDesligamentoAgendado"); // o 404
    }
}
