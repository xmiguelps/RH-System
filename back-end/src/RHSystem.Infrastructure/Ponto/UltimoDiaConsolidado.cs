using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Agendamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>O dia da maior tarefa <c>CONSOLIDAR_BANCO_HORAS</c> concluída. A limpeza de tarefas só apaga as concluídas com mais de 30 dias e a consolidação é diária, então a última sempre está lá.</summary>
public sealed class UltimoDiaConsolidado(AppDbContext db) : IUltimoDiaConsolidado
{
    public async Task<DateOnly?> ObterAsync(CancellationToken ct) =>
        await db.TarefasAgendadas.AsNoTracking()
            .Where(t => t.Tipo == ConsolidarBancoDeHorasTarefa.TipoDaTarefa && t.Situacao == SituacaoDaTarefa.Concluida)
            .MaxAsync(t => t.DiaReferencia, ct);
}
