namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>NIVEIS_DE_ACESSO</c>.</summary>
public class NivelDeAcesso
{
    private NivelDeAcesso()
    {
    }

    public NivelDeAcesso(string nome)
    {
        Nome = nome;
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;
}
