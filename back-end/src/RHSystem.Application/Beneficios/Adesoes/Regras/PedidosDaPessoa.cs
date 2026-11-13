using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>Um pedido da pessoa como o banco o guarda: adesão, dependente ou trajeto, com o nome do benefício a que pertence.</summary>
public sealed record PedidoParaResumir(
    TipoDoPedidoDeBeneficio Tipo,
    int Id,
    int AdesaoId,
    string NomeDoBeneficio,
    DateTime PedidoEmUtc,
    StatusDoPedidoDeBeneficio Situacao,
    string? MotivoDaRecusa);

/// <summary>Uma linha de "Seus pedidos" no formato do contrato (<c>BeneficioAdesaoPedidoResumo</c>), que é também a resposta dos três pedidos.</summary>
public sealed record ResumoDoPedido(
    TipoDoPedidoDeBeneficio Tipo,
    int Id,
    int AdesaoId,
    string Titulo,
    DateTime PedidoEmUtc,
    StatusDoPedidoDeBeneficio Situacao,
    string? MotivoDaRecusa,
    DateOnly RespostaPrevistaAte);

public static class PedidosDaPessoa
{
    public static ResumoDoPedido Resumir(PedidoParaResumir pedido, IReadOnlySet<DateOnly> feriados) =>
        new(
            pedido.Tipo,
            pedido.Id,
            pedido.AdesaoId,
            TituloDoPedido.De(pedido.Tipo, pedido.NomeDoBeneficio),
            pedido.PedidoEmUtc,
            pedido.Situacao,
            pedido.MotivoDaRecusa,
            RespostaPrevista.De(pedido.PedidoEmUtc, feriados));

    /// <summary>"Seus pedidos": do mais novo para o mais antigo; no mesmo instante, a adesão antes do dependente e do trajeto, e o id maior antes.</summary>
    public static IReadOnlyList<ResumoDoPedido> Montar(IEnumerable<PedidoParaResumir> pedidos, IReadOnlySet<DateOnly> feriados) =>
        pedidos
            .OrderByDescending(p => p.PedidoEmUtc)
            .ThenBy(p => p.Tipo)
            .ThenByDescending(p => p.Id)
            .Select(p => Resumir(p, feriados))
            .ToList();
}
