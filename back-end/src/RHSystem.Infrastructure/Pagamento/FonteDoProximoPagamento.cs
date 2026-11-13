using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao;

namespace RHSystem.Infrastructure.Pagamento;

/// <summary>
/// A fonte do campo <c>proximoPagamento</c> da ficha do RH (<c>obterIndicadoresDoFuncionario</c>): a MESMA conta do cartão "Próximo pagamento" (<see cref="IProximoPagamento"/>), só a data. <c>null</c> quando a empresa não tem dia de pagamento nem exceção.
/// </summary>
public sealed class FonteDoProximoPagamento(IProximoPagamento proximo, IRelogio relogio) : IFonteDoProximoPagamento
{
    public async Task<DateOnly?> ObterAsync(CancellationToken ct) =>
        (await proximo.ObterAsync(FusoBrasil.Hoje(relogio.AgoraUtc), ct))?.Data;
}
