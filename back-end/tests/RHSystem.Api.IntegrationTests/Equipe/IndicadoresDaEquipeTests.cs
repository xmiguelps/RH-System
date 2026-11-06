using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Equipe;

/// <summary>
/// <c>GET /api/gestor/equipe/{id}/indicadores</c>: o mesmo mecanismo dos indicadores do RH (cada campo é o primeiro valor das fontes registradas; sem fonte, <c>null</c>, não zero inventado), com os quatro
/// campos do gestor e SEM <c>proximoPagamento</c>. As fontes são das fatias 3, 4, 6 e 7; aqui o teste registra fontes falsas.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class IndicadoresDaEquipeTests(MySqlFixture mysql)
{
    private sealed class FonteDeIndicadorFalsa(IndicadorDaFicha indicador, int? valor) : IFonteDeIndicadorDaFicha
    {
        public IndicadorDaFicha Indicador => indicador;

        public Task<int?> ObterAsync(int funcionarioId, CancellationToken ct) => Task.FromResult(valor);
    }

    private static readonly string[] Campos = ["advertenciasQuantidade", "bancoDeHorasMinutos", "feriasDisponiveisDias", "treinamentosPendentes"];

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(IndicadoresDaEquipeTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Rota(ApiDeAcesso api) => $"/api/gestor/equipe/{api.Elenco.MembroA.FuncionarioId}/indicadores";

    [Fact]
    public async Task Indicadores_da_equipe_sem_fonte_vem_com_os_quatro_campos_null_e_com_fontes_registradas_traz_os_valores()
    {
        var api = await ApiAsync();

        var semFonte = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota(api));

        Assert.Equal(200, (int)semFonte.StatusCode);
        var corpo = await CorpoAsync(semFonte);
        Assert.Equal(Campos, corpo.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)); // os quatro campos do gestor, e nenhum proximoPagamento
        Assert.All(Campos, campo => Assert.Equal(JsonValueKind.Null, corpo.GetProperty(campo).ValueKind)); // sem fonte: null, e não zero inventado
        await semFonte.AfirmarConformeAoContratoAsync("obterIndicadoresDaEquipe");

        var comFontes = api.Api.WithWebHostBuilder(b => b.ConfigureServices(servicos =>
        {
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.BancoDeHorasMinutos, -125)); // a primeira registrada vence
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.BancoDeHorasMinutos, 999));
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.FeriasDisponiveisDias, 22));
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.TreinamentosPendentes, 4));
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.AdvertenciasQuantidade, 1));
            servicos.AddScoped<IFonteDoProximoPagamento>(_ => new ProximoPagamentoFalso()); // o gestor não tem este campo, mesmo com fonte registrada
        }));
        var cliente = comFontes.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = api.Api.ClienteComo(api.Elenco.Gestor).DefaultRequestHeaders.Authorization;

        var preenchido = await CorpoAsync(await cliente.GetAsync(Rota(api)));

        Assert.Equal((-125, 22, 4, 1), (preenchido.GetProperty("bancoDeHorasMinutos").GetInt32(), preenchido.GetProperty("feriasDisponiveisDias").GetInt32(), preenchido.GetProperty("treinamentosPendentes").GetInt32(), preenchido.GetProperty("advertenciasQuantidade").GetInt32()));
        Assert.False(preenchido.TryGetProperty("proximoPagamento", out _));
    }

    [Fact]
    public async Task Fonte_que_so_sabe_um_indicador_deixa_os_outros_null()
    {
        var api = await ApiAsync();
        var comUmaFonte = api.Api.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.TreinamentosPendentes, 0))));
        var cliente = comUmaFonte.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = api.Api.ClienteComo(api.Elenco.Gestor).DefaultRequestHeaders.Authorization;

        var corpo = await CorpoAsync(await cliente.GetAsync(Rota(api)));

        Assert.Equal(0, corpo.GetProperty("treinamentosPendentes").GetInt32()); // zero de verdade (a fonte disse 0) é diferente de "sem fonte"
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("bancoDeHorasMinutos").ValueKind);
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("feriasDisponiveisDias").ValueKind);
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("advertenciasQuantidade").ValueKind);
    }

    private sealed class ProximoPagamentoFalso : IFonteDoProximoPagamento
    {
        public Task<DateOnly?> ObterAsync(CancellationToken ct) => Task.FromResult<DateOnly?>(new DateOnly(2026, 10, 30));
    }
}
