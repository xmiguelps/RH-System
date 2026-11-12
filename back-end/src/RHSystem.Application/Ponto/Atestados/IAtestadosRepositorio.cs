using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Atestados;

/// <summary>As leituras dos atestados. O escopo sai na PRIMEIRA consulta: o atestado de fora dele nem é lido (404 igual ao do inexistente). Mais recente primeiro (o instante de envio, e o id maior no empate).</summary>
public interface IConsultaDeAtestados
{
    Task<AtestadoLido?> ObterAsync(int atestadoId, EscopoDeAtestados escopo, CancellationToken ct);

    Task<PaginaDeAtestados> ListarAsync(EscopoDeAtestados escopo, FiltroDeAtestados filtro, ParametrosDeLista parametros, CancellationToken ct);
}

/// <summary>As gravações do atestado.</summary>
public interface IAtestadosRepositorio
{
    /// <summary>Grava o atestado novo e devolve o id (o evento de auditoria precisa dele).</summary>
    Task<int> AdicionarAsync(Atestado atestado, CancellationToken ct);

    /// <summary>
    /// A decisão final do RH, por UPDATE condicional em <c>STATUS_ATESTADO = 'ENVIADO'</c>: <c>false</c> quando outra decisão chegou antes (duas abas, uma só vale). <paramref name="motivo"/> só vai quando a decisão é <see cref="SituacaoDoAtestado.Recusado"/>.
    /// </summary>
    Task<bool> DecidirAsync(int atestadoId, SituacaoDoAtestado decisao, int funcionarioRhId, DateTime agoraUtc, string? motivo, CancellationToken ct);
}
