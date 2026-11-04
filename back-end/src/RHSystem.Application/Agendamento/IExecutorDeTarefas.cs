namespace RHSystem.Application.Agendamento;

/// <summary>
/// O que o serviço em segundo plano chama. Os testes e as fatias o chamam direto, com o relógio falso, para provar o que uma tarefa faz sem esperar o relógio de verdade.
/// </summary>
public interface IExecutorDeTarefas
{
    /// <summary>Chama <see cref="ITarefaAgendada.RecuperarAsync"/> de cada tipo; devolve quantos tipos recuperou. Um tipo que lança é registrado e não impede os outros.</summary>
    Task<int> RecuperarAsync(CancellationToken ct);

    /// <summary>Reserva as tarefas vencidas e executa cada uma; devolve quantas tratou (reservou) neste ciclo.</summary>
    Task<int> ExecutarCicloAsync(CancellationToken ct);
}
