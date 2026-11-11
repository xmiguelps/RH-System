using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Documentos;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Painel;

/// <summary>
/// A bolinha <c>documentosPendentes</c> do menu (<c>GET /api/eu/contadores-do-menu</c>): quantos documentos <c>PENDENTE</c> a PRÓPRIA pessoa tem, em uma consulta de contagem. Vale para Funcionario, Gestor e RH, e só nesta
/// rota: as do gestor e do RH não têm o campo. A fonte soma com as de teste da 1a (que as registram a mais): sozinha, a pessoa sem pedido vê 0.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DocumentosPendentesTests(MySqlFixture mysql)
{
    private const string Eu = "/api/eu/contadores-do-menu";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(DocumentosPendentesTests));

    private static async Task<int> BolinhaAsync(HttpClient cliente)
    {
        var resposta = await cliente.GetAsync(Eu);
        Assert.Equal(200, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("documentosPendentes").GetInt32();
    }

    private static Task<int> PendenteAsync(ApiDeAcesso api, PessoaDoTeste pessoa, string tipo = "CPF") =>
        ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa.FuncionarioId, tipo, "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    [InlineData(Perfil.RH)]
    public async Task Bolinha_documentosPendentes_conta_so_os_pendentes_da_propria_pessoa_e_zera_ao_enviar(Perfil perfil)
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, $"Bolinha1 {perfil}", perfil);
        var outra = await PessoasDeTeste.ComLoginAsync(api, $"Bolinha1 outra {perfil}");
        var cliente = await PessoasDeTeste.ClienteAsync(api, eu);
        Assert.Equal(0, await BolinhaAsync(cliente)); // sem pedido, zero
        var primeiro = await PendenteAsync(api, eu, "CPF");
        var segundo = await PendenteAsync(api, eu, "IDENTIDADE");
        await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, eu.FuncionarioId, "OUTRO", "ENVIADO", null, CaminhosDeArquivo.Novo("documentos", ".pdf"), "a.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0)); // enviado não conta
        await PendenteAsync(api, outra); // pendente de outra pessoa não conta

        Assert.Equal(2, await BolinhaAsync(cliente));

        Assert.Equal(200, (int)(await cliente.PostAsync($"/api/eu/documentos/{primeiro}/arquivo", FormulariosDeTeste.Envio("cpf.pdf", ArquivosDeTeste.Pdf()))).StatusCode);
        Assert.Equal(1, await BolinhaAsync(cliente));
        Assert.Equal(200, (int)(await cliente.PostAsync($"/api/eu/documentos/{segundo}/arquivo", FormulariosDeTeste.Envio("rg.pdf", ArquivosDeTeste.Pdf()))).StatusCode);
        Assert.Equal(0, await BolinhaAsync(cliente)); // zera ao enviar
        Assert.Equal(1, await BolinhaAsync(await PessoasDeTeste.ClienteAsync(api, outra))); // e a da outra pessoa continua a dela
    }

    [Fact]
    public async Task Pedido_do_RH_aparece_na_lista_e_na_bolinha_da_pessoa_e_nao_na_de_outra()
    {
        var api = await ApiAsync();
        var pessoa = await PessoasDeTeste.ComLoginAsync(api, "Fluxo2 pessoa");
        var outra = await PessoasDeTeste.ComLoginAsync(api, "Fluxo2 outra");
        var clientePessoa = await PessoasDeTeste.ClienteAsync(api, pessoa);
        var clienteOutra = await PessoasDeTeste.ClienteAsync(api, outra);
        var rh = api.Api.ClienteComo(api.Elenco.Rh);
        api.Api.Armazenamento.Limpar();

        // O RH pede; a pessoa vê na lista e na bolinha; a outra não vê nada.
        var pedido = await CorpoAsync(await rh.PostAsync($"/api/rh/funcionarios/{pessoa.FuncionarioId}/documentos", FormulariosDeTeste.Pedido("COMPROVANTE_DE_RESIDENCIA", "Comprovante atualizado")));
        var id = pedido.GetProperty("id").GetInt32();
        var lista = await ItensAsync(await clientePessoa.GetAsync("/api/eu/documentos"));
        Assert.Equal((id, "PENDENTE"), (lista[0].GetProperty("id").GetInt32(), lista[0].GetProperty("situacao").GetString()));
        Assert.Equal((1, 0), (await BolinhaAsync(clientePessoa), await BolinhaAsync(clienteOutra)));
        Assert.Empty(await ItensAsync(await clienteOutra.GetAsync("/api/eu/documentos")));

        // A pessoa envia; a bolinha zera; o RH baixa.
        Assert.Equal(200, (int)(await clientePessoa.PostAsync($"/api/eu/documentos/{id}/arquivo", FormulariosDeTeste.Envio("comprovante.pdf", ArquivosDeTeste.Pdf()))).StatusCode);
        Assert.Equal(0, await BolinhaAsync(clientePessoa));
        var link = await rh.GetAsync($"/api/rh/funcionarios/{pessoa.FuncionarioId}/documentos/{id}/download");
        Assert.Equal(200, (int)link.StatusCode);
        Assert.Contains("arquivos.exemplo.test/documentos/", (await CorpoAsync(link)).GetProperty("url").GetString());
    }

    [Fact]
    public async Task Gestor_e_RH_somam_a_bolinha_so_na_rota_deles_do_funcionario_e_as_rotas_do_gestor_e_do_RH_nao_a_trazem()
    {
        var api = await ApiAsync();
        var gestor = await PessoasDeTeste.ComLoginAsync(api, "Rotas3 gestor", Perfil.Gestor);
        var rh = await PessoasDeTeste.ComLoginAsync(api, "Rotas3 rh", Perfil.RH);
        await PendenteAsync(api, gestor);
        await PendenteAsync(api, rh);

        var doGestor = await (await PessoasDeTeste.ClienteAsync(api, gestor)).GetAsync("/api/gestor/contadores-do-menu");
        var doRh = await (await PessoasDeTeste.ClienteAsync(api, rh)).GetAsync("/api/rh/contadores-do-menu");

        Assert.Equal(200, (int)doGestor.StatusCode);
        Assert.Equal(200, (int)doRh.StatusCode);
        Assert.DoesNotContain("documentosPendentes", await doGestor.Content.ReadAsStringAsync()); // PainelContadoresGestor e PainelContadoresRh não têm o campo
        Assert.DoesNotContain("documentosPendentes", await doRh.Content.ReadAsStringAsync());
        Assert.Equal(1, await BolinhaAsync(await PessoasDeTeste.ClienteAsync(api, gestor))); // e na rota /api/eu o gestor e o RH veem a própria
        Assert.Equal(1, await BolinhaAsync(await PessoasDeTeste.ClienteAsync(api, rh)));
    }
}
