namespace RHSystem.Domain.Colaboradores;

/// <summary>
/// Uma linha de <c>GESTORES</c>: o funcionário que é gestor. O setor dele está em <c>SETORES.ID_GESTOR</c> (a troca de gestor é da fatia 1b),
/// e a chave de aprovação automática (<c>APROVACAO_AUTOMATICA</c>) entra com a fatia 1c.
/// </summary>
public class Gestor
{
    private Gestor()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public static Gestor Criar(int funcionarioId) => new() { FuncionarioId = funcionarioId };
}
