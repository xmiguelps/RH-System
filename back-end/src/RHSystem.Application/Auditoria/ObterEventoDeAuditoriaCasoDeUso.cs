using RHSystem.Application.Comum;

namespace RHSystem.Application.Auditoria;

/// <summary>O detalhe de um evento (registro afetado, origem e o que mudou). Id que não existe é 404.</summary>
public sealed class ObterEventoDeAuditoriaCasoDeUso(IConsultaDeAuditoria consulta)
{
    public async Task<AuditoriaEventoDetalhe> ExecutarAsync(long eventoId, CancellationToken ct) =>
        (await consulta.ObterAsync(eventoId, ct)).OuNaoEncontrado();
}
