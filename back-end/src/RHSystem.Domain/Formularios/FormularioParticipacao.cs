namespace RHSystem.Domain.Formularios;

/// <summary>
/// O registro de participação em formulário ANÔNIMO (<c>FORMULARIOS_PARTICIPACOES</c>): diz que a pessoa respondeu (para não responder de novo e para o "quem falta"), <b>sem data, sem hora e sem ligação com as respostas</b>. A chave é um UUID v4 gerado pela aplicação (<c>Guid.NewGuid()</c>,
/// em minúsculas; o <c>UUID()</c> do MySQL é v1 e o <c>CHECK</c> recusa), devolvido à própria pessoa no recibo. A entidade não é auditada pelo interceptor. A coluna gerada <c>CHAVE_AVALIADO</c> não é mapeada.
/// </summary>
public class FormularioParticipacao
{
    private FormularioParticipacao()
    {
    }

    public Guid Id { get; private set; }

    public int FormularioId { get; private set; }

    public int FuncionarioId { get; private set; }

    public int? AvaliadoId { get; private set; }

    public static FormularioParticipacao Criar(int formularioId, int funcionarioId, int? avaliadoId) =>
        new() { Id = Guid.NewGuid(), FormularioId = formularioId, FuncionarioId = funcionarioId, AvaliadoId = avaliadoId };
}
