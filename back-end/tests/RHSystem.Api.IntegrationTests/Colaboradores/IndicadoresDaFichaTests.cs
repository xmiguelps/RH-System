using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// <c>GET /api/rh/funcionarios/{id}/indicadores</c>: cada campo é o primeiro valor das fontes registradas para ele; sem fonte é <c>null</c> (não zero inventado). As fontes são das fatias 3, 4 e 7;
/// aqui o teste registra fontes falsas.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class IndicadoresDaFichaTests(MySqlFixture mysql)
{
    private sealed class FonteDeIndicadorFalsa(IndicadorDaFicha indicador, int? valor) : IFonteDeIndicadorDaFicha
    {
        public IndicadorDaFicha Indicador => indicador;

        public Task<int?> ObterAsync(int funcionarioId, CancellationToken ct) => Task.FromResult(valor);
    }

    private sealed class ProximoPagamentoFalso(DateOnly? dia) : IFonteDoProximoPagamento
    {
        public Task<DateOnly?> ObterAsync(CancellationToken ct) => Task.FromResult(dia);
    }

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(IndicadoresDaFichaTests));

    [Fact]
    public async Task Indicadores_sem_nenhuma_fonte_devolvem_os_quatro_campos_null()
    {
        var api = await ApiAsync();

        var resposta = await api.Api.ClienteComo(api.Elenco.Rh).GetAsync($"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/indicadores");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        foreach (var campo in new[] { "bancoDeHorasMinutos", "feriasDisponiveisDias", "advertenciasQuantidade", "proximoPagamento" })
        {
            Assert.Equal(JsonValueKind.Null, corpo.GetProperty(campo).ValueKind); // sem fonte: null, e não zero inventado
        }

        await resposta.AfirmarConformeAoContratoAsync("obterIndicadoresDoFuncionario");
    }

    [Fact]
    public async Task Indicadores_usam_o_valor_das_fontes_registradas_e_a_data_do_proximo_pagamento()
    {
        var api = await ApiAsync();
        var comFontes = api.Api.WithWebHostBuilder(b => b.ConfigureServices(servicos =>
        {
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.BancoDeHorasMinutos, -125)); // a primeira registrada vence
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.BancoDeHorasMinutos, 999));
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.FeriasDisponiveisDias, 22));
            servicos.AddScoped<IFonteDeIndicadorDaFicha>(_ => new FonteDeIndicadorFalsa(IndicadorDaFicha.TreinamentosPendentes, 4)); // de outra tela: não entra neste corpo
            servicos.AddScoped<IFonteDoProximoPagamento>(_ => new ProximoPagamentoFalso(new DateOnly(2026, 10, 30)));
        }));
        var cliente = comFontes.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = api.Api.ClienteComo(api.Elenco.Rh).DefaultRequestHeaders.Authorization;

        var corpo = await CorpoAsync(await cliente.GetAsync($"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/indicadores"));

        Assert.Equal(-125, corpo.GetProperty("bancoDeHorasMinutos").GetInt32());
        Assert.Equal(22, corpo.GetProperty("feriasDisponiveisDias").GetInt32());
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("advertenciasQuantidade").ValueKind); // sem fonte para este campo: null
        Assert.Equal("2026-10-30", corpo.GetProperty("proximoPagamento").GetString());
        Assert.False(corpo.TryGetProperty("treinamentosPendentes", out _));
    }

    [Fact]
    public async Task Indicadores_de_id_inexistente_devolvem_404_e_o_gestor_recebe_403()
    {
        var api = await ApiAsync();

        Assert.Equal(404, (int)(await api.Api.ClienteComo(api.Elenco.Rh).GetAsync("/api/rh/funcionarios/987654321/indicadores")).StatusCode);
        Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync($"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/indicadores")).StatusCode);
        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync($"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/indicadores")).StatusCode);
        await (await api.Api.ClienteComo(api.Elenco.Rh).GetAsync("/api/rh/funcionarios/987654321/indicadores")).AfirmarConformeAoContratoAsync("obterIndicadoresDoFuncionario"); // o 404
    }
}
