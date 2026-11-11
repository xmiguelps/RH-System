namespace RHSystem.Domain.Ponto;

/// <summary>
/// Um pedido de ajuste de ponto (<c>PONTOS_MANUTENCOES</c>): um dia por pedido, várias batidas em <see cref="Itens"/>. Não há coluna de etapa: ela se deduz de <see cref="GestorId"/> e <see cref="ResultadoDoGestor"/> (ver
/// <c>EtapaDoPedidoDeAjuste</c>). <see cref="ChavePendente"/> é calculada pelo banco (igual a <c>ID_PONTO</c> enquanto <c>PENDENTE</c>) e a chave única dela é o "um pedido pendente por dia".
/// </summary>
public class PedidoDeAjuste
{
    private readonly List<ItemDoPedidoDeAjuste> _itens = [];

    private PedidoDeAjuste()
    {
    }

    public int Id { get; private set; }

    public int PontoId { get; private set; }

    public int FuncionarioId { get; private set; }

    public string Justificativa { get; private set; } = string.Empty;

    public DateTime? SolicitadoEmUtc { get; private set; }

    public StatusDoPedidoDeAjuste Status { get; private set; }

    public DateTime? AnalisadoEmUtc { get; private set; }

    /// <summary>A linha de <c>GESTORES</c> responsável (a etapa do gestor) ou de quem do RH decidiu um pedido sem etapa do gestor.</summary>
    public int? GestorId { get; private set; }

    public string? Observacao { get; private set; }

    public ResultadoDaEtapaDoGestor? ResultadoDoGestor { get; private set; }

    public DateTime? EtapaDoGestorEmUtc { get; private set; }

    public bool EtapaDoGestorAutomatica { get; private set; }

    public int? FuncionarioRhId { get; private set; }

    public DateTime? ConfirmadoPeloRhEmUtc { get; private set; }

    /// <summary>Calculada no banco; nunca é gravada.</summary>
    public int? ChavePendente { get; private set; }

    public IReadOnlyList<ItemDoPedidoDeAjuste> Itens => _itens;

    public static PedidoDeAjuste Criar(int pontoId, int funcionarioId, string justificativa, DateTime agoraUtc, int? gestorId, IEnumerable<ItemDoPedidoDeAjuste> itens)
    {
        var pedido = new PedidoDeAjuste
        {
            PontoId = pontoId,
            FuncionarioId = funcionarioId,
            Justificativa = justificativa,
            SolicitadoEmUtc = MarcacaoDePonto.AoSegundo(agoraUtc),
            Status = StatusDoPedidoDeAjuste.Pendente,
            GestorId = gestorId,
        };
        pedido._itens.AddRange(itens);
        return pedido;
    }
}

/// <summary>Uma batida pedida (<c>PONTOS_MANUTENCOES_ITENS</c>): nova (<see cref="MarcacaoId"/> nulo) ou a correção de uma existente do mesmo dia.</summary>
public class ItemDoPedidoDeAjuste
{
    private ItemDoPedidoDeAjuste()
    {
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public int PontoId { get; private set; }

    public int? MarcacaoId { get; private set; }

    public DateTime InstanteProposto { get; private set; }

    public TipoDeMarcacao Tipo { get; private set; }

    public static ItemDoPedidoDeAjuste Novo(int pontoId, int? marcacaoId, DateTime instanteProposto, TipoDeMarcacao tipo) =>
        new() { PontoId = pontoId, MarcacaoId = marcacaoId, InstanteProposto = MarcacaoDePonto.AoSegundo(instanteProposto), Tipo = tipo };
}
