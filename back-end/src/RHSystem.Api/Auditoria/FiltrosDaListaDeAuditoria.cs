using RHSystem.Application.Auditoria;

namespace RHSystem.Api.Auditoria;

/// <summary>
/// Os parâmetros de <c>GET /api/rh/auditoria</c> e SÓ eles: o contrato não declara <c>ordenarPor</c> nem <c>direcao</c> aqui (a ordem é fixa), então não
/// se usa o <c>ParametrosDeLista</c> inteiro em <c>[AsParameters]</c>: o documento gerado listaria os cinco e divergiria do contrato. <c>Area</c> e <c>Acao</c>
/// entram como texto e o caso de uso os lê pelo nome do contrato (<c>ACESSO_NEGADO</c>), junto com os outros erros.
/// </summary>
public sealed record FiltrosDaListaDeAuditoria(
    int Pagina = 1,
    int TamanhoPagina = 20,
    string? Busca = null,
    DateOnly? De = null,
    DateOnly? Ate = null,
    string? Area = null,
    string? Acao = null,
    bool ApenasAlteracoes = false)
{
    public PedidoDeAuditoria ParaPedido() => new(De, Ate, Busca, Area, Acao, ApenasAlteracoes);
}
