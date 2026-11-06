using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Colaboradores;

/// <summary>
/// Uma linha de <c>GESTORES</c>: o funcionário que é gestor. O setor dele está em <c>SETORES.ID_GESTOR</c> e o nível de acesso em <c>FUNCIONARIOS.ID_NIVEL_ACESSO</c> (nada duplicado). A ausência ("Gestor ausente") é
/// a chave <c>APROVACAO_AUTOMATICA</c> mais a data de volta <c>APROVACAO_AUTOMATICA_ATE</c>: <b>a aplicação decide se a chave vale</b> (<see cref="AusenteEm"/>), não há agendador que a desligue. A data de volta só existe
/// com a marca ligada (o <c>CHECK</c> do <c>V001</c>), então <see cref="DesmarcarAusencia"/> zera as duas.
/// </summary>
[Auditado(AuditoriaArea.Cadastros, "Gestor")]
public class Gestor : IAuditavel
{
    private Gestor()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    /// <summary>A pessoa, quando a consulta a traz (<c>Include</c>); sem ela o log diz "Gestor {id}".</summary>
    public Funcionario Funcionario { get; private set; } = null!;

    [Auditar("Gestor ausente")]
    public bool Ausente { get; private set; }

    /// <summary>O dia em que o gestor volta (a ausência vale ATÉ O DIA ANTERIOR); nulo com a marca ligada vale "até o RH desligar".</summary>
    [Auditar("Volta em")]
    public DateOnly? AusenteAte { get; private set; }

    public string NomeParaAuditoria => Funcionario?.Nome ?? $"Gestor {Id}";

    public string RegistroAfetado => $"Gestor {NomeParaAuditoria}";

    public static Gestor Criar(Funcionario funcionario)
    {
        ArgumentNullException.ThrowIfNull(funcionario);
        return new Gestor { Funcionario = funcionario, FuncionarioId = funcionario.Id };
    }

    /// <summary>Marcado como ausente e <paramref name="dia"/> ainda antes da volta (ou sem volta definida).</summary>
    public bool AusenteEm(DateOnly dia) => Ausente && (AusenteAte is null || dia < AusenteAte);

    /// <summary>Liga a ausência. <paramref name="ateDia"/> é o dia da volta: tem de ser DEPOIS de <paramref name="hoje"/>; nulo é "sem data de volta".</summary>
    /// <exception cref="ArgumentException">A volta é hoje ou antes.</exception>
    public void MarcarAusente(DateOnly? ateDia, DateOnly hoje)
    {
        if (ateDia is { } volta && volta <= hoje)
        {
            throw new ArgumentException("A data de volta tem de ser depois de hoje.", nameof(ateDia));
        }

        Ausente = true;
        AusenteAte = ateDia;
    }

    /// <summary>Desliga a ausência e zera a data (o banco não aceita a data com a marca desligada).</summary>
    public void DesmarcarAusencia()
    {
        Ausente = false;
        AusenteAte = null;
    }
}
