namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>SETORES</c>. Só leitura por enquanto: nome, id e o gestor do setor (a troca de gestor é da fatia 1b).</summary>
public class Setor
{
    private Setor()
    {
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    /// <summary><c>GESTORES.ID_GESTOR</c> do gestor responsável pelo setor; um setor pode estar sem gestor.</summary>
    public int? GestorId { get; private set; }
}
