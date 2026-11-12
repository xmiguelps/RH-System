using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Infrastructure.Ponto;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// O registro da porta dos pedidos parados no gestor no contêiner de verdade: o composto reúne as fatias registradas (hoje só a do ponto, a da fatia 3) e uma fatia registrada por <c>AddScoped</c> é chamada pela porta única.
/// A fatia falsa entra por uma API derivada (<c>WithWebHostBuilder</c>), não pelo <c>ApiFactory</c> comum.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PedidosParadosNoGestorRegistroTests(MySqlFixture mysql)
{
    private sealed class FatiaDeTeste(List<string> chamadas, PedidosAprovados aprova) : IPedidosParadosNoGestorDaFatia
    {
        public Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)
        {
            chamadas.Add($"Aprovar:{gestorId}");
            return Task.FromResult(aprova);
        }

        public Task PassarParaORhAsync(int gestorId, CancellationToken ct)
        {
            chamadas.Add($"Passar:{gestorId}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task IPedidosParadosNoGestor_esta_registrado_com_a_fatia_do_ponto_e_sem_pedido_parado_nao_faz_nada()
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);
        await using var escopo = api.Services.CreateAsyncScope();

        var porta = escopo.ServiceProvider.GetRequiredService<IPedidosParadosNoGestor>();

        Assert.IsType<PedidosParadosNoGestorComposto>(porta);
        Assert.IsType<PedidosParadosNoGestorDoPonto>(Assert.Single(escopo.ServiceProvider.GetServices<IPedidosParadosNoGestorDaFatia>())); // até a fatia 7 registrar a dela, o ponto é a única
        await porta.PassarParaORhAsync(123, CancellationToken.None); // um gestorId sem pedido parado: termina sem lançar
        Assert.Equal(PedidosAprovados.Zero, await porta.AprovarEtapaDoGestorAsync(123, CancellationToken.None));
    }

    [Fact]
    public async Task Fatia_registrada_por_AddScoped_e_chamada_pela_porta_unica_e_o_valor_dela_sai_em_AprovarEtapaDoGestorAsync()
    {
        var chamadas = new List<string>();
        var api = (await ApiCompartilhada.ObterAsync(mysql)).WithWebHostBuilder(construtor =>
            construtor.ConfigureServices(servicos => servicos.AddScoped<IPedidosParadosNoGestorDaFatia>(_ => new FatiaDeTeste(chamadas, new PedidosAprovados(3, 2)))));
        await using var escopo = api.Services.CreateAsyncScope();

        var porta = escopo.ServiceProvider.GetRequiredService<IPedidosParadosNoGestor>();
        var aprovados = await porta.AprovarEtapaDoGestorAsync(77, CancellationToken.None);
        await porta.PassarParaORhAsync(77, CancellationToken.None);

        Assert.Equal(new PedidosAprovados(3, 2), aprovados);
        Assert.Equal(["Aprovar:77", "Passar:77"], chamadas); // a fatia foi chamada pela porta única, com o gestorId recebido
    }

    [Fact]
    public async Task A_porta_e_registrada_como_escopo_da_requisicao()
    {
        // A fatia usa o AppDbContext da requisição (escopo): a porta não pode ser singleton (capturaria o contexto de uma requisição) nem transitória sem necessidade.
        var api = await ApiCompartilhada.ObterAsync(mysql);

        await using var escopoA = api.Services.CreateAsyncScope();
        await using var escopoB = api.Services.CreateAsyncScope();
        var a1 = escopoA.ServiceProvider.GetRequiredService<IPedidosParadosNoGestor>();
        var a2 = escopoA.ServiceProvider.GetRequiredService<IPedidosParadosNoGestor>();
        var b = escopoB.ServiceProvider.GetRequiredService<IPedidosParadosNoGestor>();

        Assert.Same(a1, a2);
        Assert.NotSame(a1, b);
    }
}
