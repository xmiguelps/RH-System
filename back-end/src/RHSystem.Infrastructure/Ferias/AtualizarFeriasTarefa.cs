using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ferias;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ferias;

/// <summary>
/// <c>ATUALIZAR_FERIAS</c>: todo dia, na ordem: (1) <c>APROVADA</c> ou <c>EM_GOZO</c> cujo último dia já passou vira <c>CONCLUIDA</c> (inclusive o pedido que pulou o gozo porque a API ficou parada dias: o trigger aceita <c>APROVADA</c> direto para <c>CONCLUIDA</c>); (2) <c>APROVADA</c> que já começou vira
/// <c>EM_GOZO</c>; (3) <c>SOLICITADA</c> cujo início já passou (início MENOR que hoje) é cancelada sem decisão, MENOS o que espera a resposta da pessoa a uma sugestão (o início original não vale nesse caso: ele só cai pela expiração da sugestão); (4) a sugestão cujo prazo (<c>expiraEm</c>, o menor entre 5 dias
/// depois dela e 2 dias antes do início sugerido) já passou expira (<c>CANCELADA</c> com <c>EXPIROU</c>, a mesma operação da resposta tardia da pessoa). Só as transições que o trigger do banco aceita; cada passo é UM <c>UPDATE</c> condicional ao estado de partida, então rodar de novo (ou duas instâncias ao mesmo
/// tempo) não muda nada. Nenhum e-mail sai (a lista fechada só tem decisões de gestor e de RH) e um evento resumo por passo que andou sai como Sistema. A recuperação agenda a tarefa do dia (<c>ATUALIZAR_FERIAS:dia=AAAA-MM-DD</c>); uma execução recupera tudo o que ficou para trás (a conta é do estado, não do dia
/// perdido).
/// </summary>
public sealed class AtualizarFeriasTarefa(AppDbContext db, IFeriasRepositorio repositorio, IUnidadeDeTrabalho unidade, IAuditor auditor, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDaTarefa = "ATUALIZAR_FERIAS";

    public string Tipo => TipoDaTarefa;

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var dia = FusoBrasil.Hoje(agora);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), agora, DiaReferencia: dia), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);

        // AUDITORIA-OK: o sistema concluir as férias que já terminaram não tem ator humano: um evento resumo (Sistema) sai com a quantidade, e o ExecuteUpdate não passa pelo interceptor do EF.
        var concluidas = await db.PedidosDeFerias
            .Where(p => (p.Status == StatusDasFerias.Aprovada || p.Status == StatusDasFerias.EmGozo) && p.DataFim < hoje)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusDasFerias.Concluida), ct);

        // AUDITORIA-OK: entrar em gozo é o relógio andando, sem ator humano: um evento resumo (Sistema) sai com a quantidade, e o ExecuteUpdate não passa pelo interceptor do EF.
        var emGozo = await db.PedidosDeFerias
            .Where(p => p.Status == StatusDasFerias.Aprovada && p.DataInicio <= hoje && p.DataFim >= hoje)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusDasFerias.EmGozo), ct);

        // AUDITORIA-OK: cancelar o pedido que venceu sem decisão não tem ator humano: um evento resumo (Sistema) sai com a quantidade, e o ExecuteUpdate não passa pelo interceptor do EF. A sugestão pendente fica de fora (cai pela expiração dela).
        var canceladas = await db.PedidosDeFerias
            .Where(p => p.Status == StatusDasFerias.Solicitada && p.DataInicio < hoje && !(p.ResultadoDoGestor == ResultadoDoGestorNasFerias.SugeriuData && p.RespostaDaSugestao == null))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusDasFerias.Cancelada), ct);

        var expiradas = await ExpirarAsync(hoje, agora, ct);

        Resumir(concluidas, "concluiu {0} férias que já terminaram");
        Resumir(emGozo, "colocou {0} férias em gozo");
        Resumir(canceladas, "cancelou {0} pedido(s) de férias cujo início passou sem decisão");
        Resumir(expiradas, "expirou {0} sugestão(ões) de outra data sem resposta");
        await unidade.SalvarAsync(ct);
    }

    /// <summary>As sugestões pendentes cujo prazo passou: cada uma na sua transação, com a pessoa travada ANTES (a mesma ordem de travas de toda escrita de férias).</summary>
    private async Task<int> ExpirarAsync(DateOnly hoje, DateTime agora, CancellationToken ct)
    {
        var pendentes = await db.PedidosDeFerias.AsNoTracking()
            .Where(p => p.Status == StatusDasFerias.Solicitada && p.ResultadoDoGestor == ResultadoDoGestorNasFerias.SugeriuData && p.RespostaDaSugestao == null)
            .Select(p => new { p.Id, p.FuncionarioId, p.EtapaDoGestorEmUtc, p.SugestaoDataInicio })
            .ToListAsync(ct);

        var expiradas = 0;
        foreach (var pendente in pendentes.Where(p => p is { EtapaDoGestorEmUtc: not null, SugestaoDataInicio: not null } && MapeadorDoPedidoDeFerias.ExpiraEm(p.EtapaDoGestorEmUtc.Value, p.SugestaoDataInicio.Value) < hoje))
        {
            var expirou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    _ = await repositorio.TravarPessoaAsync(pendente.FuncionarioId, ct);
                    return await repositorio.ExpirarSugestaoAsync(pendente.Id, pendente.FuncionarioId, agora, ct);
                },
                ct);
            expiradas += expirou ? 1 : 0;
        }

        return expiradas;
    }

    private void Resumir(int quantidade, string modelo)
    {
        if (quantidade > 0)
        {
            auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ferias, AuditoriaAcao.Alterou, $"O sistema {string.Format(System.Globalization.CultureInfo.InvariantCulture, modelo, quantidade)}.", "Pedidos de férias"));
        }
    }
}
