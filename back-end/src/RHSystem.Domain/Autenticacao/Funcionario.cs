using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>FUNCIONARIOS</c> (as colunas que o login usa). A auditoria registra as propriedades marcadas com <c>[Auditar]</c>.</summary>
[Auditado(AuditoriaArea.Colaboradores, "Colaborador")]
public class Funcionario : IAuditavel
{
    private Funcionario()
    {
    }

    public int Id { get; private set; }

    [Auditar("Nome")]
    public string Nome { get; private set; } = string.Empty;

    [Auditar("E-mail corporativo")]
    public string? EmailCorporativo { get; private set; }

    [Auditar("Data de admissão")]
    public DateOnly DataAdmissao { get; private set; }

    [Auditar("Situação")]
    public bool Ativo { get; private set; } = true;

    /// <summary>Cinco dígitos (<c>00482</c>), gerada pelo sistema; sem matrícula ainda (por exemplo o administrador do seed), é nula.</summary>
    [Auditar("Matrícula")]
    public string? Matricula { get; private set; }

    /// <summary>A chave de <c>ID_NIVEL_ACESSO</c>; o <see cref="NivelDeAcesso"/> abaixo é a navegação. O log mostra o NOME do nível (a chave é para uma entidade <see cref="IAuditavel"/>).</summary>
    [Auditar("Nível de acesso")]
    public int? NivelDeAcessoId { get; private set; }

    public NivelDeAcesso? NivelDeAcesso { get; private set; }

    /// <summary>A data de nascimento é dado pessoal: o log diz que o campo mudou e nunca o valor.</summary>
    [Auditar("Data de nascimento", Sensivel = true)]
    public DateOnly? DataNascimento { get; private set; }

    /// <summary>O último dia de trabalho de quem foi desligado; nulo enquanto a pessoa está ativa.</summary>
    [Auditar("Último dia de trabalho")]
    public DateOnly? DataDesligamento { get; private set; }

    /// <summary>O gestor da pessoa (<c>ID_GESTOR</c>, de <c>GESTORES</c>): derivado do setor, por isso sem <c>[Auditar]</c> (a troca de setor já é auditada).</summary>
    public int? GestorId { get; private set; }

    /// <summary>Texto livre no banco; <see cref="TiposDeContrato"/> converte de e para o enum do contrato.</summary>
    [Auditar("Tipo de contrato")]
    public string? TipoContrato { get; private set; }

    public FuncionarioDetalhes? Detalhes { get; private set; }

    [Auditar("Cargo")]
    public int? CargoId { get; private set; }

    public Cargo? Cargo { get; private set; }

    [Auditar("Setor")]
    public int? SetorId { get; private set; }

    public Setor? Setor { get; private set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => Matricula is null ? $"Colaborador {Nome}" : $"Colaborador {Nome}, matrícula {Matricula}";

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
    /// Os dados do cadastro e da edição de uma vez. O e-mail vai sem espaços nas pontas e em minúsculas; <paramref name="gestorId"/> é o
    /// <c>ID_GESTOR</c> de <c>GESTORES</c> (nulo limpa). O nível entra só pela navegação: o EF acerta a chave na gravação.
    /// </summary>
    public void AtualizarCadastro(
        string nome,
        string emailCorporativo,
        DateOnly dataNascimento,
        DateOnly dataAdmissao,
        TipoDeContrato tipoContrato,
        int cargoId,
        int setorId,
        int? gestorId,
        NivelDeAcesso nivel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailCorporativo);
        ArgumentNullException.ThrowIfNull(nivel);
        Nome = nome;
        EmailCorporativo = emailCorporativo.Trim().ToLowerInvariant();
        DataNascimento = dataNascimento;
        DataAdmissao = dataAdmissao;
        TipoContrato = TiposDeContrato.ParaTexto(tipoContrato);
        CargoId = cargoId;
        SetorId = setorId;
        GestorId = gestorId;
        NivelDeAcesso = nivel;
    }

    /// <summary>Desligamento efetivado: a pessoa fica inativa e o último dia de trabalho vale a data (o trigger do banco exige as duas coisas juntas).</summary>
    public void Desligar(DateOnly ultimoDiaDeTrabalho)
    {
        DataDesligamento = ultimoDiaDeTrabalho;
        Ativo = false;
    }

    /// <summary>Volta a trabalhar: ativa, sem data de desligamento, com a nova admissão e o novo vínculo. Quem já está ativo não se readmite.</summary>
    public void Readmitir(DateOnly dataAdmissao, int cargoId, int setorId, TipoDeContrato tipoContrato, int? gestorId, NivelDeAcesso nivel)
    {
        if (Ativo)
        {
            throw new InvalidOperationException("Só quem está inativo pode ser readmitido.");
        }

        ArgumentNullException.ThrowIfNull(nivel);
        Ativo = true;
        DataDesligamento = null;
        DataAdmissao = dataAdmissao;
        CargoId = cargoId;
        SetorId = setorId;
        TipoContrato = TiposDeContrato.ParaTexto(tipoContrato);
        GestorId = gestorId;
        NivelDeAcesso = nivel;
    }

    /// <summary>Inativo se não está ativo; em admissão se a data de admissão ainda não chegou; senão ativo. <paramref name="hoje"/> é o dia no fuso do Brasil.</summary>
    public SituacaoCadastral Situacao(DateOnly hoje) => CalcularSituacao(Ativo, DataAdmissao, hoje);

    /// <summary>A mesma regra para quem lê só as colunas (as listas projetam sem carregar a entidade): inativo, em admissão ou ativo.</summary>
    public static SituacaoCadastral CalcularSituacao(bool ativo, DateOnly dataAdmissao, DateOnly hoje) =>
        !ativo ? SituacaoCadastral.Inativo
        : dataAdmissao > hoje ? SituacaoCadastral.EmAdmissao
        : SituacaoCadastral.Ativo;

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
