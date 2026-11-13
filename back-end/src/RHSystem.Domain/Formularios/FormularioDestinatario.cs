namespace RHSystem.Domain.Formularios;

/// <summary>
/// Quem deve responder o formulário (<c>FORMULARIOS_DESTINATARIOS</c>): uma linha por pessoa <b>ou</b> por setor (nunca os dois, nunca nenhum: <c>CHK_FORMULARIOS_DESTINATARIOS_PESSOA_OU_SETOR</c>), com o avaliado nas avaliações. A chave <c>CHAVE_DESTINO</c> é gerada pelo banco
/// (a unicidade de formulário, pessoa, setor e avaliado) e por isso não é mapeada.
/// </summary>
public class FormularioDestinatario
{
    private FormularioDestinatario()
    {
    }

    public int Id { get; private set; }

    public int FormularioId { get; private set; }

    public int? FuncionarioId { get; private set; }

    public int? SetorId { get; private set; }

    public int? AvaliadoId { get; private set; }

    public static FormularioDestinatario DaPessoa(int formularioId, int funcionarioId, int? avaliadoId = null) => new() { FormularioId = formularioId, FuncionarioId = funcionarioId, AvaliadoId = avaliadoId };

    public static FormularioDestinatario DoSetor(int formularioId, int setorId, int? avaliadoId = null) => new() { FormularioId = formularioId, SetorId = setorId, AvaliadoId = avaliadoId };
}
