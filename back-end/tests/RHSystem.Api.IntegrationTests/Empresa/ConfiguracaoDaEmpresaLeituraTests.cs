using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum;
using RHSystem.Application.Empresa;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Empresa;

/// <summary>
/// <c>IConfiguracaoDaEmpresa</c>: a porta que as fatias 3 (ponto) e 7 (férias) usam para ler a empresa. Sem cache, e com a falha de negócio (<c>EMPRESA_NAO_CONFIGURADA</c>, 422) para a operação que precisa dela.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConfiguracaoDaEmpresaLeituraTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(ConfiguracaoDaEmpresaLeituraTests));

    private static Task<T> NoEscopoAsync<T>(ApiDeAcesso api, Func<IConfiguracaoDaEmpresa, Task<T>> acao) =>
        api.NoEscopoAsync(sp => acao(sp.GetRequiredService<IConfiguracaoDaEmpresa>()));

    [Fact]
    public async Task ObterAsync_devolve_null_sem_configuracao_e_os_valores_salvos_depois()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);

        Assert.Null(await NoEscopoAsync(api, empresa => empresa.ObterAsync(CancellationToken.None)));

        await EmpresaDeTeste.InserirAsync(api.Conexao, nome: "Empresa lida", uf: "PR", municipio: "Curitiba", entrada: "07:30:00", saida: "16:45:00", tolerancia: 12, dia: 25);

        var lida = await NoEscopoAsync(api, empresa => empresa.ObterAsync(CancellationToken.None));

        Assert.Equal(new ConfiguracaoDaEmpresaLida("Empresa lida", "PR", "Curitiba", new TimeOnly(7, 30), new TimeOnly(16, 45), 12, 25), lida);
    }

    [Fact]
    public async Task ObterOuFalharAsync_sem_configuracao_lanca_e_a_rota_de_teste_responde_422_com_codigo_EMPRESA_NAO_CONFIGURADA()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);

        var erro = await Assert.ThrowsAsync<ErroDeNegocioException>(() => NoEscopoAsync(api, empresa => empresa.ObterOuFalharAsync(CancellationToken.None)));
        Assert.Equal(("EMPRESA_NAO_CONFIGURADA", "A empresa ainda não foi configurada."), (erro.Codigo, erro.Detalhe));

        var resposta = await api.Api.ClienteAnonimo().GetAsync("/__teste/empresa-obrigatoria"); // a rota de teste de uma operação que precisa da empresa
        Assert.Equal((422, "EMPRESA_NAO_CONFIGURADA", "A empresa ainda não foi configurada."), await ProblemaAsync(resposta));

        await EmpresaDeTeste.InserirAsync(api.Conexao);
        Assert.Equal(200, (int)(await api.Api.ClienteAnonimo().GetAsync("/__teste/empresa-obrigatoria")).StatusCode);
        Assert.Equal("Empresa semeada", (await NoEscopoAsync(api, empresa => empresa.ObterOuFalharAsync(CancellationToken.None))).NomeEmpresa);
    }

    [Fact]
    public async Task ObterAsync_enxerga_a_mudanca_logo_depois_de_salvar_sem_cache()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.InserirAsync(api.Conexao, nome: "Antes", municipio: "Santos");
        Assert.Equal("Santos", (await NoEscopoAsync(api, empresa => empresa.ObterAsync(CancellationToken.None)))!.Municipio);

        var resposta = await api.Api.ClienteComo(api.Elenco.Rh).PutAsJsonAsync("/api/rh/empresa", new { nomeEmpresa = "Depois", uf = "SP", municipio = "Campinas", horarioEntrada = "08:00", horarioSaida = "17:00", toleranciaAtrasoMinutos = 0, diaPagamento = 1 });

        Assert.Equal(200, (int)resposta.StatusCode);
        var lida = (await NoEscopoAsync(api, empresa => empresa.ObterAsync(CancellationToken.None)))!;
        Assert.Equal(("Depois", "Campinas"), (lida.NomeEmpresa, lida.Municipio)); // a leitura seguinte já vê a mudança: nada ficou guardado
    }
}
