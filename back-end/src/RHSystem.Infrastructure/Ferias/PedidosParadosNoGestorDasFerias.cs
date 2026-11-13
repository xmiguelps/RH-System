using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ferias;

/// <summary>
/// As férias na porta dos pedidos parados no gestor (<see cref="IPedidosParadosNoGestorDaFatia"/>): roda no <c>AppDbContext</c> e na transação de quem chamou (ligar a ausência, trocar, tirar ou desligar o gestor, trocar a pessoa de setor), então o fato e a mudança dos pedidos se desfazem juntos.
/// Só mexe no pedido <c>COM_O_GESTOR</c> (<c>SOLICITADA</c>, daquele <c>ID_GESTOR</c> e sem resultado dele): o que espera a resposta da pessoa a uma sugestão (<c>SUGERIU_DATA</c>) <b>não muda</b> (o contrato). <b>Aprovar a etapa do gestor</b> grava o resultado e a data juntos
/// (<c>ETAPA_GESTOR_AUTOMATICA</c>); <b>passar ao RH</b> tira o <c>ID_GESTOR</c> (o pedido fica sem etapa do gestor). Nenhum e-mail sai (a aprovação automática não é decisão de ninguém) e um evento resumo por chamada diz quantos pedidos andaram.
/// </summary>
public sealed class PedidosParadosNoGestorDasFerias(AppDbContext db, IRelogio relogio, IAuditor auditor) : IPedidosParadosNoGestorDaFatia
{
    public async Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(relogio.AgoraUtc);
        ResultadoDoGestorNasFerias? aprovou = ResultadoDoGestorNasFerias.Aprovou;

        // AUDITORIA-OK: não há evento por pedido: a aprovação automática não é decisão de ninguém (o ligar da ausência é auditado pela fatia 1) e esta chamada registra um evento resumo com a quantidade. Resultado e data da etapa vão juntos (CHECK).
        var aprovados = await ParadosNoGestor(gestorId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ResultadoDoGestor, aprovou).SetProperty(p => p.EtapaDoGestorEmUtc, agora).SetProperty(p => p.EtapaDoGestorAutomatica, true), ct);
        await ResumirAsync(aprovados, "aprovou sozinha (gestor ausente) a etapa do gestor de", ct);
        return new PedidosAprovados(0, aprovados);
    }

    public async Task PassarParaORhAsync(int gestorId, CancellationToken ct)
    {
        // AUDITORIA-OK: a passagem ao RH não é decisão de ninguém e a mudança de gestor já é auditada pela fatia 1; esta chamada registra um evento resumo com a quantidade.
        var passados = await ParadosNoGestor(gestorId).ExecuteUpdateAsync(s => s.SetProperty(p => p.GestorId, (int?)null), ct);
        await ResumirAsync(passados, "passou para a etapa do RH", ct);
    }

    public async Task PassarParaORhOsDaPessoaAsync(int funcionarioId, CancellationToken ct)
    {
        // AUDITORIA-OK: a passagem ao RH não é decisão de ninguém e a troca de setor já é auditada pela edição da ficha; esta chamada registra um evento resumo com a quantidade.
        var passados = await db.PedidosDeFerias
            .Where(p => p.FuncionarioId == funcionarioId && p.Status == StatusDasFerias.Solicitada && p.GestorId != null && p.ResultadoDoGestor == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.GestorId, (int?)null), ct);
        await ResumirAsync(passados, "passou para a etapa do RH, porque a pessoa trocou de setor,", ct);
    }

    private IQueryable<PedidoDeFerias> ParadosNoGestor(int gestorId) =>
        db.PedidosDeFerias.Where(p => p.Status == StatusDasFerias.Solicitada && p.GestorId == gestorId && p.ResultadoDoGestor == null);

    /// <summary>
    /// O evento resumo (um por chamada, só quando algum pedido andou). É GRAVADO aqui mesmo, dentro da transação de quem chamou: nem todo chamador salva depois de chamar a porta (a ausência do gestor e a troca do gestor do setor não salvam), e o evento ficaria na fila do contexto sem sair.
    /// </summary>
    private async Task ResumirAsync(int quantidade, string oQue, CancellationToken ct)
    {
        if (quantidade > 0)
        {
            auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ferias, AuditoriaAcao.Alterou, $"O sistema {oQue} {quantidade} pedido(s) de férias.", "Pedidos de férias"));
            await db.SaveChangesAsync(ct);
        }
    }
}
