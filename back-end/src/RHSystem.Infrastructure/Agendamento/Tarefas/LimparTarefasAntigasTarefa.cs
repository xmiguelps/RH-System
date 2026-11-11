using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Agendamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Agendamento.Tarefas;

/// <summary>
/// <c>LIMPAR_TAREFAS</c>: apaga da própria tabela do agendador o que já terminou há mais de <c>Retencao:TarefasDias</c>: <c>CONCLUIDA</c> (pela hora da conclusão) e
/// <c>CANCELADA</c> (pela criação). <b>Nunca</b> apaga <c>PENDENTE</c>, <c>EM_EXECUCAO</c> nem <c>FALHOU</c> (a falha fica para alguém ver). A recuperação agenda uma por dia
/// (<c>LIMPAR_TAREFAS:dia=AAAA-MM-DD</c>, o dia de hoje em Brasília): rodar duas vezes no mesmo dia é inofensivo, e depois de uma parada basta a de hoje.
/// </summary>
public sealed class LimparTarefasAntigasTarefa(AppDbContext db, IAgendador agendador, IRelogio relogio, OpcoesDeRetencao retencao) : ITarefaAgendada
{
    public const string TipoDaTarefa = "LIMPAR_TAREFAS";

    public string Tipo => TipoDaTarefa;

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var dia = FusoBrasil.Hoje(agora);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), agora, DiaReferencia: dia), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var corte = relogio.AgoraUtc.AddDays(-retencao.TarefasDias);

        // AUDITORIA-OK: tabela técnica, sem registro por decisão (limpeza de retenção do que o próprio agendador já terminou)
        await db.TarefasAgendadas.Where(t => (t.Situacao == SituacaoDaTarefa.Concluida && t.ConcluidaEm < corte) || (t.Situacao == SituacaoDaTarefa.Cancelada && t.CriadaEm < corte)).ExecuteDeleteAsync(ct);
    }
}
