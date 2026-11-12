using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Gestores;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// As gravações do pedido de ajuste. O pedido nasce na transação em que a pessoa já foi travada (a mesma trava do bater ponto): o "um pedido pendente por dia" é conferido antes e a chave <c>UQ_PONTOS_MANUTENCOES_UM_PENDENTE</c> ainda vira o 409 do
/// contrato se duas criações escaparem da conferência.
/// </summary>
public sealed class PedidosDeAjusteRepositorio(AppDbContext db) : IPedidosDeAjusteRepositorio
{
    public async Task<bool> TemPendenteNoDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct) =>
        await (
            from pedido in db.PedidosDeAjuste.AsNoTracking()
            join ponto in db.PontosDosDias.AsNoTracking() on pedido.PontoId equals ponto.Id
            where pedido.FuncionarioId == funcionarioId && ponto.Dia == dia && pedido.Status == StatusDoPedidoDeAjuste.Pendente
            select pedido.Id)
            .AnyAsync(ct);

    public async Task<int> CriarAsync(int funcionarioId, DateOnly dia, string justificativa, DateTime agoraUtc, EtapaInicialDoPedido etapa, IReadOnlyList<ItemParaAplicar> itens, CancellationToken ct)
    {
        try
        {
            var ponto = await db.PontosDosDias.FirstOrDefaultAsync(p => p.FuncionarioId == funcionarioId && p.Dia == dia, ct);
            if (ponto is null)
            {
                ponto = PontoDoDia.Novo(funcionarioId, dia);
                db.PontosDosDias.Add(ponto);
                await db.SaveChangesAsync(ct);
            }

            var pedido = PedidoDeAjuste.Criar(
                ponto.Id,
                funcionarioId,
                justificativa,
                agoraUtc,
                etapa.GestorId,
                itens.Select(i => ItemDoPedidoDeAjuste.Novo(ponto.Id, i.MarcacaoId, i.InstanteUtc, i.Tipo)),
                etapa.EtapaDoGestorAprovadaPorAusencia);
            db.PedidosDeAjuste.Add(pedido);
            await db.SaveChangesAsync(ct);
            return pedido.Id;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CriarPedido) is { } problema)
        {
            throw problema;
        }
    }

    public async Task<bool> DecidirComoGestorAsync(int pedidoId, int gestorId, bool aprovar, DateTime agoraUtc, string? observacao, CancellationToken ct)
    {
        var agora = MarcacaoDePonto.AoSegundo(agoraUtc);
        var naEtapaDoGestor = db.PedidosDeAjuste.Where(p => p.Id == pedidoId && p.Status == StatusDoPedidoDeAjuste.Pendente && p.GestorId == gestorId && p.ResultadoDoGestor == null);

        // AUDITORIA-OK: a decisão do gestor é auditada à mão pelo caso de uso (evento Aprovou ou Recusou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF.
        var linhas = aprovar
            ? await naEtapaDoGestor.ExecuteUpdateAsync(
                s => s.SetProperty(p => p.ResultadoDoGestor, ResultadoDaEtapaDoGestor.Aprovou).SetProperty(p => p.EtapaDoGestorEmUtc, agora).SetProperty(p => p.Observacao, observacao),
                ct)
            // AUDITORIA-OK: a recusa do gestor é auditada à mão pelo caso de uso (evento Recusou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF.
            : await naEtapaDoGestor.ExecuteUpdateAsync(
                s => s.SetProperty(p => p.Status, StatusDoPedidoDeAjuste.Rejeitada)
                    .SetProperty(p => p.ResultadoDoGestor, ResultadoDaEtapaDoGestor.Recusou)
                    .SetProperty(p => p.EtapaDoGestorEmUtc, agora)
                    .SetProperty(p => p.AnalisadoEmUtc, agora)
                    .SetProperty(p => p.Observacao, observacao),
                ct);
        return linhas == 1;
    }

    public async Task<int?> GestorIdDaPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Gestores.AsNoTracking().Where(g => g.FuncionarioId == funcionarioId).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct);

    public async Task<bool> DecidirComoRhAsync(int pedidoId, bool aprovar, int funcionarioRhId, int? gestorIdDoRh, DateTime agoraUtc, string? observacao, CancellationToken ct)
    {
        var agora = MarcacaoDePonto.AoSegundo(agoraUtc);
        var status = aprovar ? StatusDoPedidoDeAjuste.Aprovada : StatusDoPedidoDeAjuste.Rejeitada;
        var naEtapaDoRh = db.PedidosDeAjuste.Where(p => p.Id == pedidoId && p.Status == StatusDoPedidoDeAjuste.Pendente && (p.GestorId == null || p.ResultadoDoGestor == ResultadoDaEtapaDoGestor.Aprovou));

        try
        {
            // AUDITORIA-OK: a decisão do RH é auditada à mão pelo caso de uso (evento Aprovou ou Recusou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF.
            var linhas = await naEtapaDoRh.ExecuteUpdateAsync(
                s => s.SetProperty(p => p.Status, status)
                    .SetProperty(p => p.FuncionarioRhId, funcionarioRhId)
                    .SetProperty(p => p.ConfirmadoPeloRhEmUtc, agora)
                    .SetProperty(p => p.AnalisadoEmUtc, agora)
                    .SetProperty(p => p.GestorId, p => p.GestorId ?? gestorIdDoRh)
                    .SetProperty(p => p.Observacao, observacao),
                ct);
            return linhas == 1;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.DecidirPedido) is { } problema)
        {
            throw problema;
        }
        catch (MySqlConnector.MySqlException erro) when (TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.DecidirPedido) is { } problema)
        {
            throw problema; // o ExecuteUpdate não embrulha o erro do MySQL em DbUpdateException
        }
    }

    public async Task AplicarBatidasAsync(int funcionarioId, DateOnly dia, IReadOnlyList<BatidaDepoisDeAplicar> batidas, DateTime agoraUtc, CancellationToken ct)
    {
        var ponto = await db.PontosDosDias.AsNoTracking().FirstAsync(p => p.FuncionarioId == funcionarioId && p.Dia == dia, ct);
        var existentes = await db.Marcacoes.Where(m => m.PontoId == ponto.Id).ToDictionaryAsync(m => m.Id, ct);
        foreach (var batida in batidas.Where(b => b.Mudou))
        {
            if (batida.Id is { } id)
            {
                existentes[id].CorrigirPeloRh(batida.InstanteUtc, batida.Tipo, agoraUtc);
            }
            else
            {
                db.Marcacoes.Add(MarcacaoDePonto.NovaCorrigidaPeloRh(ponto.Id, batida.InstanteUtc, batida.Tipo, agoraUtc));
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.DecidirPedido) is { } problema)
        {
            throw problema;
        }
    }

    public async Task<string?> EmailCorporativoAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(f => f.EmailCorporativo).FirstOrDefaultAsync(ct);
}
