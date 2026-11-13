using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Painel;

/// <summary>
/// As leituras do painel do RH que nenhuma outra fatia tem pronta: as cinco filas de pendências (cada uma com o MESMO critério da tela de origem) e as pessoas que contam em "Presença agora". Cada fila é UMA consulta (as pessoas dos itens vêm em lote), nunca uma por item. <b>Pendência é só o que o RH já pode decidir</b>: o
/// pedido ainda na etapa do gestor não conta, e pessoa inativa nunca é pendência de benefício.
/// </summary>
public interface IConsultaDoPainelDoRh
{
    /// <summary>Pedidos de ajuste de ponto <c>PENDENTE</c> na etapa do RH (o gestor já aprovou, a aprovação por ausência ou sem etapa do gestor): o mesmo critério do contador do menu do ponto. Desde: a aprovação do gestor, quando há, senão o pedido.</summary>
    Task<IReadOnlyList<PendenciaLida>> AjustesDePontoNaEtapaDoRhAsync(CancellationToken ct);

    /// <summary>Férias <c>COM_O_RH</c> (a mesma situação da lista do RH). Desde: a aprovação do gestor, quando há; a resposta da pessoa à sugestão aceita; senão a solicitação.</summary>
    Task<IReadOnlyList<PendenciaLida>> FeriasNaEtapaDoRhAsync(CancellationToken ct);

    /// <summary>Atestados <c>ENVIADO</c>, desde o envio.</summary>
    Task<IReadOnlyList<PendenciaLida>> AtestadosEnviadosAsync(CancellationToken ct);

    /// <summary>
    /// Pessoas ativas que começam de hoje a 7 dias à frente, estão atribuídas a um formulário de admissão PUBLICADO e ainda não o responderam. A referência é a própria pessoa; desde: a data de admissão às 00:00 em -03:00, em UTC (não há instante guardado).
    /// </summary>
    Task<IReadOnlyList<PendenciaLida>> AdmissoesComFormularioPendenteAsync(DateOnly hoje, CancellationToken ct);

    /// <summary>Adesões com pedido <c>PENDENTE</c> (a adesão, um dependente ou um trajeto): um item por adesão, pelo pedido mais antigo dela, de pessoa ativa; a referência é a adesão.</summary>
    Task<IReadOnlyList<PendenciaLida>> BeneficiosPendentesAsync(CancellationToken ct);

    /// <summary>As pessoas ativas e já admitidas (<c>DataAdmissao &lt;= hoje</c>): as que contam em "Presença agora".</summary>
    Task<IReadOnlyList<int>> IdsDasPessoasAtivasEAdmitidasAsync(DateOnly hoje, CancellationToken ct);

    /// <summary>A ficha curta das pessoas, em UMA consulta.</summary>
    Task<IReadOnlyList<FuncionarioResumo>> ResumosAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct);
}
