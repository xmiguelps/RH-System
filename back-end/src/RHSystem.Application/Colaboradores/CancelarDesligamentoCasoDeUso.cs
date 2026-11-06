namespace RHSystem.Application.Colaboradores;

/// <summary>
/// <c>cancelarDesligamentoAgendado</c>: cancela o desligamento que ainda está <c>AGENDADO</c> (<see cref="IDesligamentoDeFuncionarios.CancelarAsync"/>). 404 se a pessoa não existe; 422
/// <c>SEM_DESLIGAMENTO_AGENDADO</c> se não há o que cancelar (nunca houve ou já foi gravado: aí quem volta à empresa é readmitido).
/// </summary>
public sealed class CancelarDesligamentoCasoDeUso(IDesligamentoDeFuncionarios desligamentos)
{
    public Task ExecutarAsync(int funcionarioId, CancellationToken ct) => desligamentos.CancelarAsync(funcionarioId, ct);
}
