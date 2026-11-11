using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Cadastros;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// Os pedidos de ajuste que ficam parados no gestor quando ele se ausenta, é trocado ou tirado do setor (<see cref="IPedidosParadosNoGestorDaFatia"/> do ponto), no banco REAL com o relógio em 2026-10-07T12:42Z. Cada teste monta o setor dele
/// (<c>SetoresDeTeste.ComEquipeAsync</c>: gestor e membros por SQL, sem login) e semeia os pedidos pela entidade.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PedidosParadosTests(MySqlFixture mysql)
{
    private static int _rotulo;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PedidosParadosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    /// <summary>Um setor com gestor e três pedidos: um na etapa do gestor, um que o gestor já aprovou e um de OUTRO gestor (Carlos, do elenco).</summary>
    private sealed record Cenario(SetorDeTeste Setor, int ParadoNoGestor, int JaAprovadoPeloGestor, int DeOutroGestor);

    private static async Task<Cenario> MontarAsync(ApiDeAcesso api)
    {
        var n = Interlocked.Increment(ref _rotulo);
        var setor = await SetoresDeTeste.ComEquipeAsync(api, $"Parados{n}");
        var carlos = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {api.Elenco.Gestor.FuncionarioId}");
        var dia = new DateOnly(2026, 6, n % 20 + 1);
        var parado = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, setor.Ativos[0], dia, setor.Gestor.GestorId);
        var aprovado = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, setor.Ativos[1], dia, setor.Gestor.GestorId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE PONTOS_MANUTENCOES SET RESULTADO_GESTOR = 'APROVOU', DATA_ETAPA_GESTOR = '2026-10-06 10:00:00' WHERE ID_MANUTENCAO_PONTO = {aprovado}");
        var deOutro = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, dia, carlos);
        return new Cenario(setor, parado, aprovado, deOutro);
    }

    // ---------------------------------------------------------------- a porta

    [Fact]
    public async Task Aprovar_a_etapa_do_gestor_aprova_so_os_pedidos_parados_nele_com_automatica_true_e_conta_ajustesDePonto()
    {
        var api = await ApiAsync();
        var c = await MontarAsync(api);

        var aprovados = await api.NaTransacaoAsync(async sp =>
        {
            var resultado = await sp.GetRequiredService<IPedidosParadosNoGestorDaFatia>().AprovarEtapaDoGestorAsync(c.Setor.Gestor.GestorId, CancellationToken.None);
            await sp.GetRequiredService<IUnidadeDeTrabalho>().SalvarAsync(CancellationToken.None); // o evento resumo do auditor sai com a gravação de quem chamou
            return resultado;
        });
        var denovo = await api.NaTransacaoAsync(sp => sp.GetRequiredService<IPedidosParadosNoGestorDaFatia>().AprovarEtapaDoGestorAsync(c.Setor.Gestor.GestorId, CancellationToken.None));

        Assert.Equal((new PedidosAprovados(1, 0), PedidosAprovados.Zero), (aprovados, denovo)); // idempotente: a segunda chamada não acha mais nada parado
        Assert.Equal(("PENDENTE", "APROVOU", "True", "2026-10-07 09:42:00"), (
            await TextoAsync(api, $"SELECT STATUS_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.ParadoNoGestor}"),
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.ParadoNoGestor}"),
            await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.ParadoNoGestor}"),
            Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_ETAPA_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.ParadoNoGestor}"), System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal("False", await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.JaAprovadoPeloGestor}")); // o que o gestor já aprovou continua dele
        Assert.Null(await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.DeOutroGestor}")); // o de outro gestor não é tocado
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE AREA = 'PONTO' AND DESCRICAO LIKE '%aprovou sozinha (gestor ausente) a etapa do gestor de 1 pedido(s)%'")); // um evento resumo por chamada, e só quando andou algum
    }

    [Fact]
    public async Task Passar_ao_RH_tira_o_ID_GESTOR_so_dos_pedidos_parados_nele()
    {
        var api = await ApiAsync();
        var c = await MontarAsync(api);

        await api.NaTransacaoAsync(async sp =>
        {
            await sp.GetRequiredService<IPedidosParadosNoGestorDaFatia>().PassarParaORhAsync(c.Setor.Gestor.GestorId, CancellationToken.None);
            return 0;
        });

        Assert.Null(await TextoAsync(api, $"SELECT ID_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.ParadoNoGestor}")); // sem etapa do gestor: o RH decide
        Assert.Equal(c.Setor.Gestor.GestorId.ToString(), await TextoAsync(api, $"SELECT ID_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.JaAprovadoPeloGestor}")); // o que o gestor aprovou fica como está
        Assert.NotNull(await TextoAsync(api, $"SELECT ID_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.DeOutroGestor}"));
        var comoRh = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/ponto/pedidos-de-ajuste/{c.ParadoNoGestor}"));
        Assert.Equal(("RH", System.Text.Json.JsonValueKind.Null, System.Text.Json.JsonValueKind.Null), (comoRh.GetProperty("etapa").GetString(), comoRh.GetProperty("gestorResponsavel").ValueKind, comoRh.GetProperty("decisaoGestor").ValueKind));
    }

    // ---------------------------------------------------------------- de ponta a ponta, pelas rotas da fatia 1

    [Fact]
    public async Task Ligar_a_ausencia_do_gestor_aprova_a_etapa_dos_pedidos_parados_e_devolve_ajustesDePonto()
    {
        var api = await ApiAsync();
        var c = await MontarAsync(api);
        var cliente = await api.ClienteDoRhAgoraAsync();

        var ligar = await cliente.PutAsJsonAsync($"/api/rh/gestores/{c.Setor.Gestor.GestorId}/ausencia", new { ausente = true, ausenteAte = (string?)null });

        Assert.Equal(200, (int)ligar.StatusCode);
        var aprovados = (await CorpoAsync(ligar)).GetProperty("pedidosAprovadosAgora");
        Assert.Equal((1, 0), (aprovados.GetProperty("ajustesDePonto").GetInt32(), aprovados.GetProperty("ferias").GetInt32()));
        var pedido = await CorpoAsync(await cliente.GetAsync($"/api/rh/ponto/pedidos-de-ajuste/{c.ParadoNoGestor}"));
        Assert.Equal(("PENDENTE", "RH", "APROVADA", true, System.Text.Json.JsonValueKind.Null), (pedido.GetProperty("status").GetString(), pedido.GetProperty("etapa").GetString(), pedido.GetProperty("decisaoGestor").GetProperty("decisao").GetString(), pedido.GetProperty("decisaoGestor").GetProperty("automatica").GetBoolean(), pedido.GetProperty("decisaoGestor").GetProperty("decididaPor").ValueKind));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'RESULTADO_AJUSTE_PONTO' AND REFERENCIA_ID = {c.ParadoNoGestor}")); // a aprovação automática não manda e-mail

        var desligar = await cliente.PutAsJsonAsync($"/api/rh/gestores/{c.Setor.Gestor.GestorId}/ausencia", new { ausente = false, ausenteAte = (string?)null });
        Assert.Equal(0, (await CorpoAsync(desligar)).GetProperty("pedidosAprovadosAgora").GetProperty("ajustesDePonto").GetInt32()); // desligar a ausência não aprova nada nem desfaz o que foi aprovado
        Assert.Equal("True", await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.ParadoNoGestor}"));
    }

    [Fact]
    public async Task Tirar_o_gestor_do_setor_passa_os_pedidos_parados_ao_RH_sem_etapa_do_gestor()
    {
        var api = await ApiAsync();
        var c = await MontarAsync(api);
        var cliente = await api.ClienteDoRhAgoraAsync();

        var tirar = await cliente.PutAsJsonAsync($"/api/rh/setores/{c.Setor.SetorId}", new { nome = c.Setor.Nome, descricao = (string?)null, gestorFuncionarioId = (int?)null });

        Assert.Equal(200, (int)tirar.StatusCode);
        var pedido = await CorpoAsync(await cliente.GetAsync($"/api/rh/ponto/pedidos-de-ajuste/{c.ParadoNoGestor}"));
        Assert.Equal(("RH", System.Text.Json.JsonValueKind.Null, System.Text.Json.JsonValueKind.Null), (pedido.GetProperty("etapa").GetString(), pedido.GetProperty("gestorResponsavel").ValueKind, pedido.GetProperty("decisaoGestor").ValueKind));
    }

    [Fact]
    public async Task Trocar_o_gestor_do_setor_passa_os_pedidos_do_gestor_antigo_ao_RH_e_o_novo_nao_os_recebe()
    {
        var api = await ApiAsync();
        var c = await MontarAsync(api);
        var novo = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Gestor novo {Guid.NewGuid():N}", $"{Guid.NewGuid():N}@parados.com.br", new DateOnly(2026, 1, 2), true, "Funcionario");

        var trocar = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"/api/rh/setores/{c.Setor.SetorId}", new { nome = c.Setor.Nome, descricao = (string?)null, gestorFuncionarioId = novo });

        Assert.Equal(200, (int)trocar.StatusCode);
        Assert.Null(await TextoAsync(api, $"SELECT ID_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {c.ParadoNoGestor}")); // nasceu no gestor antigo e agora só o RH decide: o novo não o recebe
    }
}
