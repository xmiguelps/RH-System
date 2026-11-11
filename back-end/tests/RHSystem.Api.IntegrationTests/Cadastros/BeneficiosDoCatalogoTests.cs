using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Cadastros;

/// <summary>
/// <c>/api/rh/beneficios</c> (tela 29, cadastro "Benefícios"), só para o RH, no banco REAL com o relógio fixo em 2026-10-07T12:42Z. É o CATÁLOGO (o que a empresa oferece); o benefício de cada pessoa (e o pedido) é
/// <c>BeneficioDoFuncionario</c>. Cada teste cria os benefícios dele (nome com um marcador próprio, para a busca isolá-los dos outros testes).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class BeneficiosDoCatalogoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/beneficios";
    private const string NomeRepetido = "Já existe um cadastro com este nome.";
    private const string EmUso = "Este benefício já foi dado a colaboradores e não pode ser excluído.";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(BeneficiosDoCatalogoTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static object Corpo(string nome, string? descricao = null, bool opcional = false, bool aceitaDependentes = false, bool aceitaTrajeto = false) =>
        new { nome, descricao, opcional, aceitaDependentes, aceitaTrajeto };

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome) =>
        SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, $"{Guid.NewGuid():N}@beneficios.com.br", new DateOnly(2026, 1, 2));

    // O BOOLEAN do MySQL chega como bool: o CAST fixa o 0 ou o 1 que o teste compara.
    private static async Task<(long Opcional, long Dependentes, long Trajeto)> MarcasAsync(ApiDeAcesso api, int id) =>
        (await Sql.ContarAsync(api.Conexao, $"SELECT CAST(OPCIONAL AS SIGNED) FROM BENEFICIOS WHERE ID_BENEFICIO = {id}"),
            await Sql.ContarAsync(api.Conexao, $"SELECT CAST(ACEITA_DEPENDENTES AS SIGNED) FROM BENEFICIOS WHERE ID_BENEFICIO = {id}"),
            await Sql.ContarAsync(api.Conexao, $"SELECT CAST(ACEITA_TRAJETO AS SIGNED) FROM BENEFICIOS WHERE ID_BENEFICIO = {id}"));

    // ---------------------------------------------------------------- lista

    [Fact]
    public async Task Lista_ordenada_por_id_com_as_tres_marcas_e_quantidadePessoas_so_dos_vinculos_ativos()
    {
        var api = await ApiAsync();
        var segundo = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Lista1 B vale-transporte", "Trajeto casa-trabalho", opcional: false, aceitaDependentes: false, aceitaTrajeto: true);
        var primeiro = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Lista1 A plano de saúde", null, opcional: true, aceitaDependentes: true, aceitaTrajeto: false);
        // Ordem por id, não por nome: "A plano" tem id MAIOR que "B vale" e vem DEPOIS.
        var ativa = await PessoaAsync(api, "Lista1 ativa");
        var inativa = await PessoaAsync(api, "Lista1 inativa");
        var pendente = await PessoaAsync(api, "Lista1 pendente");
        await ApoioDeTeste.DarBeneficioAsync(api.Conexao, ativa, primeiro, ativo: true, statusDoPedido: "APROVADO");
        await ApoioDeTeste.DarBeneficioAsync(api.Conexao, inativa, primeiro, ativo: false, statusDoPedido: "APROVADO"); // vínculo inativo não conta
        await ApoioDeTeste.DarBeneficioAsync(api.Conexao, pendente, primeiro, ativo: false, statusDoPedido: "PENDENTE"); // pedido pendente não conta

        var itens = await ItensAsync(await ComoRh(api).GetAsync($"{Rota}?busca=Lista1&tamanhoPagina=100"));

        Assert.Equal([segundo, primeiro], itens.Select(i => i.GetProperty("id").GetInt32()));
        Assert.Equal(("Lista1 B vale-transporte", "Trajeto casa-trabalho", false, false, true, 0), (itens[0].GetProperty("nome").GetString(), itens[0].GetProperty("descricao").GetString(), itens[0].GetProperty("opcional").GetBoolean(), itens[0].GetProperty("aceitaDependentes").GetBoolean(), itens[0].GetProperty("aceitaTrajeto").GetBoolean(), itens[0].GetProperty("quantidadePessoas").GetInt32()));
        Assert.Equal(("Lista1 A plano de saúde", System.Text.Json.JsonValueKind.Null, true, true, false, 1), (itens[1].GetProperty("nome").GetString(), itens[1].GetProperty("descricao").ValueKind, itens[1].GetProperty("opcional").GetBoolean(), itens[1].GetProperty("aceitaDependentes").GetBoolean(), itens[1].GetProperty("aceitaTrajeto").GetBoolean(), itens[1].GetProperty("quantidadePessoas").GetInt32()));
    }

    // ---------------------------------------------------------------- criar e editar

    [Fact]
    public async Task Criar_e_editar_gravam_true_e_false_das_marcas_como_enviados()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);

        var criado = await CorpoAsync(await cliente.PostAsJsonAsync(Rota, Corpo("Marcas2 benefício", "descrição", opcional: false, aceitaDependentes: false, aceitaTrajeto: true)));
        var id = criado.GetProperty("id").GetInt32();

        Assert.Equal((0L, 0L, 1L), await MarcasAsync(api, id)); // o false vai ao banco: o EF não pode omitir e deixar o padrão do V001
        Assert.Equal((false, false, true, 0), (criado.GetProperty("opcional").GetBoolean(), criado.GetProperty("aceitaDependentes").GetBoolean(), criado.GetProperty("aceitaTrajeto").GetBoolean(), criado.GetProperty("quantidadePessoas").GetInt32()));

        var editado = await CorpoAsync(await cliente.PutAsJsonAsync($"{Rota}/{id}", Corpo("Marcas2 benefício editado", null, opcional: true, aceitaDependentes: true, aceitaTrajeto: false)));

        Assert.Equal((1L, 1L, 0L), await MarcasAsync(api, id));
        Assert.Equal(("Marcas2 benefício editado", System.Text.Json.JsonValueKind.Null), (editado.GetProperty("nome").GetString(), editado.GetProperty("descricao").ValueKind));
        var alterou = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS", id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal((AuditoriaArea.Cadastros, api.Elenco.Rh.FuncionarioId), (alterou.Area, alterou.FuncionarioAtorId));
        Assert.Contains(alterou.Mudancas, m => m is { Campo: "Opcional", ValorAntes: "Não", ValorDepois: "Sim" });
    }

    [Fact]
    public async Task Nome_repetido_409_e_vinte_criacoes_simultaneas_dao_um_201_e_nenhum_500()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Nome3 Auxílio café", null, false, false, false);

        foreach (var repetido in new[] { "Nome3 Auxílio café", "nome3 AUXILIO CAFE" })
        {
            Assert.Equal((409, "NOME_JA_CADASTRADO", NomeRepetido), await ProblemaAsync(await cliente.PostAsJsonAsync(Rota, Corpo(repetido))));
        }

        var respostas = await EmSimultaneoAsync(20, _ => cliente.PostAsJsonAsync(Rota, Corpo("Corrida3 benefício disputado")));

        Assert.Equal(1, respostas.Count(r => (int)r.StatusCode == 201));
        foreach (var recusada in respostas.Where(r => (int)r.StatusCode != 201))
        {
            Assert.Equal((409, "NOME_JA_CADASTRADO", NomeRepetido), await ProblemaAsync(recusada));
        }

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM BENEFICIOS WHERE NOME_BENEFICIO = 'Corrida3 benefício disputado'"));
    }

    // ---------------------------------------------------------------- excluir

    [Fact]
    public async Task Excluir_beneficio_sem_vinculo_devolve_204_e_grava_Excluiu_com_a_descricao_Beneficio_Vale_cultura_excluido()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Vale-cultura excluir4", null, true, false, false);

        var resposta = await ComoRh(api).DeleteAsync($"{Rota}/{id}");

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BENEFICIOS WHERE ID_BENEFICIO = {id}"));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS", id));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Excluiu, "Benefício Vale-cultura excluir4 excluído", api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.FuncionarioAtorId));
    }

    [Fact]
    public async Task Excluir_com_qualquer_vinculo_ativo_inativo_ou_pedido_pendente_devolve_409_CADASTRO_EM_USO_e_nao_exclui()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var pessoa = await PessoaAsync(api, "Excluir5 pessoa");

        foreach (var (rotulo, ativo, status) in new[] { ("ativo", true, "APROVADO"), ("inativo", false, "APROVADO"), ("pendente", false, "PENDENTE") })
        {
            var id = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, $"Excluir5 {rotulo}", null, false, false, false);
            await ApoioDeTeste.DarBeneficioAsync(api.Conexao, pessoa, id, ativo, status);

            Assert.Equal((409, "CADASTRO_EM_USO", EmUso), await ProblemaAsync(await cliente.DeleteAsync($"{Rota}/{id}")));
            Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BENEFICIOS WHERE ID_BENEFICIO = {id}")); // continua no catálogo
            Assert.Empty(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS", id)); // e nenhuma exclusão foi registrada
        }
    }

    [Fact]
    public async Task Excluir_com_vinculo_criado_depois_de_carregar_o_beneficio_devolve_409_e_nunca_500()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Corrida6 benefício", null, false, false, false);
        var pessoa = await PessoaAsync(api, "Corrida6 pessoa");

        var erro = await api.NoEscopoAsync(async sp =>
        {
            var repositorio = sp.GetRequiredService<IBeneficiosRepositorio>();
            var beneficio = await repositorio.ObterAsync(id, CancellationToken.None); // a conferência de "sem vínculo" já passou
            await ApoioDeTeste.DarBeneficioAsync(api.Conexao, pessoa, id, ativo: true, statusDoPedido: "APROVADO"); // o vínculo nasce no meio da exclusão
            return await Assert.ThrowsAsync<ConflitoException>(() => repositorio.ExcluirAsync(beneficio!, CancellationToken.None));
        });

        Assert.Equal(("CADASTRO_EM_USO", EmUso), (erro.Codigo, erro.Detalhe)); // o 1451 do banco virou o 409, não um 500
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BENEFICIOS WHERE ID_BENEFICIO = {id}"));
    }

    [Fact]
    public async Task Editar_e_excluir_inexistente_devolvem_404()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);

        Assert.Equal(404, (int)(await cliente.PutAsJsonAsync($"{Rota}/{int.MaxValue}", Corpo("Inexistente7 qualquer"))).StatusCode);
        Assert.Equal(404, (int)(await cliente.DeleteAsync($"{Rota}/{int.MaxValue}")).StatusCode);
    }

    // ---------------------------------------------------------------- permissões e contrato

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_quatro_rotas()
    {
        var api = await ApiAsync();
        var id = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Permissao8 benefício", "intacto", false, false, false);
        var rotas = new (HttpMethod Metodo, string Caminho)[] { (HttpMethod.Get, Rota), (HttpMethod.Post, Rota), (HttpMethod.Put, $"{Rota}/{id}"), (HttpMethod.Delete, $"{Rota}/{id}") };

        foreach (var (metodo, caminho) in rotas)
        {
            foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
            {
                var pedido = new HttpRequestMessage(metodo, caminho) { Content = metodo == HttpMethod.Post || metodo == HttpMethod.Put ? JsonContent.Create(Corpo("Permissao8 invasor")) : null };
                Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).SendAsync(pedido)).StatusCode);
            }

            Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().SendAsync(new HttpRequestMessage(metodo, caminho))).StatusCode);
        }

        Assert.Equal(("Permissao8 benefício", "intacto"), (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_BENEFICIO FROM BENEFICIOS WHERE ID_BENEFICIO = {id}"), await Sql.EscalarAsync(api.Conexao, $"SELECT DESCRICAO FROM BENEFICIOS WHERE ID_BENEFICIO = {id}")));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM BENEFICIOS WHERE NOME_BENEFICIO = 'Permissao8 invasor'"));
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var id = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Schema9 benefício", "descrição", true, false, true);
        var emUso = await ApoioDeTeste.CriarBeneficioAsync(api.Conexao, "Schema9 em uso", null, false, false, false);
        await ApoioDeTeste.DarBeneficioAsync(api.Conexao, await PessoaAsync(api, "Schema9 pessoa"), emUso, true, "APROVADO");

        await (await cliente.GetAsync($"{Rota}?busca=Schema9")).AfirmarConformeAoContratoAsync("listarBeneficiosDoCatalogo");
        await (await cliente.GetAsync($"{Rota}?pagina=0")).AfirmarConformeAoContratoAsync("listarBeneficiosDoCatalogo"); // o 400
        await (await cliente.PostAsJsonAsync(Rota, Corpo("Schema9 criado", opcional: true))).AfirmarConformeAoContratoAsync("criarBeneficioDoCatalogo");
        await (await cliente.PostAsJsonAsync(Rota, Corpo("Schema9 criado"))).AfirmarConformeAoContratoAsync("criarBeneficioDoCatalogo"); // o 409
        await (await cliente.PutAsJsonAsync($"{Rota}/{id}", Corpo("Schema9 editado", null, false, true, false))).AfirmarConformeAoContratoAsync("editarBeneficioDoCatalogo");
        await (await cliente.PutAsJsonAsync($"{Rota}/{int.MaxValue}", Corpo("Schema9 nenhum"))).AfirmarConformeAoContratoAsync("editarBeneficioDoCatalogo"); // o 404
        await (await cliente.DeleteAsync($"{Rota}/{id}")).AfirmarConformeAoContratoAsync("excluirBeneficioDoCatalogo"); // o 204
        await (await cliente.DeleteAsync($"{Rota}/{emUso}")).AfirmarConformeAoContratoAsync("excluirBeneficioDoCatalogo"); // o 409
        await (await cliente.DeleteAsync($"{Rota}/{int.MaxValue}")).AfirmarConformeAoContratoAsync("excluirBeneficioDoCatalogo"); // o 404
    }
}
