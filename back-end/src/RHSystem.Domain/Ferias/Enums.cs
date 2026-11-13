namespace RHSystem.Domain.Ferias;

/// <summary>Texto no banco (<c>FERIAS.STATUS_FERIAS</c>): os seis valores do <c>CHECK</c>. A etapa (gestor ou RH) NÃO é status: o pedido continua <c>SOLICITADA</c> até a decisão final.</summary>
public enum StatusDasFerias
{
    Solicitada,
    Aprovada,
    Rejeitada,
    EmGozo,
    Concluida,
    Cancelada,
}

/// <summary>
/// Texto no banco (<c>FERIAS.RESULTADO_GESTOR</c>): o que o gestor fez na primeira etapa. Diferente do enum do ponto, férias tem a terceira saída, <c>SUGERIU_DATA</c> (o pedido espera a resposta da pessoa).
/// </summary>
public enum ResultadoDoGestorNasFerias
{
    Aprovou,
    Recusou,
    SugeriuData,
}

/// <summary>Texto no banco (<c>FERIAS.RESPOSTA_SUGESTAO</c>): a resposta da pessoa à sugestão de outra data; <c>Expirou</c> é a tarefa agendada, sem resposta.</summary>
public enum RespostaDaSugestao
{
    Aceitou,
    Recusou,
    Expirou,
}
