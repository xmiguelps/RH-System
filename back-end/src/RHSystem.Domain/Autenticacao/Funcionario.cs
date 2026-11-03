namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>FUNCIONARIOS</c> (as colunas que o login usa).</summary>
public class Funcionario
{
    private Funcionario()
    {
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string? EmailCorporativo { get; private set; }

    public DateOnly DataAdmissao { get; private set; }

    public bool Ativo { get; private set; } = true;

    public NivelDeAcesso? NivelDeAcesso { get; private set; }

    public Cargo? Cargo { get; private set; }

    public Setor? Setor { get; private set; }

    /// <summary>O perfil vem do nome do nível de acesso; sem nível, ou com nível desconhecido, é nulo (e o login recusa).</summary>
    public Perfil? Perfil => Perfis.PerfilPorNome(NivelDeAcesso?.Nome);

    /// <summary>Cria um funcionário ativo. O e-mail é guardado sem espaços nas pontas e em minúsculas.</summary>
    public static Funcionario Criar(string nome, string emailCorporativo, DateOnly dataAdmissao, NivelDeAcesso nivel) =>
        new()
        {
            Nome = nome,
            EmailCorporativo = emailCorporativo.Trim().ToLowerInvariant(),
            DataAdmissao = dataAdmissao,
            NivelDeAcesso = nivel,
        };

    public void Inativar() => Ativo = false;
}
