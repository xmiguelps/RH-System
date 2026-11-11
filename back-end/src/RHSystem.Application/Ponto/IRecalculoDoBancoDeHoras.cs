namespace RHSystem.Application.Ponto;

/// <summary>
/// A porta do recálculo do banco de horas de UM dia de UMA pessoa: o que muda o dia depois de consolidado (aprovação de um pedido de ajuste, correção direta do RH, atestado validado) a chama. A consolidação diária (tarefa agendada) é quem a implementa de verdade; até lá
/// a implementação não faz nada.
/// </summary>
public interface IRecalculoDoBancoDeHoras
{
    Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct);
}
