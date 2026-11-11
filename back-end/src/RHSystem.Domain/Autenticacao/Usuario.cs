namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>USUARIOS</c>: o acesso de um funcionário ao sistema (a senha fica só como hash).</summary>
public class Usuario
{
    private Usuario()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public Funcionario Funcionario { get; private set; } = null!;

    public string SenhaHash { get; private set; } = string.Empty;

    public bool Ativo { get; private set; } = true;

    public DateTime? UltimoLoginEm { get; private set; }

    /// <summary>Verdadeira enquanto a pessoa ainda não escolheu a senha (convite não aceito): o hash guardado não confere com nenhuma senha.</summary>
    public bool SenhaProvisoria { get; private set; }

    /// <summary>Só entra quem tem o usuário ativo e o funcionário ativo.</summary>
    public bool PodeEntrar => Ativo && Funcionario.Ativo;

    public static Usuario Criar(Funcionario funcionario, string senhaHash, bool senhaProvisoria = false) =>
        new()
        {
            Funcionario = funcionario,
            FuncionarioId = funcionario.Id,
            SenhaHash = senhaHash,
            SenhaProvisoria = senhaProvisoria,
        };

    /// <summary>A pessoa escolheu a senha (pelo link de convite ou de redefinição, ou trocando a própria): deixa de ser provisória.</summary>
    public void DefinirSenha(string novoHash)
    {
        SenhaHash = novoHash;
        SenhaProvisoria = false;
    }

    /// <summary>
    /// Grava o instante do login truncado ao segundo: <c>USUARIOS.DATA_HORA_ULTIMO_LOGIN</c> é <c>DATETIME</c> sem fração e o MySQL
    /// pode arredondar a fração em vez de descartá-la (12:42:00.999 viraria 12:42:01).
    /// </summary>
    public void RegistrarLogin(DateTime agoraUtc) =>
        UltimoLoginEm = new DateTime(agoraUtc.Ticks - (agoraUtc.Ticks % TimeSpan.TicksPerSecond), agoraUtc.Kind);

    public void Inativar() => Ativo = false;

    /// <summary>Libera o acesso de novo (o RH usa "Ativar" em Usuários do sistema; a readmissão também reativa). O trigger do banco só aceita se o funcionário estiver ativo.</summary>
    public void Reativar() => Ativo = true;
}
