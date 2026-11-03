using System.Text.RegularExpressions;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Auditoria;

/// <summary>
/// Uma linha de <c>AUDITORIA_EVENTOS</c>: quem fez o quê, quando e de onde. O log é só de inclusão por regra da aplicação (nenhum caminho de
/// código atualiza ou apaga evento). <see cref="Criar"/> aplica as invariantes dos <c>CHECK</c> da tabela e recebe o instante de fora.
/// </summary>
public partial class EventoDeAuditoria
{
    private EventoDeAuditoria()
    {
    }

    public long Id { get; private set; }

    public DateTime Quando { get; private set; }

    public TipoDeAtor AtorTipo { get; private set; }

    public int? FuncionarioAtorId { get; private set; }

    public string AtorNome { get; private set; } = string.Empty;

    public Perfil? AtorPerfil { get; private set; }

    public AuditoriaArea Area { get; private set; }

    public AuditoriaAcao Acao { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    public string RegistroAfetado { get; private set; } = string.Empty;

    public string? Entidade { get; private set; }

    public long? IdRegistro { get; private set; }

    public CanalDeOrigem Canal { get; private set; }

    public string? Ip { get; private set; }

    public List<MudancaDeAuditoria> Mudancas { get; private set; } = [];

    public static EventoDeAuditoria Criar(
        DateTime agoraUtc,
        TipoDeAtor atorTipo,
        int? funcionarioAtorId,
        string atorNome,
        Perfil? atorPerfil,
        AuditoriaArea area,
        AuditoriaAcao acao,
        string descricao,
        string registroAfetado,
        string? entidade,
        long? idRegistro,
        CanalDeOrigem canal,
        string? ip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atorNome);
        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);
        ArgumentNullException.ThrowIfNull(registroAfetado);

        // CHK_AUDITORIA_EVENTOS_ATOR_PESSOA_OU_SISTEMA
        if (atorTipo == TipoDeAtor.Pessoa && funcionarioAtorId is null)
        {
            throw new ArgumentException("Um evento de pessoa tem o funcionário que agiu.", nameof(funcionarioAtorId));
        }

        if (atorTipo == TipoDeAtor.Sistema && (funcionarioAtorId is not null || atorPerfil is not null))
        {
            throw new ArgumentException("Um evento do sistema não tem funcionário nem perfil.", nameof(atorTipo));
        }

        // CHK_AUDITORIA_EVENTOS_REGISTRO e _ENTIDADE
        if ((entidade is null) != (idRegistro is null))
        {
            throw new ArgumentException("Entidade e id do registro vêm juntos ou ficam os dois vazios.", nameof(entidade));
        }

        if (entidade is not null && !NomeDeEntidade().IsMatch(entidade))
        {
            throw new ArgumentException("A entidade é um nome em maiúsculas (letras, números e sublinhado), como PONTOS_MARCACOES.", nameof(entidade));
        }

        // CHK_AUDITORIA_EVENTOS_CANAL_DO_ATOR e _IP_SO_NO_PORTAL
        if ((atorTipo == TipoDeAtor.Sistema) != (canal == CanalDeOrigem.Sistema))
        {
            throw new ArgumentException("O canal SISTEMA é só do ator sistema (e o ator sistema só usa o canal SISTEMA).", nameof(canal));
        }

        if (canal != CanalDeOrigem.Portal && ip is not null)
        {
            throw new ArgumentException("O IP só existe para eventos do portal.", nameof(ip));
        }

        return new EventoDeAuditoria
        {
            Quando = agoraUtc,
            AtorTipo = atorTipo,
            FuncionarioAtorId = funcionarioAtorId,
            AtorNome = atorNome,
            AtorPerfil = atorPerfil,
            Area = area,
            Acao = acao,
            Descricao = descricao,
            RegistroAfetado = registroAfetado,
            Entidade = entidade,
            IdRegistro = idRegistro,
            Canal = canal,
            Ip = ip,
        };
    }

    public void AdicionarMudanca(int ordem, string campo, string? valorAntes, string? valorDepois, bool valorOculto) =>
        Mudancas.Add(MudancaDeAuditoria.Criar(ordem, campo, valorAntes, valorDepois, valorOculto));

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*\z")]
    private static partial Regex NomeDeEntidade();
}
