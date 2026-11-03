using Microsoft.AspNetCore.Hosting;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Matriz;

/// <summary>
/// A matriz de perfis: toda rota da API, chamada como cada perfil (e sem login), tem de responder como o <c>x-perfis</c> do contrato manda. Uma
/// rota nova entra na matriz sozinha: quem a cria não escreve teste de perfil.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MatrizDePerfisTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Toda_rota_da_API_responde_a_cada_perfil_como_o_x_perfis_do_contrato()
    {
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);

        var resultado = await MatrizDePerfis.ExecutarAsync(api, elenco);

        Assert.True(resultado.Falhas.Count == 0, "A matriz de perfis achou:\n- " + string.Join("\n- ", resultado.Falhas));
        Assert.True(resultado.RotasConferidas > 0, "A matriz não conferiu nenhuma rota.");
    }

    [Fact]
    public async Task Rota_real_fora_do_contrato_e_acusada_e_rota_de_teste_com_segmento___teste_e_ignorada()
    {
        // Controle positivo: uma fábrica à parte com uma rota real que o contrato não tem (/api/rh/rota-falsa-fora-do-contrato). As rotas
        // /api/*/__teste/ping já existem em toda fábrica e não podem aparecer na acusação.
        var (api, elenco) = await ApiComElenco.ObterAsync(mysql);
        var comRotaFalsa = api.WithWebHostBuilder(b => b.UseSetting("Testes:RotaFalsaDaMatriz", "true"));

        var falhas = (await MatrizDePerfis.ExecutarAsync(comRotaFalsa, elenco)).Falhas;

        var foraDoContrato = Assert.Single(falhas);
        Assert.Contains("rota fora do contrato", foraDoContrato);
        Assert.Contains("/api/rh/rota-falsa-fora-do-contrato", foraDoContrato);
        Assert.DoesNotContain(falhas, f => f.Contains("__teste", StringComparison.Ordinal));
    }
}
