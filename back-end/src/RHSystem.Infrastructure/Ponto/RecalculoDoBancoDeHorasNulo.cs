using RHSystem.Application.Ponto;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>O recálculo que ainda não existe: não faz nada. A consolidação do banco de horas (tarefa agendada) registra a implementação de verdade no lugar desta.</summary>
public sealed class RecalculoDoBancoDeHorasNulo : IRecalculoDoBancoDeHoras
{
    public Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct) => Task.CompletedTask;
}
