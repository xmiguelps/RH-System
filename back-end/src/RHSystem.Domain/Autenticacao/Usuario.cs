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

    /// <summary>Só entra quem tem o usuário ativo e o funcionário ativo.</summary>
    public bool PodeEntrar => Ativo && Funcionario.Ativo;

    public static Usuario Criar(Funcionario funcionario, string senhaHash) =>
        new()
        {
            Funcionario = funcionario,
            FuncionarioId = funcionario.Id,
            SenhaHash = senhaHash,
        };

    /// <summary>
    /// Grava o instante do login truncado ao segundo: <c>USUARIOS.DATA_HORA_ULTIMO_LOGIN</c> é <c>DATETIME</c> sem fração e o MySQL
    /// pode arredondar a fração em vez de descartá-la (12:42:00.999 viraria 12:42:01).
    /// </summary>
    public void RegistrarLogin(DateTime agoraUtc) =>
        UltimoLoginEm = new DateTime(agoraUtc.Ticks - (agoraUtc.Ticks % TimeSpan.TicksPerSecond), agoraUtc.Kind);

    public void Inativar() => Ativo = false;
}
