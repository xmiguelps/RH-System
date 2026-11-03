namespace RHSystem.Domain.Colaboradores;

/// <summary>Texto no banco: <c>AGENDADO</c>, <c>EFETIVADO</c>, <c>CANCELADO</c>, <c>REVERTIDO</c>.</summary>
public enum SituacaoDoDesligamento
{
    Agendado,
    Efetivado,
    Cancelado,
    Revertido,
}

/// <summary>
/// Uma linha de <c>DESLIGAMENTOS_AGENDADOS</c>. O banco garante um desligamento vigente (<c>AGENDADO</c> ou <c>EFETIVADO</c>) por funcionário.
/// As transições (efetivar, cancelar, reverter) são da fatia 1b.
/// </summary>
public class DesligamentoAgendado
{
    private DesligamentoAgendado()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public DateOnly UltimoDiaTrabalho { get; private set; }

    public SituacaoDoDesligamento Situacao { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public DateTime? EfetivadoEm { get; private set; }

    public DateTime? CanceladoEm { get; private set; }

    public DateTime? RevertidoEm { get; private set; }

    public static DesligamentoAgendado Criar(int funcionarioId, DateOnly ultimoDiaTrabalho, DateTime agoraUtc) =>
        new()
        {
            FuncionarioId = funcionarioId,
            UltimoDiaTrabalho = ultimoDiaTrabalho,
            Situacao = SituacaoDoDesligamento.Agendado,
            CriadoEm = agoraUtc,
        };
}
