using RHSystem.Application.Escopo;

namespace RHSystem.Application.Ponto;

/// <summary><c>GET /api/eu/ponto/hoje</c>: o cartão da PRÓPRIA pessoa logada (qualquer perfil, inclusive gestor e RH).</summary>
public sealed class ObterPontoHojeCasoDeUso(IEscopoDoUsuario escopo, MontadorDoCartaoDeHoje montador)
{
    public async Task<PontoHoje> ExecutarAsync(CancellationToken ct) => await montador.MontarAsync(escopo.FuncionarioId, ct);
}
