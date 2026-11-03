namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>SETORES</c>. Só leitura na fatia 0: nome e id bastam para o <c>setor</c> de <c>UsuarioResposta</c>.</summary>
public class Setor
{
    private Setor()
    {
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;
}
