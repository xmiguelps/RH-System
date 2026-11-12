namespace RHSystem.Application.Ponto;

/// <summary>
/// A porta do recálculo do banco de horas de UM dia de UMA pessoa: o que muda o dia depois de consolidado (aprovação de um pedido de ajuste, correção direta do RH, atestado validado) a chama. A consolidação diária (tarefa agendada) também a usa para cada pessoa.
/// </summary>
public interface IRecalculoDoBancoDeHoras
{
    Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct);
}

/// <summary>
/// O último dia consolidado do banco de horas: o dia da maior tarefa <c>CONSOLIDAR_BANCO_HORAS</c> concluída (<c>null</c> enquanto nenhuma rodou). O extrato o mostra em <c>atualizadoAte</c> e a efetivação do desligamento só vale depois dele.
/// </summary>
public interface IUltimoDiaConsolidado
{
    Task<DateOnly?> ObterAsync(CancellationToken ct);
}
