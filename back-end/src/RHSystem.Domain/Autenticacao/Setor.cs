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

    /// <summary><paramref name="idGestor"/> é o <c>ID_GESTOR</c> de <c>GESTORES</c> (não o id do colaborador); nulo deixa o setor sem gestor.</summary>
    public void DefinirGestor(int? idGestor) => GestorId = idGestor;
}
