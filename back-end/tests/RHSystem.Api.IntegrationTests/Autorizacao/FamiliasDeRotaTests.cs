using System.Net;
using System.Net.Http.Json;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Autorizacao.ApiDeAutorizacao;

namespace RHSystem.Api.IntegrationTests.Autorizacao;

/// <summary>
/// As quatro famílias de rota (<c>/api/rh</c>, <c>/api/gestor</c>, <c>/api/eu</c>, <c>/api/publico</c>) e a regra de reserva: rota sem política exige login.
/// Cada família tem uma rota de teste <c>/__teste/ping</c> (e há uma fora de qualquer família).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FamiliasDeRotaTests(MySqlFixture mysql)
{
    private const string TituloSemPermissao = "Sem permissão.";
    private const string DetalheSemPermissao = "Você não tem permissão para esta ação.";

    [Fact]
    public async Task Anonimo_em_rota_de_cada_familia_recebe_401_problem_json_e_o_publico_passa()
    {
        var cliente = (await ObterAsync(mysql)).CreateClient();

        foreach (var rota in new[] { "/api/rh/__teste/ping", "/api/gestor/__teste/ping", "/api/eu/__teste/ping" })
        {
            await AssertProblemaAsync(await GetAsync(cliente, rota), 401, "Não autenticado.", "Entre novamente para continuar.");
        }

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, "/api/publico/__teste/ping")).StatusCode);
    }

    [Fact]
    public async Task Funcionario_em_rota_do_rh_recebe_403_problem_json_com_title_e_detail_do_contrato()
    {
        var api = await ObterAsync(mysql);
        var token = await TokenAsync(api, ApiFactory.EmailComCargo); // perfil Funcionario
        var cliente = api.CreateClient();

        await AssertProblemaAsync(await GetAsync(cliente, "/api/rh/__teste/ping", token), 403, TituloSemPermissao, DetalheSemPermissao);
        await AssertProblemaAsync(await GetAsync(cliente, "/api/gestor/__teste/ping", token), 403, TituloSemPermissao, DetalheSemPermissao);
    }

    [Fact]
    public async Task Gestor_em_rota_do_rh_e_Rh_em_rota_do_gestor_recebem_403()
    {
        var api = await ObterAsync(mysql);
        var cliente = api.CreateClient();
        var gestor = await TokenAsync(api, EmailGestor);
        var rh = await TokenAsync(api, ApiFactory.EmailRh);

        await AssertProblemaAsync(await GetAsync(cliente, "/api/rh/__teste/ping", gestor), 403, TituloSemPermissao, DetalheSemPermissao);
        await AssertProblemaAsync(await GetAsync(cliente, "/api/gestor/__teste/ping", rh), 403, TituloSemPermissao, DetalheSemPermissao);
    }

    [Fact]
    public async Task Cada_perfil_passa_na_sua_familia()
    {
        // Controle positivo dos 403 acima: o perfil certo passa (senão os 403 poderiam ser só "a rota nunca deixa ninguém entrar").
        var api = await ObterAsync(mysql);
        var cliente = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, "/api/rh/__teste/ping", await TokenAsync(api, ApiFactory.EmailRh))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, "/api/gestor/__teste/ping", await TokenAsync(api, EmailGestor))).StatusCode);
    }

    [Fact]
    public async Task Qualquer_perfil_passa_na_familia_eu()
    {
        var api = await ObterAsync(mysql);
        var cliente = api.CreateClient();

        foreach (var email in new[] { ApiFactory.EmailComCargo, EmailGestor, ApiFactory.EmailRh })
        {
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, "/api/eu/__teste/ping", await TokenAsync(api, email))).StatusCode);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Diretor")]
    [InlineData("rh")]
    [InlineData("1")]
    public async Task Token_sem_perfil_ou_com_perfil_desconhecido_recebe_403_na_familia_eu(string? perfil)
    {
        var cliente = (await ObterAsync(mysql)).CreateClient();
        var token = TokenForjado(perfil); // válido, assinado com a chave certa

        await AssertProblemaAsync(await GetAsync(cliente, "/api/eu/__teste/ping", token), 403, TituloSemPermissao, DetalheSemPermissao);
        await AssertProblemaAsync(await GetAsync(cliente, "/api/auth/eu", token), 403, TituloSemPermissao, DetalheSemPermissao);
    }

    [Fact]
    public async Task Rota_fora_das_familias_exige_login_por_padrao()
    {
        var api = await ObterAsync(mysql);
        var cliente = api.CreateClient();

        await AssertProblemaAsync(await GetAsync(cliente, "/__teste/sem-familia"), 401, "Não autenticado.", "Entre novamente para continuar.");
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, "/__teste/sem-familia", await TokenAsync(api, ApiFactory.EmailComCargo))).StatusCode);
    }

    [Fact]
    public async Task Rotas_publicas_da_fatia_0_continuam_anonimas_e_auth_eu_exige_login()
    {
        var api = await ObterAsync(mysql);
        var cliente = api.CreateClient();

        // Anônimas: chegam ao tratamento da rota (400 por corpo vazio), e não são barradas antes com 401.
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/auth/entrar", new { email = "", senha = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/auth/sair", new { refreshToken = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, "/health")).StatusCode);

        await AssertProblemaAsync(await GetAsync(cliente, "/api/auth/eu"), 401, "Não autenticado.", "Entre novamente para continuar.");
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(cliente, "/api/auth/eu", await TokenAsync(api, ApiFactory.EmailComCargo))).StatusCode);
    }
}
