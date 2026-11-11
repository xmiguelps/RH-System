using RHSystem.Application.Auditoria;

namespace RHSystem.Api.Auditoria;

/// <summary>Os parâmetros de <c>GET /api/rh/auditoria/exportacao</c>: os filtros da lista, sem paginação e sem <c>apenasAlteracoes</c> (que é só do painel do RH).</summary>
public sealed record FiltrosDaExportacaoDeAuditoria(
    string? Busca = null,
    DateOnly? De = null,
    DateOnly? Ate = null,
    string? Area = null,
    string? Acao = null)
{
    public PedidoDeAuditoria ParaPedido() => new(De, Ate, Busca, Area, Acao);
}
