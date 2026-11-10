using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Gestores;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Gestores;

/// <summary>
/// <see cref="IGestorAusente"/>: a interface que as fatias 3 (ajuste de ponto) e 7 (férias) chamam ao CRIAR um pedido para saber quem decide primeiro (<c>CONVENCOES.md</c>, seção 9). Chamada direto pelo contêiner da API, no banco
/// REAL, com o relógio fixo em 2026-10-07T12:42Z (hoje em Brasília: 07/10).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class GestorAusenteTests(MySqlFixture mysql)
{
    private static readonly EtapaInicialDoPedido ParaORhSemGestor = new(EtapaInicial.Rh, null, false);

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(GestorAusenteTests));

    private static async Task<T> ComAsync<T>(ApiDeAcesso api, Func<IGestorAusente, Task<T>> consulta)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        return await consulta(escopo.ServiceProvider.GetRequiredService<IGestorAusente>());
    }

    private static Task<EtapaInicialDoPedido> EtapaAsync(ApiDeAcesso api, int funcionarioId) => ComAsync(api, g => g.EtapaInicialDoPedidoAsync(funcionarioId, CancellationToken.None));

    private static Task MarcarAsync(ApiDeAcesso api, int gestorId, string? ate) =>
        Sql.ExecutarAsync(api.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE, APROVACAO_AUTOMATICA_ATE = {(ate is null ? "NULL" : $"'{ate}'")} WHERE ID_GESTOR = {gestorId}");

    [Fact]
    public async Task EtapaInicial_de_quem_e_gestor_ou_do_RH_cadastrado_e_Rh_sem_gestor()
    {
        var api = await ApiAsync();
        var gestorDeSetor = await GestoresDeTeste.GestorDeSetorAsync(api, "Quem1", equipe: 1);
        var rhCadastrado = await GestoresDeTeste.RhCadastradoAsync(api, "Quem1");

        Assert.Equal(ParaORhSemGestor, await EtapaAsync(api, gestorDeSetor.FuncionarioId)); // o gestor do setor decide com o RH, não com ele mesmo
        Assert.Equal(ParaORhSemGestor, await EtapaAsync(api, rhCadastrado.FuncionarioId));
    }

    [Fact]
    public async Task EtapaInicial_de_pessoa_de_setor_sem_gestor_ou_sem_setor_e_Rh_sem_gestor()
    {
        var api = await ApiAsync();
        var compras = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Semgestor2 Compras"); // setor sem gestor
        var recursosHumanos = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Semgestor2 Recursos Humanos");
        var doSetorSemGestor = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Semgestor2 Compradora", Perfil.Funcionario, compras);
        var semSetor = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Semgestor2 Sem setor", Perfil.Funcionario);
        var roberto = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Semgestor2 Roberto Alves", Perfil.RH, recursosHumanos); // do RH, sem linha em GESTORES, no setor sem gestor

        foreach (var pessoa in new[] { doSetorSemGestor, semSetor, roberto })
        {
            Assert.Equal(ParaORhSemGestor, await EtapaAsync(api, pessoa));
        }
    }

    [Fact]
    public async Task EtapaInicial_com_gestor_do_setor_inativo_e_Rh_sem_gestor()
    {
        var api = await ApiAsync();
        var inativo = await GestoresDeTeste.GestorInativoAsync(api, "Inativo3", equipe: 1); // o setor continua apontando para o gestor desligado

        Assert.Equal(inativo.GestorId, Convert.ToInt32(await Sql.EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM SETORES WHERE ID_SETOR = {inativo.SetorId}")));
        Assert.Equal(ParaORhSemGestor, await EtapaAsync(api, inativo.Membros[0]));
    }

    [Fact]
    public async Task EtapaInicial_com_gestor_presente_e_Gestor_com_o_GestorId_dele()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Presente4", equipe: 1);

        Assert.Equal(new EtapaInicialDoPedido(EtapaInicial.Gestor, gestor.GestorId, false), await EtapaAsync(api, gestor.Membros[0]));
    }

    [Fact]
    public async Task EtapaInicial_com_gestor_ausente_hoje_e_Rh_com_GestorId_e_aprovada_por_ausencia()
    {
        var api = await ApiAsync();
        var comVolta = await GestoresDeTeste.GestorDeSetorAsync(api, "Ausente5a", equipe: 1);
        var semVolta = await GestoresDeTeste.GestorDeSetorAsync(api, "Ausente5b", equipe: 1);
        await MarcarAsync(api, comVolta.GestorId, "2026-10-19");
        await MarcarAsync(api, semVolta.GestorId, null);

        Assert.Equal(new EtapaInicialDoPedido(EtapaInicial.Rh, comVolta.GestorId, true), await EtapaAsync(api, comVolta.Membros[0]));
        Assert.Equal(new EtapaInicialDoPedido(EtapaInicial.Rh, semVolta.GestorId, true), await EtapaAsync(api, semVolta.Membros[0]));
    }

    [Fact]
    public async Task EtapaInicial_passada_a_data_de_volta_volta_a_ser_Gestor()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Volta6", equipe: 1);
        await MarcarAsync(api, gestor.GestorId, "2026-10-19");

        try
        {
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc));
            var vespera = await EtapaAsync(api, gestor.Membros[0]);
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc));
            var diaDaVolta = await EtapaAsync(api, gestor.Membros[0]);

            Assert.Equal(new EtapaInicialDoPedido(EtapaInicial.Rh, gestor.GestorId, true), vespera);
            Assert.Equal(new EtapaInicialDoPedido(EtapaInicial.Gestor, gestor.GestorId, false), diaDaVolta);
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task EtapaInicial_de_ex_gestor_rebaixado_segue_o_gestor_do_setor_dele()
    {
        var api = await ApiAsync();
        var gestorDoSetor = await GestoresDeTeste.GestorDeSetorAsync(api, "Rebaixado7");
        var rebaixado = await GestoresDeTeste.ExGestorRebaixadoAsync(api, "Rebaixado7", setorDaPessoa: gestorDoSetor.SetorId); // mantém a linha em GESTORES, mas já não é gestor

        Assert.Equal(new EtapaInicialDoPedido(EtapaInicial.Gestor, gestorDoSetor.GestorId, false), await EtapaAsync(api, rebaixado.FuncionarioId));
    }

    [Fact]
    public async Task EstaAusenteHojeAsync_segue_a_mesma_regra_da_lista()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Esta8");

        async Task<bool> NaListaAsync()
        {
            var itens = await Cadastros.CadastrosDeTeste.ItensAsync(await (await api.Api.ClienteComTokenNovoAsync(api.Elenco.Rh)).GetAsync("/api/rh/gestores")); // token novo: o relógio anda
            return Assert.Single(itens, i => i.GetProperty("gestorId").GetInt32() == gestor.GestorId).GetProperty("ausenteHoje").GetBoolean();
        }

        Task<bool> PeloServicoAsync() => ComAsync(api, g => g.EstaAusenteHojeAsync(gestor.GestorId, CancellationToken.None));

        Assert.Equal((false, false), (await PeloServicoAsync(), await NaListaAsync())); // sem marca
        await MarcarAsync(api, gestor.GestorId, null);
        Assert.Equal((true, true), (await PeloServicoAsync(), await NaListaAsync())); // marcado, sem volta

        await MarcarAsync(api, gestor.GestorId, "2026-10-19");
        try
        {
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 19, 2, 30, 0, DateTimeKind.Utc)); // 18/10 23:30 em Brasília: ainda ausente
            Assert.Equal((true, true), (await PeloServicoAsync(), await NaListaAsync()));
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc)); // 19/10 00:00: voltou
            Assert.Equal((false, false), (await PeloServicoAsync(), await NaListaAsync()));
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }

        Assert.False(await ComAsync(api, g => g.EstaAusenteHojeAsync(int.MaxValue, CancellationToken.None))); // gestor que não existe
    }
}
