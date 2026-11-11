using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O ponto na porta dos pedidos parados no gestor (<see cref="IPedidosParadosNoGestorDaFatia"/>): roda no <c>AppDbContext</c> e na transação de quem chamou (ligar a ausência, trocar, tirar ou desligar o gestor), então o fato e a passagem ao RH se desfazem
/// juntos. <b>Aprovar a etapa do gestor</b> marca como aprovados, sozinhos (<c>ETAPA_GESTOR_AUTOMATICA</c>), os pedidos <c>PENDENTE</c> daquele <c>ID_GESTOR</c> que ainda não têm resultado dele; <b>passar ao RH</b> tira o <c>ID_GESTOR</c> deles (a etapa vira a do
/// RH, sem gestor). Nenhum e-mail sai (a aprovação automática não é decisão de ninguém) e um evento resumo por chamada diz quantos pedidos andaram; o ligar e o desligar da ausência já são auditados pela fatia 1.
/// </summary>
public sealed class PedidosParadosNoGestorDoPonto(AppDbContext db, IRelogio relogio, IAuditor auditor) : IPedidosParadosNoGestorDaFatia
{
    public async Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)
    {
        var agora = MarcacaoDePonto.AoSegundo(relogio.AgoraUtc);

        // AUDITORIA-OK: não há evento por pedido: a aprovação automática não é decisão de ninguém (o ligar da ausência é auditado pela fatia 1) e esta chamada registra um evento resumo com a quantidade.
        var aprovados = await db.PedidosDeAjuste
            .Where(p => p.Status == StatusDoPedidoDeAjuste.Pendente && p.GestorId == gestorId && p.ResultadoDoGestor == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ResultadoDoGestor, ResultadoDaEtapaDoGestor.Aprovou).SetProperty(p => p.EtapaDoGestorEmUtc, agora).SetProperty(p => p.EtapaDoGestorAutomatica, true), ct);
        Resumir(aprovados, "aprovou sozinha (gestor ausente) a etapa do gestor de");
        return new PedidosAprovados(aprovados, 0);
    }

    public async Task PassarParaORhAsync(int gestorId, CancellationToken ct)
    {
        // AUDITORIA-OK: a passagem ao RH não é decisão de ninguém e a mudança de gestor já é auditada pela fatia 1; esta chamada registra um evento resumo com a quantidade.
        var passados = await db.PedidosDeAjuste
            .Where(p => p.Status == StatusDoPedidoDeAjuste.Pendente && p.GestorId == gestorId && p.ResultadoDoGestor == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.GestorId, (int?)null), ct);
        Resumir(passados, "passou para a etapa do RH");
    }

    private void Resumir(int quantidade, string oQue)
    {
        if (quantidade > 0)
        {
            auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ponto, AuditoriaAcao.Alterou, $"O sistema {oQue} {quantidade} pedido(s) de ajuste de ponto.", "Pedidos de ajuste de ponto"));
        }
    }
}
