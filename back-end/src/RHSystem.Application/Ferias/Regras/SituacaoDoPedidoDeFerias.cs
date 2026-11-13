using RHSystem.Application.Comum;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias.Regras;

/// <summary>As oito situações (<c>FeriasSituacao</c>): as três em análise e os cinco estados finais do status do banco.</summary>
public enum SituacaoDasFerias
{
    ComOGestor,
    SugestaoDeData,
    ComORh,
    Aprovada,
    EmGozo,
    Concluida,
    Rejeitada,
    Cancelada,
}

/// <summary>
/// O que o banco guarda de um pedido de férias e as regras precisam: o status, o gestor da etapa (<c>ID_GESTOR</c> e a pessoa dona dessa linha), o resultado do gestor e quando, se foi automática, quem do RH decidiu e quando, as datas e a resposta da sugestão e o motivo da recusa.
/// </summary>
public sealed record LinhaDoPedidoDeFerias(
    StatusDasFerias Status,
    int? GestorId,
    int? GestorFuncionarioId,
    ResultadoDoGestorNasFerias? ResultadoDoGestor,
    DateTime? EtapaDoGestorEmUtc,
    bool EtapaDoGestorAutomatica,
    int? FuncionarioRhId,
    DateTime? ConfirmadaPeloRhEmUtc,
    DateOnly? SugestaoInicio,
    DateOnly? SugestaoFim,
    RespostaDaSugestao? Resposta,
    DateTime? RespostaEmUtc,
    string? MotivoRecusa);

/// <summary>
/// A situação e a etapa, deduzidas das colunas (não há coluna de etapa nem de situação). <c>SOLICITADA</c>: com o gestor da etapa e sem resultado dele é <c>COM_O_GESTOR</c>; com <c>SUGERIU_DATA</c> e sem resposta da pessoa é <c>SUGESTAO_DE_DATA</c> (a etapa continua a do gestor);
/// em qualquer outro caso (gestor aprovou, sugestão aceita, ou pedido sem etapa do gestor) está <c>COM_O_RH</c>. Os outros status são finais e a etapa é <c>CONCLUIDA</c>.
/// </summary>
public static class SituacaoDoPedidoDeFerias
{
    public static SituacaoDasFerias Situacao(LinhaDoPedidoDeFerias linha) => linha.Status switch
    {
        StatusDasFerias.Solicitada => SituacaoEmAnalise(linha),
        StatusDasFerias.Aprovada => SituacaoDasFerias.Aprovada,
        StatusDasFerias.EmGozo => SituacaoDasFerias.EmGozo,
        StatusDasFerias.Concluida => SituacaoDasFerias.Concluida,
        StatusDasFerias.Rejeitada => SituacaoDasFerias.Rejeitada,
        _ => SituacaoDasFerias.Cancelada,
    };

    public static EtapaDoPedido Etapa(LinhaDoPedidoDeFerias linha) => Situacao(linha) switch
    {
        SituacaoDasFerias.ComOGestor or SituacaoDasFerias.SugestaoDeData => EtapaDoPedido.Gestor,
        SituacaoDasFerias.ComORh => EtapaDoPedido.Rh,
        _ => EtapaDoPedido.Concluida,
    };

    private static SituacaoDasFerias SituacaoEmAnalise(LinhaDoPedidoDeFerias linha)
    {
        if (linha.ResultadoDoGestor == ResultadoDoGestorNasFerias.SugeriuData && linha.Resposta is null)
        {
            return SituacaoDasFerias.SugestaoDeData;
        }

        return linha.GestorId is not null && linha.ResultadoDoGestor is null ? SituacaoDasFerias.ComOGestor : SituacaoDasFerias.ComORh;
    }
}
