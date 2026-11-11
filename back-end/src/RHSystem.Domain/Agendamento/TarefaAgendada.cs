using System.Text.RegularExpressions;

namespace RHSystem.Domain.Agendamento;

/// <summary>Texto no banco: <c>PENDENTE</c>, <c>EM_EXECUCAO</c>, <c>CONCLUIDA</c>, <c>FALHOU</c>, <c>CANCELADA</c>.</summary>
public enum SituacaoDaTarefa
{
    Pendente,
    EmExecucao,
    Concluida,
    Falhou,
    Cancelada,
}

/// <summary>
/// Uma linha de <c>TAREFAS_AGENDADAS</c>: o estado idempotente de uma tarefa do agendador. A chave de idempotência é única e começa por
/// <c>TIPO:</c> (ASCII). <see cref="Criar"/> aplica as invariantes dos <c>CHECK</c> e recebe o instante de fora.
/// </summary>
public partial class TarefaAgendada
{
    private TarefaAgendada()
    {
    }

    public long Id { get; private set; }

    public string Tipo { get; private set; } = string.Empty;

    public string ChaveDeIdempotencia { get; private set; } = string.Empty;

    public int? ReferenciaId { get; private set; }

    public DateOnly? DiaReferencia { get; private set; }

    /// <summary>JSON dos parâmetros; nunca com dado pessoal.</summary>
    public string? Parametros { get; private set; }

    public SituacaoDaTarefa Situacao { get; private set; }

    public DateTime ExecutarApos { get; private set; }

    public int Tentativas { get; private set; }

    public DateTime? ReservadaAte { get; private set; }

    public DateTime? UltimaTentativaEm { get; private set; }

    public DateTime? ConcluidaEm { get; private set; }

    public string? UltimoErro { get; private set; }

    public DateTime CriadaEm { get; private set; }

    public static TarefaAgendada Criar(
        string tipo,
        string chaveDeIdempotencia,
        DateTime agoraUtc,
        int? referenciaId = null,
        DateOnly? diaReferencia = null,
        string? parametros = null,
        DateTime? executarApos = null)
    {
        // CHK_TAREFAS_AGENDADAS_TIPO
        if (tipo is null || !NomeDeTipo().IsMatch(tipo))
        {
            throw new ArgumentException("O tipo é um nome em maiúsculas de 3 a 50 caracteres (por exemplo EFETIVAR_DESLIGAMENTO).", nameof(tipo));
        }

        // CHK_TAREFAS_AGENDADAS_CHAVE_COM_TIPO, e a coluna é ASCII (varchar(190))
        if (chaveDeIdempotencia is null || chaveDeIdempotencia.Length > 190 || !chaveDeIdempotencia.StartsWith(tipo + ":", StringComparison.Ordinal) || chaveDeIdempotencia.Any(c => c > 127))
        {
            throw new ArgumentException("A chave de idempotência é ASCII, tem até 190 caracteres e começa por \"TIPO:\".", nameof(chaveDeIdempotencia));
        }

        return new TarefaAgendada
        {
            Tipo = tipo,
            ChaveDeIdempotencia = chaveDeIdempotencia,
            ReferenciaId = referenciaId,
            DiaReferencia = diaReferencia,
            Parametros = parametros,
            Situacao = SituacaoDaTarefa.Pendente,
            ExecutarApos = executarApos ?? agoraUtc,
            Tentativas = 0,
            CriadaEm = agoraUtc,
        };
    }

    /// <summary>O tamanho de <c>ULTIMO_ERRO</c>.</summary>
    public const int TamanhoMaximoDoUltimoErro = 500;

    /// <summary>
    /// Uma execução começou: <c>EM_EXECUCAO</c> com a reserva (<c>CHK_TAREFAS_AGENDADAS_RESERVA</c>: reservada se e só se está <c>EM_EXECUCAO</c>), a hora da tentativa e uma
    /// tentativa a mais (aqui a tentativa conta quando COMEÇA, e não quando falha).
    /// </summary>
    public void Reservar(DateTime reservadaAteUtc, DateTime agoraUtc)
    {
        Situacao = SituacaoDaTarefa.EmExecucao;
        ReservadaAte = reservadaAteUtc;
        UltimaTentativaEm = agoraUtc;
        Tentativas++;
    }

    /// <summary><c>CONCLUIDA</c> com a hora (<c>CHK_TAREFAS_AGENDADAS_CONCLUIDA_EM</c>: hora se e só se concluída) e a reserva limpa, num <c>UPDATE</c> só.</summary>
    public void MarcarComoConcluida(DateTime agoraUtc)
    {
        Situacao = SituacaoDaTarefa.Concluida;
        ConcluidaEm = agoraUtc;
        ReservadaAte = null;
        UltimoErro = null;
    }

    /// <summary>Falhou e vai tentar de novo: volta a <c>PENDENTE</c> para <paramref name="executarAposUtc"/>, com a reserva LIMPA no mesmo <c>UPDATE</c> (toda saída de <c>EM_EXECUCAO</c> a limpa).</summary>
    public void AgendarNovaTentativa(DateTime executarAposUtc, string erro)
    {
        Situacao = SituacaoDaTarefa.Pendente;
        ExecutarApos = executarAposUtc;
        ReservadaAte = null;
        UltimoErro = Cortar(erro);
    }

    /// <summary>Desistiu (tentativas esgotadas, ou tipo sem executor): <c>FALHOU</c> com a reserva limpa.</summary>
    public void MarcarComoFalhou(string erro)
    {
        Situacao = SituacaoDaTarefa.Falhou;
        ReservadaAte = null;
        UltimoErro = Cortar(erro);
    }

    private static string Cortar(string erro) => erro.Length <= TamanhoMaximoDoUltimoErro ? erro : erro[..TamanhoMaximoDoUltimoErro];

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{2,49}\z")]
    private static partial Regex NomeDeTipo();
}
