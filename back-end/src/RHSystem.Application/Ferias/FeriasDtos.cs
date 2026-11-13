using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias;

// Os tipos de férias (contrato/fontes/70-fatia-7-gestao.yaml, components/schemas). O JSON é camelCase e os enums saem em MAIÚSCULAS_COM_SUBLINHADO; os nomes são os do contrato.

public sealed record FeriasPeriodoDatas(DateOnly Inicio, DateOnly Fim);

/// <summary>Um ano de casa com os dias de férias a que dá direito (<c>FeriasPeriodoAquisitivo</c>).</summary>
public sealed record FeriasPeriodoAquisitivo(
    DateOnly Inicio,
    DateOnly Fim,
    SituacaoDoPeriodo Situacao,
    int DiasDeDireito,
    int DiasTirados,
    int DiasEmPedido,
    int DiasDisponiveis,
    DateOnly PrazoParaTirar,
    int? MesesTrabalhados,
    DateOnly? DisponivelAPartirDe);

public sealed record FeriasProximas(DateOnly DataInicio, DateOnly DataFim, int Dias);

/// <summary>Os números do topo da tela de férias e da aba Férias da ficha.</summary>
public sealed record FeriasResumo(int DiasDisponiveis, FeriasPeriodoDatas? PeriodoDoSaldo, DateOnly? PrazoParaTirarSaldo, FeriasPeriodoDatas? PeriodoAquisitivoAtual, FeriasProximas? ProximasFerias, int PedidosEmAnalise);

/// <summary>A sugestão de outras datas do gestor, com a resposta da pessoa e o último dia para ela responder.</summary>
public sealed record FeriasSugestao(DateOnly DataInicio, DateOnly DataFim, int Dias, FuncionarioResumo SugeridaPor, DateTime SugeridaEmUtc, RespostaDaSugestao? RespostaDaPessoa, DateTime? RespondidaEmUtc, DateOnly ExpiraEm);

/// <summary>
/// Um pedido (ou registro) de férias no modelo único de duas etapas: <paramref name="Status"/> é o do banco, <paramref name="Etapa"/> diz onde o pedido está, <paramref name="Situacao"/> acrescenta o que o status não distingue e as decisões contam as duas etapas.
/// <paramref name="PodeDecidir"/> e <paramref name="MotivoNaoPodeDecidir"/> valem para <b>quem consulta</b>, pela tela do perfil dele.
/// </summary>
public sealed record FeriasPedido(
    int Id,
    FuncionarioResumo Funcionario,
    FeriasPeriodoDatas PeriodoAquisitivo,
    DateOnly DataInicio,
    DateOnly DataFim,
    int Dias,
    bool AbonoPecuniario,
    int DiasAbono,
    bool Adiantar13Salario,
    StatusDasFerias Status,
    EtapaDoPedido Etapa,
    bool PodeDecidir,
    string? MotivoNaoPodeDecidir,
    SituacaoDasFerias Situacao,
    DateTime SolicitadaEmUtc,
    string? Observacao,
    FuncionarioResumo? GestorResponsavel,
    DecisaoDeEtapa? DecisaoGestor,
    DecisaoDeEtapa? DecisaoRh,
    FeriasSugestao? Sugestao);

/// <summary>Uma verificação da lista de "vistos" do pedido (<c>FeriasRegraConferida</c>).</summary>
public sealed record FeriasRegraConferida(RegraDeFerias Codigo, bool Atendida, string Mensagem);

public sealed record FeriasConferencia(int TotalDias, int DiasQueSobram, bool PodeEnviar, DateOnly? PagamentoAte, IReadOnlyList<FeriasRegraConferida> Regras);

public sealed record FeriasEuResposta(FeriasResumo Resumo, FuncionarioResumo? GestorQueAnalisa, IReadOnlyList<FeriasPeriodoAquisitivo> PeriodosAquisitivos, IReadOnlyList<FeriasPedido> Historico);

/// <summary>O corpo da conferência: as duas datas e o abono (<c>diasAbono</c> 0 sem abono, de 1 a 10 com ele).</summary>
public record FeriasConferenciaComando
{
    public required DateOnly DataInicio { get; init; }

    public required DateOnly DataFim { get; init; }

    public bool AbonoPecuniario { get; init; }

    public int DiasAbono { get; init; }
}

/// <summary>O corpo do pedido de férias da própria pessoa: a conferência mais o 13º adiantado e a mensagem (até 500, texto).</summary>
public sealed record FeriasPedirComando : FeriasConferenciaComando
{
    public bool Adiantar13Salario { get; init; }

    public string? Mensagem { get; init; }
}
