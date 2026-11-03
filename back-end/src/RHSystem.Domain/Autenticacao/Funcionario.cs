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

    /// <summary>Cinco dígitos (<c>00482</c>), gerada pelo sistema; sem matrícula ainda (por exemplo o administrador do seed), é nula.</summary>
    public string? Matricula { get; private set; }

    public NivelDeAcesso? NivelDeAcesso { get; private set; }

    public int? CargoId { get; private set; }

    public Cargo? Cargo { get; private set; }

    public int? SetorId { get; private set; }

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

    /// <summary>
    /// Só aceita o formato que o <c>CHECK</c> do banco aceita: exatamente cinco dígitos ASCII (<c>^[0-9]{5}$</c>, sem espaço nem
    /// quebra de linha). Quem gera o número é o contador <c>SEQUENCIAS</c>.
    /// </summary>
    public void AtribuirMatricula(string matricula)
    {
        if (matricula is null || matricula.Length != 5 || !matricula.All(c => c is >= '0' and <= '9'))
        {
            throw new ArgumentException("A matrícula tem exatamente 5 dígitos (por exemplo 00482).", nameof(matricula));
        }

        Matricula = matricula;
    }
}
