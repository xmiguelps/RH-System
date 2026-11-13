namespace RHSystem.Domain.Ferias;

/// <summary>
/// Um pedido (ou registro) de férias de UM período de gozo (<c>FERIAS</c>). Não há coluna de etapa: ela se deduz de <see cref="GestorId"/>, <see cref="ResultadoDoGestor"/> e <see cref="FuncionarioRhId"/> (tabela de estados do plano da fatia 7).
/// Os instantes são UTC na aplicação e -03:00 no banco, sempre pelo relógio da aplicação (os <c>NOW()</c> dos triggers usam o relógio real do banco). Nenhum padrão do banco é declarado (<c>STATUS_FERIAS</c>, abono, automática, 13º): o EF sempre envia o valor.
/// <b>As decisões não passam por esta classe</b>: são <c>UPDATE</c>s condicionais ao estado de partida (corrida entre duas pessoas), cada um gravando juntas as colunas que os <c>CHECK</c> em cadeia exigem. <see cref="Observacao"/> é a mensagem da pessoa (ou a observação do RH
/// no registro direto) e nunca é sobrescrita por decisão; o trigger de inativação acrescenta um sufixo a ela.
/// </summary>
public class PedidoDeFerias
{
    private PedidoDeFerias()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public DateOnly InicioDoPeriodoAquisitivo { get; private set; }

    public DateOnly FimDoPeriodoAquisitivo { get; private set; }

    public DateOnly DataInicio { get; private set; }

    public DateOnly DataFim { get; private set; }

    public bool AbonoPecuniario { get; private set; }

    public int DiasAbono { get; private set; }

    public StatusDasFerias Status { get; private set; }

    public DateTime? SolicitadaEmUtc { get; private set; }

    public DateTime? AnalisadaEmUtc { get; private set; }

    /// <summary>A linha de <c>GESTORES</c> da etapa do gestor, ou a de quem do RH decidiu um pedido sem etapa do gestor.</summary>
    public int? GestorId { get; private set; }

    public string? Observacao { get; private set; }

    public ResultadoDoGestorNasFerias? ResultadoDoGestor { get; private set; }

    public DateTime? EtapaDoGestorEmUtc { get; private set; }

    public bool EtapaDoGestorAutomatica { get; private set; }

    public int? FuncionarioRhId { get; private set; }

    public DateTime? ConfirmadaPeloRhEmUtc { get; private set; }

    public bool Adiantar13Salario { get; private set; }

    public DateOnly? SugestaoDataInicio { get; private set; }

    public DateOnly? SugestaoDataFim { get; private set; }

    public RespostaDaSugestao? RespostaDaSugestao { get; private set; }

    public DateTime? RespostaDaSugestaoEmUtc { get; private set; }

    public string? MotivoRecusa { get; private set; }

    /// <summary>
    /// O pedido da pessoa: nasce <c>SOLICITADA</c>. <paramref name="gestorId"/> é a linha de <c>GESTORES</c> da etapa do gestor (<c>null</c>: o pedido nasce sem ela e só o RH decide). Com <paramref name="etapaDoGestorAprovadaPorAusencia"/> o gestor está ausente:
    /// a etapa dele já nasce aprovada, automática, na hora da criação.
    /// </summary>
    public static PedidoDeFerias Solicitar(int funcionarioId, DateOnly inicioDoPeriodo, DateOnly fimDoPeriodo, DateOnly dataInicio, DateOnly dataFim, bool abonoPecuniario, int diasAbono, bool adiantar13Salario, string? mensagem, DateTime agoraUtc, int? gestorId, bool etapaDoGestorAprovadaPorAusencia = false)
    {
        if (etapaDoGestorAprovadaPorAusencia && gestorId is null)
        {
            throw new ArgumentException("A aprovação por ausência precisa do gestor ausente.", nameof(gestorId));
        }

        var ferias = Nova(funcionarioId, inicioDoPeriodo, fimDoPeriodo, dataInicio, dataFim, abonoPecuniario, diasAbono, adiantar13Salario, mensagem, agoraUtc, StatusDasFerias.Solicitada);
        ferias.GestorId = gestorId;
        if (etapaDoGestorAprovadaPorAusencia)
        {
            ferias.ResultadoDoGestor = ResultadoDoGestorNasFerias.Aprovou;
            ferias.EtapaDoGestorEmUtc = AoSegundo(agoraUtc);
            ferias.EtapaDoGestorAutomatica = true;
        }

        return ferias;
    }

    /// <summary>O registro direto do RH: nasce <c>APROVADA</c>, sem as duas etapas (gestor e RH ficam nulos).</summary>
    public static PedidoDeFerias Registrar(int funcionarioId, DateOnly inicioDoPeriodo, DateOnly fimDoPeriodo, DateOnly dataInicio, DateOnly dataFim, bool abonoPecuniario, int diasAbono, bool adiantar13Salario, string? observacao, DateTime agoraUtc) =>
        Nova(funcionarioId, inicioDoPeriodo, fimDoPeriodo, dataInicio, dataFim, abonoPecuniario, diasAbono, adiantar13Salario, observacao, agoraUtc, StatusDasFerias.Aprovada);

    private static PedidoDeFerias Nova(int funcionarioId, DateOnly inicioDoPeriodo, DateOnly fimDoPeriodo, DateOnly dataInicio, DateOnly dataFim, bool abonoPecuniario, int diasAbono, bool adiantar13Salario, string? observacao, DateTime agoraUtc, StatusDasFerias status) =>
        new()
        {
            FuncionarioId = funcionarioId,
            InicioDoPeriodoAquisitivo = inicioDoPeriodo,
            FimDoPeriodoAquisitivo = fimDoPeriodo,
            DataInicio = dataInicio,
            DataFim = dataFim,
            AbonoPecuniario = abonoPecuniario,
            DiasAbono = diasAbono,
            Adiantar13Salario = adiantar13Salario,
            Observacao = observacao,
            Status = status,
            SolicitadaEmUtc = AoSegundo(agoraUtc),
        };

    private static DateTime AoSegundo(DateTime instanteUtc)
    {
        if (instanteUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O instante precisa ter Kind = Utc.", nameof(instanteUtc));
        }

        return new DateTime(instanteUtc.Ticks - (instanteUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
    }
}
