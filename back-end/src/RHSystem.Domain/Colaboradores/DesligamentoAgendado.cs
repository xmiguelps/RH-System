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
/// As transições são as do <c>V001</c>: <c>AGENDADO</c> vira <c>EFETIVADO</c> (<see cref="Efetivar"/>) ou <c>CANCELADO</c> (<see cref="Cancelar"/>); <c>EFETIVADO</c> vira <c>REVERTIDO</c> (<see cref="Reverter"/>, na readmissão).
/// Cada instante é UTC e vem do relógio da aplicação (os <c>CHECK</c> do banco amarram o instante à situação).
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

    /// <summary>A pessoa foi desligada de fato (o <c>Funcionario.Desligar</c> acontece na mesma transação). Só de <c>AGENDADO</c>.</summary>
    /// <exception cref="InvalidOperationException">Não está <c>AGENDADO</c>.</exception>
    public void Efetivar(DateTime agoraUtc)
    {
        Exigir(SituacaoDoDesligamento.Agendado, agoraUtc, "efetivado");
        Situacao = SituacaoDoDesligamento.Efetivado;
        EfetivadoEm = agoraUtc;
    }

    /// <summary>O RH desistiu antes de efetivar. Só de <c>AGENDADO</c>.</summary>
    /// <exception cref="InvalidOperationException">Não está <c>AGENDADO</c>.</exception>
    public void Cancelar(DateTime agoraUtc)
    {
        Exigir(SituacaoDoDesligamento.Agendado, agoraUtc, "cancelado");
        Situacao = SituacaoDoDesligamento.Cancelado;
        CanceladoEm = agoraUtc;
    }

    /// <summary>A readmissão encerrou um desligamento efetivado. Só de <c>EFETIVADO</c>; <see cref="EfetivadoEm"/> fica (o <c>CHECK</c> do <c>V001</c> exige: <c>EFETIVADO</c> e <c>REVERTIDO</c> têm o instante da efetivação).</summary>
    /// <exception cref="InvalidOperationException">Não está <c>EFETIVADO</c>.</exception>
    public void Reverter(DateTime agoraUtc)
    {
        Exigir(SituacaoDoDesligamento.Efetivado, agoraUtc, "revertido");
        Situacao = SituacaoDoDesligamento.Revertido;
        RevertidoEm = agoraUtc;
    }

    private void Exigir(SituacaoDoDesligamento necessaria, DateTime agoraUtc, string verbo)
    {
        if (agoraUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O instante precisa ter Kind = Utc.", nameof(agoraUtc));
        }

        if (Situacao != necessaria)
        {
            throw new InvalidOperationException($"Só um desligamento {necessaria.ToString().ToUpperInvariant()} pode ser {verbo} (este está {Situacao.ToString().ToUpperInvariant()}).");
        }
    }
}
