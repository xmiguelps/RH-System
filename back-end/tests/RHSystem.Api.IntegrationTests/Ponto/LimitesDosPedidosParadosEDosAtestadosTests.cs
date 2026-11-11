using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Cadastros;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ponto.Atestados;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// O que a mutação do incremento 3b achou nos pedidos parados no gestor e na decisão do atestado: quando nada está parado não sai evento resumo; o pedido que o RH decidiu sem etapa do gestor (o banco grava nele o <c>ID_GESTOR</c> de quem do RH decidiu) NÃO é um pedido
/// parado e nenhuma passagem o toca; e o atestado que outra aba decidiu entre a leitura e a gravação perde a corrida com o 409, sem sobrescrever a decisão dela.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class LimitesDosPedidosParadosEDosAtestadosTests(MySqlFixture mysql)
{
    private static int _rotulo;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(LimitesDosPedidosParadosEDosAtestadosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static async Task<T> NaTransacaoDaPortaAsync<T>(ApiDeAcesso api, Func<IPedidosParadosNoGestorDaFatia, Task<T>> acao) =>
        await api.NaTransacaoAsync(async sp =>
        {
            var resultado = await acao(sp.GetRequiredService<IPedidosParadosNoGestorDaFatia>());
            await sp.GetRequiredService<IUnidadeDeTrabalho>().SalvarAsync(CancellationToken.None); // o evento resumo do auditor sai com a gravação de quem chamou
            return resultado;
        });

    // ---------------------------------------------------------------- pedidos parados

    [Fact]
    public async Task Sem_pedido_parado_nenhuma_das_duas_passagens_grava_evento_resumo()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, $"Vazio{Interlocked.Increment(ref _rotulo)}");

        var aprovados = await NaTransacaoDaPortaAsync(api, porta => porta.AprovarEtapaDoGestorAsync(setor.Gestor.GestorId, CancellationToken.None));
        await NaTransacaoDaPortaAsync(api, async porta =>
        {
            await porta.PassarParaORhAsync(setor.Gestor.GestorId, CancellationToken.None);
            return 0;
        });

        Assert.Equal(PedidosAprovados.Zero, aprovados);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE AREA = 'PONTO' AND (DESCRICAO LIKE '%etapa do gestor de 0 pedido(s)%' OR DESCRICAO LIKE '%passou para a etapa do RH 0 pedido(s)%')"));
    }

    [Fact]
    public async Task Pedido_que_o_RH_decidiu_sem_etapa_do_gestor_nao_e_pedido_parado_e_nenhuma_passagem_o_toca()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, $"Decidido{Interlocked.Increment(ref _rotulo)}");
        var gestorId = setor.Gestor.GestorId;
        var pedido = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, setor.Ativos[0], new DateOnly(2026, 6, 10), gestorId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE PONTOS_MANUTENCOES SET STATUS_SOLICITACAO = 'APROVADA', ID_FUNCIONARIO_RH = {api.Elenco.Rh.FuncionarioId}, DATA_CONFIRMACAO_RH = '2026-06-20 10:00:00', DATA_ANALISE = '2026-06-20 10:00:00' WHERE ID_MANUTENCAO_PONTO = {pedido}");

        var aprovados = await NaTransacaoDaPortaAsync(api, porta => porta.AprovarEtapaDoGestorAsync(gestorId, CancellationToken.None));
        await NaTransacaoDaPortaAsync(api, async porta =>
        {
            await porta.PassarParaORhAsync(gestorId, CancellationToken.None);
            return 0;
        });

        Assert.Equal(PedidosAprovados.Zero, aprovados);
        Assert.Equal(("APROVADA", null, gestorId.ToString(), "False"), (
            await TextoAsync(api, $"SELECT STATUS_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido}"),
            await TextoAsync(api, $"SELECT RESULTADO_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido}"), // continua sem resultado do gestor
            await TextoAsync(api, $"SELECT ID_GESTOR FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido}"), // e o ID_GESTOR de quem decidiu não é apagado
            await TextoAsync(api, $"SELECT ETAPA_GESTOR_AUTOMATICA FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido}")));
    }

    // ---------------------------------------------------------------- a corrida na decisão do atestado

    [Fact]
    public async Task Atestado_decidido_por_outra_aba_entre_a_leitura_e_a_gravacao_da_409_e_a_decisao_dela_continua()
    {
        var api = await ApiAsync();
        var rhId = api.Elenco.Rh.FuncionarioId;
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-01-12", "2026-01-12");
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IAtestadosRepositorio>();
            servicos.AddScoped<IAtestadosRepositorio>(sp => new RecusaAntes(ActivatorUtilities.CreateInstance<AtestadosRepositorio>(sp), api.Api.Services, rhId));
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;

        var resposta = await cliente.PostAsJsonAsync($"/api/rh/ponto/atestados/{id}/validar", new { });

        Assert.Equal(409, (int)resposta.StatusCode);
        Assert.Equal("ATESTADO_JA_DECIDIDO", (await CorpoAsync(resposta)).GetProperty("codigo").GetString());
        Assert.Equal(("RECUSADO", "Outra aba."), (await TextoAsync(api, $"SELECT STATUS_ATESTADO FROM ATESTADOS WHERE ID_ATESTADO = {id}"), await TextoAsync(api, $"SELECT MOTIVO_RECUSA FROM ATESTADOS WHERE ID_ATESTADO = {id}")));
        Assert.DoesNotContain(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS", id), e => e.Acao == AuditoriaAcao.Aprovou); // o evento da validação que perdeu a corrida desfez junto
    }

    /// <summary>O repositório de verdade, que antes de gravar a decisão faz a decisão de OUTRA aba (recusa o atestado por outro contexto): a gravação condicional acha zero linhas.</summary>
    private sealed class RecusaAntes(IAtestadosRepositorio real, IServiceProvider raiz, int rhId) : IAtestadosRepositorio
    {
        public Task<int> AdicionarAsync(Atestado atestado, CancellationToken ct) => real.AdicionarAsync(atestado, ct);

        public async Task<bool> DecidirAsync(int atestadoId, SituacaoDoAtestado decisao, int funcionarioRhId, DateTime agoraUtc, string? motivo, CancellationToken ct)
        {
            await using (var escopo = raiz.CreateAsyncScope())
            {
                var outraAba = ActivatorUtilities.CreateInstance<AtestadosRepositorio>(escopo.ServiceProvider);
                Assert.True(await outraAba.DecidirAsync(atestadoId, SituacaoDoAtestado.Recusado, rhId, agoraUtc, "Outra aba.", ct));
            }

            return await real.DecidirAsync(atestadoId, decisao, funcionarioRhId, agoraUtc, motivo, ct);
        }
    }
}
