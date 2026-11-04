using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>SETORES</c>. Só leitura por enquanto: nome, id e o gestor do setor (a troca de gestor é da fatia 1b).</summary>
[Auditado(AuditoriaArea.Cadastros, "Setor")]
public class Setor : IAuditavel
{
    private Setor()
    {
    }

    public int Id { get; private set; }

    [Auditar("Nome")]
    public string Nome { get; private set; } = string.Empty;

    /// <summary><c>GESTORES.ID_GESTOR</c> do gestor responsável pelo setor; um setor pode estar sem gestor.</summary>
    [Auditar("Gestor")]
    public int? GestorId { get; private set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Setor {Nome}";
}
