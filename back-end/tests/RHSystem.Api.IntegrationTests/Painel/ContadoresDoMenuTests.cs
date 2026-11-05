using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Escopo;
using RHSystem.Application.Painel;
using static RHSystem.Api.IntegrationTests.Autorizacao.ApiDeAutorizacao;

namespace RHSystem.Api.IntegrationTests.Painel;

/// <summary>
/// As três rotas dos contadores do menu (<c>/api/eu</c>, <c>/api/gestor</c> e <c>/api/rh</c> <c>/contadores-do-menu</c>). Nenhuma fatia dona registrou fonte ainda: sem
/// fonte tudo vale 0 (a API compartilhada do elenco); com fontes de teste (uma API derivada, <c>WithWebHostBuilder</c>, no mesmo banco) a soma e o escopo da pessoa
/// logada aparecem na resposta.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ContadoresDoMenuTests(MySqlFixture mysql)
{
    private const string Eu = "/api/eu/contadores-do-menu";
    private const string Gestor = "/api/gestor/contadores-do-menu";
    private const string Rh = "/api/rh/contadores-do-menu";

    private sealed class FonteDeTeste(ContadorDoMenu contador, Func<IEscopoDoUsuario, int> conta) : IFonteDeContadorDoMenu
    {
        public ContadorDoMenu Contador => contador;

        public Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct) => Task.FromResult(conta(escopo));
    }

    private static WebApplicationFactory<Program> ComFontes(ApiFactory api, params (ContadorDoMenu Contador, Func<IEscopoDoUsuario, int> Conta)[] fontes) =>
        api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            foreach (var (contador, conta) in fontes)
            {
                servicos.AddScoped<IFonteDeContadorDoMenu>(_ => new FonteDeTeste(contador, conta));
            }
        }));

    private static HttpClient Como(WebApplicationFactory<Program> fabrica, ElencoDeTeste elenco, PessoaDeTeste pessoa)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", elenco.TokensPorUsuario[pessoa.UsuarioId]);
        return cliente;
    }

    private static async Task<JsonElement> CorpoOkAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await LerAsync(resposta);
    }

    private static string[] Chaves(JsonElement corpo) => [.. corpo.EnumerateObject().Select(p => p.Name).Order()];

    [Fact]
    public async Task Eu_devolve_zeros_sem_fontes_e_a_soma_das_fontes_de_teste_registradas()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        var semFontes = await CorpoOkAsync(await api.ClienteComo(elenco.MembroA).GetAsync(Eu));

        Assert.Equal(["avisosNaoLidos", "documentosPendentes", "formulariosPendentes"], Chaves(semFontes));
        Assert.Equal([0, 0, 0], [semFontes.GetProperty("avisosNaoLidos").GetInt32(), semFontes.GetProperty("formulariosPendentes").GetInt32(), semFontes.GetProperty("documentosPendentes").GetInt32()]);

        var comFontes = ComFontes(
            api,
            (ContadorDoMenu.AvisosNaoLidos, _ => 2),
            (ContadorDoMenu.AvisosNaoLidos, escopo => escopo.FuncionarioId), // prova que a fonte recebe o escopo da PESSOA logada
            (ContadorDoMenu.FormulariosPendentes, _ => 1),
            (ContadorDoMenu.DocumentosPendentes, _ => 4),
            (ContadorDoMenu.PendenciasDaEquipe, _ => 100)); // contador de outra rota: não entra no /api/eu
        var deA = await CorpoOkAsync(await Como(comFontes, elenco, elenco.MembroA).GetAsync(Eu));
        var deB = await CorpoOkAsync(await Como(comFontes, elenco, elenco.MembroB).GetAsync(Eu));

        Assert.Equal([2 + elenco.MembroA.FuncionarioId, 1, 4], [deA.GetProperty("avisosNaoLidos").GetInt32(), deA.GetProperty("formulariosPendentes").GetInt32(), deA.GetProperty("documentosPendentes").GetInt32()]);
        Assert.Equal(2 + elenco.MembroB.FuncionarioId, deB.GetProperty("avisosNaoLidos").GetInt32()); // cada pessoa recebe o dela
    }

    [Fact]
    public async Task Gestor_e_RH_devolvem_zeros_sem_fontes_e_a_soma_das_fontes_de_cada_um()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        var gestorSem = await CorpoOkAsync(await api.ClienteComo(elenco.Gestor).GetAsync(Gestor));
        var rhSem = await CorpoOkAsync(await api.ClienteComo(elenco.Rh).GetAsync(Rh));

        Assert.Equal(["pedidosDeAjusteDePonto", "pendenciasDaEquipe"], Chaves(gestorSem));
        Assert.Equal([0, 0], [gestorSem.GetProperty("pendenciasDaEquipe").GetInt32(), gestorSem.GetProperty("pedidosDeAjusteDePonto").GetInt32()]);
        Assert.Equal(["pedidosDeAjusteDePonto"], Chaves(rhSem));
        Assert.Equal(0, rhSem.GetProperty("pedidosDeAjusteDePonto").GetInt32());

        var comFontes = ComFontes(
            api,
            (ContadorDoMenu.PendenciasDaEquipe, _ => 7),
            (ContadorDoMenu.PendenciasDaEquipe, _ => 1),
            (ContadorDoMenu.PedidosDeAjusteDePontoDoGestor, _ => 2),
            (ContadorDoMenu.PedidosDeAjusteDePontoDoRh, _ => 9),
            (ContadorDoMenu.DocumentosPendentes, _ => 50)); // do /api/eu: não entra nas outras duas
        var gestor = await CorpoOkAsync(await Como(comFontes, elenco, elenco.Gestor).GetAsync(Gestor));
        var rh = await CorpoOkAsync(await Como(comFontes, elenco, elenco.Rh).GetAsync(Rh));

        Assert.Equal([8, 2], [gestor.GetProperty("pendenciasDaEquipe").GetInt32(), gestor.GetProperty("pedidosDeAjusteDePonto").GetInt32()]); // PedidosDeAjusteDePonto = o do GESTOR, não o do RH
        Assert.Equal(9, rh.GetProperty("pedidosDeAjusteDePonto").GetInt32());
    }

    [Fact]
    public async Task Eu_aceita_os_tres_perfis_e_gestor_e_rh_so_aceitam_o_proprio_perfil()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        foreach (var pessoa in new[] { elenco.MembroA, elenco.Gestor, elenco.Rh })
        {
            Assert.Equal(HttpStatusCode.OK, (await api.ClienteComo(pessoa).GetAsync(Eu)).StatusCode); // a rota do /api/eu é aberta a todos os perfis
        }

        Assert.Equal(HttpStatusCode.OK, (await api.ClienteComo(elenco.Gestor).GetAsync(Gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.ClienteComo(elenco.Rh).GetAsync(Rh)).StatusCode);
        foreach (var (rota, intrusos) in new[] { (Gestor, new[] { elenco.MembroA, elenco.Rh }), (Rh, new[] { elenco.MembroA, elenco.Gestor }) })
        {
            foreach (var intruso in intrusos)
            {
                await AssertProblemaAsync(await api.ClienteComo(intruso).GetAsync(rota), 403, "Sem permissão.", "Você não tem permissão para esta ação.");
            }
        }

        foreach (var rota in new[] { Eu, Gestor, Rh })
        {
            await AssertProblemaAsync(await api.ClienteAnonimo().GetAsync(rota), 401, "Não autenticado.", "Entre novamente para continuar.");
        }
    }

    [Fact]
    public async Task Respostas_das_tres_rotas_e_de_auth_eu_batem_com_o_schema_do_contrato()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);
        var comFontes = ComFontes(
            api,
            (ContadorDoMenu.AvisosNaoLidos, _ => 2),
            (ContadorDoMenu.FormulariosPendentes, _ => 1),
            (ContadorDoMenu.DocumentosPendentes, _ => 1),
            (ContadorDoMenu.PendenciasDaEquipe, _ => 3),
            (ContadorDoMenu.PedidosDeAjusteDePontoDoGestor, _ => 2),
            (ContadorDoMenu.PedidosDeAjusteDePontoDoRh, _ => 4));

        foreach (var pessoa in new[] { elenco.MembroA, elenco.Gestor, elenco.Rh })
        {
            await (await Como(comFontes, elenco, pessoa).GetAsync(Eu)).AfirmarConformeAoContratoAsync("obterContadoresDoMenuDoFuncionario");
        }

        await (await Como(comFontes, elenco, elenco.Gestor).GetAsync(Gestor)).AfirmarConformeAoContratoAsync("obterContadoresDoMenuDoGestor");
        await (await Como(comFontes, elenco, elenco.Rh).GetAsync(Rh)).AfirmarConformeAoContratoAsync("obterContadoresDoMenuDoRh");
        await (await api.ClienteComo(elenco.MembroA).GetAsync(Gestor)).AfirmarConformeAoContratoAsync("obterContadoresDoMenuDoGestor"); // o 403
        await (await api.ClienteComo(elenco.Gestor).GetAsync(Rh)).AfirmarConformeAoContratoAsync("obterContadoresDoMenuDoRh"); // o 403
        await (await api.ClienteAnonimo().GetAsync(Eu)).AfirmarConformeAoContratoAsync("obterContadoresDoMenuDoFuncionario"); // o 401

        // /api/auth/eu: cargo e setor são texto ou null, e a CHAVE está sempre presente (o contrato os exige).
        var comCargo = await GetAsync(api.CreateClient(), "/api/auth/eu", await TokenAsync(api, ApiFactory.EmailComCargo));
        var semCargo = await GetAsync(api.CreateClient(), "/api/auth/eu", await TokenAsync(api, ApiFactory.EmailRh));
        await comCargo.AfirmarConformeAoContratoAsync("obterUsuarioLogado");
        await semCargo.AfirmarConformeAoContratoAsync("obterUsuarioLogado");
        var usuarioComCargo = await LerAsync(comCargo);
        var usuarioSemCargo = await LerAsync(semCargo);
        Assert.Equal("Analista de RH", usuarioComCargo.GetProperty("cargo").GetString());
        Assert.Equal("Recursos Humanos", usuarioComCargo.GetProperty("setor").GetString());
        Assert.Equal([JsonValueKind.Null, JsonValueKind.Null], [usuarioSemCargo.GetProperty("cargo").ValueKind, usuarioSemCargo.GetProperty("setor").ValueKind]); // GetProperty lança se a chave faltar
    }
}
