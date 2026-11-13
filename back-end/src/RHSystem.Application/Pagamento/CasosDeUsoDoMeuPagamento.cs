using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao;

namespace RHSystem.Application.Pagamento;

/// <summary>
/// <c>GET /api/eu/pagamento/proximo</c>: a data do próximo pagamento e o mês do holerite (a competência, SEMPRE o mês anterior ao do pagamento), para qualquer perfil. É a <b>definição única</b> do dado: o cartão do painel (<c>GET /api/eu/painel</c>) devolve este mesmo <see cref="PagamentoProximoDto"/>. Vale de hoje em
/// diante (hoje inclusive), pela mesma conta de <see cref="IProximoPagamento"/>; empresa sem dia de pagamento e sem exceção: os dois campos <c>null</c> (200, o cartão fica escondido).
/// </summary>
public sealed class ObterProximoPagamentoCasoDeUso(IProximoPagamento proximo, IRelogio relogio)
{
    public async Task<PagamentoProximoDto> ExecutarAsync(CancellationToken ct)
    {
        var pagamento = await proximo.ObterAsync(FusoBrasil.Hoje(relogio.AgoraUtc), ct);
        return pagamento is null
            ? new PagamentoProximoDto(null, null)
            : new PagamentoProximoDto(PagamentoDataDto.De(pagamento), new PagamentoCompetenciaDto(pagamento.Competencia.Ano, pagamento.Competencia.Mes));
    }
}
