using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

// Os tipos do pedido de ajuste de ponto (contrato/fontes/30-fatia-3-ponto.yaml, components/schemas). O JSON é camelCase e os enums saem em MAIÚSCULAS_COM_SUBLINHADO.

/// <summary>Uma batida pedida (nova, com <paramref name="MarcacaoId"/> nulo, ou a correção de uma existente).</summary>
public sealed record PontoPedidoItem(int Id, int? MarcacaoId, TipoDeMarcacao Tipo, DateTime InstanteUtc);

/// <summary>
/// Um pedido de ajuste com tudo o que os modais mostram. <paramref name="Status"/> é o do banco; <paramref name="Etapa"/> diz onde o pedido está e <paramref name="DecisaoGestor"/> e <paramref name="DecisaoRh"/> contam as duas etapas.
/// <paramref name="PodeDecidir"/> e <paramref name="MotivoNaoPodeDecidir"/> valem para <b>quem consulta</b>, pela tela do perfil dele.
/// </summary>
public sealed record PontoPedidoAjuste(
    int Id,
    FuncionarioResumo Funcionario,
    DateOnly Data,
    DateTime CriadoEmUtc,
    string Justificativa,
    StatusDoPedidoDeAjuste Status,
    EtapaDoPedido Etapa,
    FuncionarioResumo? GestorResponsavel,
    DecisaoDeEtapa? DecisaoGestor,
    DecisaoDeEtapa? DecisaoRh,
    IReadOnlyList<PontoPedidoItem> Itens,
    string ResumoDaCorrecao,
    IReadOnlyList<PontoPeriodo> PeriodosDoDia,
    bool PodeDecidir,
    string? MotivoNaoPodeDecidir);

/// <summary>Uma batida pedida no corpo de <c>criarPedidoDeAjuste</c>.</summary>
public sealed record PontoPedidoItemEnviar
{
    public required TipoDeMarcacao Tipo { get; init; }

    /// <summary>O horário que a pessoa digitou no dia, convertido de -03:00 para UTC (com <c>Z</c>).</summary>
    public required DateTime InstanteUtc { get; init; }

    /// <summary>A batida existente do mesmo dia a corrigir; vazio é batida nova.</summary>
    public int? MarcacaoId { get; init; }
}

/// <summary>O corpo de <c>criarPedidoDeAjuste</c>: o dia do ponto (já passado), a justificativa (5 a 500) e de 1 a 10 batidas.</summary>
public sealed record PontoPedidoAjusteCriar
{
    public required DateOnly Data { get; init; }

    public required string Justificativa { get; init; }

    public required IReadOnlyList<PontoPedidoItemEnviar> Itens { get; init; }
}

/// <summary>O corpo das decisões (aprovar e recusar): só a observação, opcional, até 500 caracteres.</summary>
public sealed record PontoPedidoDecisao(string? Observacao = null);

/// <summary>Quantos pedidos estão em análise por etapa (independe da página e dos filtros, mas respeita o escopo do perfil).</summary>
public sealed record PontoPedidoFilaResumo(int ComGestor, int ComRh);

/// <summary>Uma página de pedidos para o gestor e o RH, com a contagem por etapa.</summary>
public sealed record PontoPedidoAjusteFilaPagina(IReadOnlyList<PontoPedidoAjuste> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, PontoPedidoFilaResumo Resumo);

/// <summary>Uma batida da lista de <c>corrigirMarcacoesDoDia</c>: com <paramref name="Id"/> é a existente do dia; sem ele é nova.</summary>
public sealed record PontoDiaCorrigirMarcacao
{
    public int? Id { get; init; }

    public required TipoDeMarcacao Tipo { get; init; }

    public required DateTime InstanteUtc { get; init; }
}

/// <summary>O corpo de <c>corrigirMarcacoesDoDia</c>: a lista COMPLETA de batidas que o dia deve ter depois da correção (até 20); substitui a atual.</summary>
public sealed record PontoDiaCorrigir
{
    public required IReadOnlyList<PontoDiaCorrigirMarcacao> Marcacoes { get; init; }
}
