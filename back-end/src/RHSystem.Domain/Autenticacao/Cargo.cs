namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>CARGOS</c>. Só leitura na fatia 0: nome e id bastam para o <c>cargo</c> de <c>UsuarioResposta</c>.</summary>
public class Cargo
{
    private Cargo()
    {
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;
}
