using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Comum;

/// <summary>Em que etapa está um pedido de aprovação em duas etapas (<c>EtapaDoPedido</c> da base do contrato; o ajuste de ponto e as férias usam a mesma). No JSON: <c>GESTOR</c>, <c>RH</c>, <c>CONCLUIDA</c>.</summary>
public enum EtapaDoPedido
{
    Gestor,
    Rh,
    Concluida,
}

/// <summary>O que uma etapa decidiu. No JSON: <c>APROVADA</c> ou <c>REJEITADA</c> (no banco do ponto, <c>RESULTADO_GESTOR</c> é <c>APROVOU</c> ou <c>RECUSOU</c>).</summary>
public enum DecisaoDaEtapa
{
    Aprovada,
    Rejeitada,
}

/// <summary>
/// A decisão de uma etapa (<c>DecisaoDeEtapa</c> da base). <see cref="DecididaPor"/> é <c>null</c> quando o gestor estava ausente e a etapa foi aprovada sozinha (<paramref name="Automatica"/>); <paramref name="DecididaEmUtc"/> é o momento em que ela
/// valeu. A <see cref="Observacao"/> é opcional. <c>decididaPor</c> e <c>observacao</c> são propriedades (não parâmetros do construtor) porque o contrato não os lista em <c>required</c>: sempre saem no JSON, mas o schema da API não os exige.
/// </summary>
public sealed record DecisaoDeEtapa(DecisaoDaEtapa Decisao, DateTime DecididaEmUtc, bool Automatica)
{
    public FuncionarioResumo? DecididaPor { get; init; }

    public string? Observacao { get; init; }
}
